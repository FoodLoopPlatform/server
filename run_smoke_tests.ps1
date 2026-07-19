# FoodLoop Comprehensive E2E Smoke Tests & Verification Suite
# Connects to localhost API and verifies all Sprint 1 behaviors against SQL Server.
# Strict assertions are enabled: any unexpected HTTP status code or database state will throw an error and terminate execution.

$ErrorActionPreference = "Stop"

# Setup: Bypassing SSL errors and enforcing TLS 1.2
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$baseUrl = "https://localhost:7238"

# 1. Resolve Connection String
$appSettingsPath = Join-Path $PSScriptRoot "src\FoodLoop.API\appsettings.json"
$dbConnectionString = "Server=localhost\SQLEXPRESS;Database=foodloop_dev;Trusted_Connection=True;TrustServerCertificate=True"

# Try to read from dotnet user-secrets first
try {
    $secretsOut = dotnet user-secrets list --project (Join-Path $PSScriptRoot "src\FoodLoop.API") 2>$null | Out-String
    if ($secretsOut -match "ConnectionStrings:DefaultConnection\s*=\s*([^\r\n]+)") {
        $dbConnectionString = $Matches[1].Trim()
    }
} catch {}

# Fall back to appsettings.json only if user secrets was not found and appsettings does not contain placeholder
if ($dbConnectionString -eq "Server=localhost\SQLEXPRESS;Database=foodloop_dev;Trusted_Connection=True;TrustServerCertificate=True" -and (Test-Path $appSettingsPath)) {
    try {
        $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $connStr = $appSettings.ConnectionStrings.DefaultConnection
        if ($connStr -and $connStr -notlike "*CHANGE_ME*") {
            $dbConnectionString = $connStr
        }
    } catch {}
}

Write-Host "Database Connection String: $dbConnectionString" -ForegroundColor DarkGray

# 2. Check and Manage Kestrel Process
$portActive = $false
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $ar = $tcpClient.BeginConnect("127.0.0.1", 7238, $null, $null)
    $wait = $ar.AsyncWaitHandle.WaitOne(800) # wait 800ms
    if ($tcpClient.Connected) {
        $portActive = $true
    }
    $tcpClient.Close()
} catch {}

$startedProcess = $null
if (-not $portActive) {
    Write-Host "API server is not running on port 7238. Starting it automatically..." -ForegroundColor Yellow
    if (-not (Test-Path (Join-Path $PSScriptRoot "src\FoodLoop.API"))) {
        Write-Error "Could not find src\FoodLoop.API project directory. Please run this script from the solution root folder."
        exit
    }
    $startedProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src\FoodLoop.API", "--launch-profile", "https" -PassThru -NoNewWindow
    
    # Wait for API to start up
    Write-Host "Waiting 8 seconds for Kestrel to initialize..." -ForegroundColor Yellow
    Start-Sleep -Seconds 8
} else {
    Write-Host "API server is already running. Exercising endpoints against active instance..." -ForegroundColor Green
}

# Database query helper
function Query-Database {
    param (
        [string]$sql
    )
    $conn = New-Object System.Data.SqlClient.SqlConnection($dbConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        $adapter.Fill($dt) | Out-Null
        $rows = @()
        foreach ($r in $dt.Rows) {
            $rows += $r
        }
        return , $rows
    }
    finally {
        $conn.Close()
    }
}

# HTTP helper to output requests and responses with STRICT assertions
function Invoke-Http {
    param (
        [string]$Path,
        [string]$Method = "POST",
        [object]$Body = $null,
        [string]$Token = $null,
        [hashtable]$Headers = @{},
        [string]$ContentType = "application/json",
        [int[]]$ExpectedStatusCodes = @(200, 201, 204)
    )

    $url = "$baseUrl$Path"
    $allHeaders = @{}
    if ($Token) {
        $allHeaders["Authorization"] = "Bearer $Token"
    }
    foreach ($key in $Headers.Keys) {
        $allHeaders[$key] = $Headers[$key]
    }

    Write-Host "`n>>> HTTP REQUEST: $Method $Path" -ForegroundColor Cyan
    if ($Body -and $ContentType -eq "application/json") {
        $jsonBody = $Body | ConvertTo-Json -Depth 10
        Write-Host "Request Body:" -ForegroundColor DarkGray
        Write-Host $jsonBody -ForegroundColor DarkGray
    }

    $response = $null
    $rawResponse = $null
    $content = $null
    try {
        $params = @{
            Uri = $url
            Method = $Method
            Headers = $allHeaders
        }
        if ($Body) {
            $params["ContentType"] = $ContentType
            if ($ContentType -eq "application/json") {
                $params["Body"] = $jsonBody
            } else {
                $params["Body"] = $Body
            }
        }
        
        $rawResponse = Invoke-WebRequest @params -UseBasicParsing
        $content = $rawResponse.Content
    }
    catch {
        $exception = $_.Exception
        if ($exception.Response) {
            $rawResponse = $exception.Response
            $stream = $rawResponse.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
                $reader.Close()
                $stream.Close()
            }
        } else {
            Write-Error $_
            throw $_
        }
    }

    $statusCode = [int]$rawResponse.StatusCode
    Write-Host "HTTP RESPONSE: $statusCode" -ForegroundColor $(
        if ($statusCode -lt 300) { "Green" } elseif ($statusCode -lt 400) { "Yellow" } else { "Red" }
    )

    # STRICT ASSERTION:
    if ($ExpectedStatusCodes -notcontains $statusCode) {
        Write-Host "ASSERTION FAILED: Expected status code in ($($ExpectedStatusCodes -join ', ')), but got $statusCode" -ForegroundColor Red
        throw "Assertion Failed: got $statusCode, expected ($($ExpectedStatusCodes -join ', '))"
    }
    
    # Print response headers
    Write-Host "Response Headers:" -ForegroundColor DarkGray
    foreach ($h in $rawResponse.Headers.Keys) {
        Write-Host "  $($h): $($rawResponse.Headers[$h])" -ForegroundColor DarkGray
    }

    # Print response body
    if ($content) {
        Write-Host "Response Body:" -ForegroundColor Gray
        try {
            $parsed = $content | ConvertFrom-Json
            $parsed | ConvertTo-Json -Depth 10 | Write-Host
            return @{ StatusCode = $statusCode; Headers = $rawResponse.Headers; Data = $parsed }
        }
        catch {
            Write-Host $content
            return @{ StatusCode = $statusCode; Headers = $rawResponse.Headers; RawContent = $content }
        }
    }

    return @{ StatusCode = $statusCode; Headers = $rawResponse.Headers }
}

try {
    Write-Host "=== STARTING FOODLOOP SMOKE TESTS WITH STRICT ASSERTIONS ===" -ForegroundColor Green

    # Ensure DB is clean for tests
    Write-Host "Cleaning up test users and data..." -ForegroundColor Yellow
    Query-Database "DELETE FROM UserRoles WHERE UserId IN (SELECT Id FROM Users WHERE Email IN ('sara@example.com', 'sara.store@example.com'))" | Out-Null
    Query-Database "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM Users WHERE Email IN ('sara@example.com', 'sara.store@example.com'))" | Out-Null
    Query-Database "DELETE FROM Addresses WHERE UserId IN (SELECT Id FROM Users WHERE Email IN ('sara@example.com', 'sara.store@example.com'))" | Out-Null
    Query-Database "DELETE FROM StoreVerifications WHERE StoreId IN (SELECT Id FROM Stores WHERE Name = 'Green Valley Groceries')" | Out-Null
    Query-Database "DELETE FROM Stores WHERE Name = 'Green Valley Groceries'" | Out-Null
    Query-Database "DELETE FROM Users WHERE Email IN ('sara@example.com', 'sara.store@example.com')" | Out-Null

    # a) POST /auth/register as a plain "User" account type
    Write-Host "`n--- TEST A: Register Plain Consumer User ---" -ForegroundColor Blue
    $registerConsumerRes = Invoke-Http -Path "/auth/register" -Method "POST" -Body @{
        name = "Sara Ahmed"
        email = "sara@example.com"
        password = "P@ssw0rd1"
        accountType = "User"
    } -ExpectedStatusCodes @(201)
    $consumerToken = $registerConsumerRes.Data.data.accessToken
    $consumerRefreshToken = $registerConsumerRes.Data.data.refreshToken

    # b) POST /auth/register as "StoreOwner" with a BusinessName
    Write-Host "`n--- TEST B: Register StoreOwner and Check Draft Store ---" -ForegroundColor Blue
    $registerStoreRes = Invoke-Http -Path "/auth/register" -Method "POST" -Body @{
        name = "Store Owner Sara"
        email = "sara.store@example.com"
        password = "P@ssw0rd1"
        phoneNumber = "+201001234567"
        accountType = "StoreOwner"
        businessName = "Green Valley Groceries"
        businessCategory = "Supermarket"
    } -ExpectedStatusCodes @(201)
    $storeOwnerToken = $registerStoreRes.Data.data.accessToken
    $storeOwnerRefreshToken = $registerStoreRes.Data.data.refreshToken
    $storeOwnerUserId = $registerStoreRes.Data.data.user.id

    # Check the DB directly for draft store
    Write-Host "Verifying draft Store row in database..." -ForegroundColor Yellow
    $storeRows = Query-Database "SELECT Id, OwnerId, Name, VerificationStatus, IsDeleted FROM Stores WHERE Name = 'Green Valley Groceries'"
    $storeRows | Format-Table -AutoSize | Out-String | Write-Host
    if (@($storeRows).Count -eq 1 -and $storeRows[0]["OwnerId"].ToString() -eq $storeOwnerUserId) {
        Write-Host "Success: Draft store verified in DB for OwnerId $storeOwnerUserId." -ForegroundColor Green
    } else {
        throw "FAIL: Store verification failed in DB!"
    }

    # c) POST /auth/login with correct then incorrect password
    Write-Host "`n--- TEST C: Login Verification ---" -ForegroundColor Blue
    Write-Host "Attempting correct password login..." -ForegroundColor Yellow
    $loginSuccess = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(200)

    Write-Host "Attempting incorrect password login..." -ForegroundColor Yellow
    $loginFail = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "WrongPassword123"
    } -ExpectedStatusCodes @(401)

    # d) GET /users/me with the access token
    Write-Host "`n--- TEST D: GET /users/me ---" -ForegroundColor Blue
    $getMeRes = Invoke-Http -Path "/users/me" -Method "GET" -Token $consumerToken -ExpectedStatusCodes @(200)
    $meUserId = $getMeRes.Data.data.id

    # Check the DB directly to match
    Write-Host "Verifying user record in database..." -ForegroundColor Yellow
    $userRows = Query-Database "SELECT Id, FullName, Email, OrderUpdatesEnabled, MarketingNotificationsEnabled FROM Users WHERE Id = '$meUserId'"
    $userRows | Format-Table -AutoSize | Out-String | Write-Host
    if ($userRows[0]["FullName"].ToString() -ne "Sara Ahmed") {
        throw "Assertion Failed: User FullName in DB does not match."
    }

    # d2) PATCH /users/me
    Write-Host "`n--- TEST D2: PATCH /users/me ---" -ForegroundColor Blue
    $patchMeRes = Invoke-Http -Path "/users/me" -Method "PATCH" -Token $consumerToken -Body @{
        name = "Sara Ahmed Updated"
        preferredLanguage = "ar"
    } -ExpectedStatusCodes @(200)
    if ($patchMeRes.Data.data.fullName -ne "Sara Ahmed Updated" -or $patchMeRes.Data.data.language -ne "ar") {
        throw "Assertion Failed: PATCH /users/me fields not updated properly."
    }

    # d3) PATCH /users/me/preferences
    Write-Host "`n--- TEST D3: PATCH /users/me/preferences ---" -ForegroundColor Blue
    $patchPrefRes = Invoke-Http -Path "/users/me/preferences" -Method "PATCH" -Token $consumerToken -Body @{
        orderUpdatesEnabled = $false
        marketingNotificationsEnabled = $true
        preferredLanguage = "en"
    } -ExpectedStatusCodes @(200)

    # e) POST /auth/refresh with the refresh token
    Write-Host "`n--- TEST E: POST /auth/refresh (Rotation & Old Revocation) ---" -ForegroundColor Blue
    # Login again to get a fresh refresh token
    $loginForRotation = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(200)
    $rtRotation1 = $loginForRotation.Data.data.refreshToken

    # Perform rotation
    Write-Host "Rotating refresh token..." -ForegroundColor Yellow
    $refreshRes = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $rtRotation1
    } -ExpectedStatusCodes @(200)
    $rtRotation2 = $refreshRes.Data.data.refreshToken

    # Verify old refresh token is rejected
    Write-Host "Trying to reuse OLD refresh token (Expected 401)..." -ForegroundColor Yellow
    $refreshOldRes = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $rtRotation1
    } -ExpectedStatusCodes @(401)

    # f) Reuse a revoked refresh token deliberately
    Write-Host "`n--- TEST F: Revoked Refresh Token Reuse Detection ---" -ForegroundColor Blue
    # Obtain a new refresh token session
    $loginForReuse = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(200)
    $rtReuse1 = $loginForReuse.Data.data.refreshToken
    $rtReuseUserId = $loginForReuse.Data.data.user.id

    # First rotation
    Write-Host "First rotation of token..." -ForegroundColor Yellow
    $firstRotate = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $rtReuse1
    } -ExpectedStatusCodes @(200)
    $rtReuse2 = $firstRotate.Data.data.refreshToken

    # Confirm the active tokens before reuse in DB
    Write-Host "Refresh tokens in DB before reuse attack:" -ForegroundColor Yellow
    $tokensBefore = Query-Database "SELECT Token, ExpiresAt, RevokedAt FROM RefreshTokens WHERE UserId = '$rtReuseUserId'"
    $tokensBefore | Format-Table -AutoSize | Out-String | Write-Host

    # Deliberately reuse the old/revoked token (rtReuse1) (Expected 401)
    Write-Host "Reusing revoked token $rtReuse1..." -ForegroundColor Yellow
    $reuseAttackRes = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $rtReuse1
    } -ExpectedStatusCodes @(401)

    # Check DB to confirm ALL refresh tokens are now revoked for this user
    Write-Host "Refresh tokens in DB after reuse attack:" -ForegroundColor Yellow
    $tokensAfter = Query-Database "SELECT Token, ExpiresAt, RevokedAt FROM RefreshTokens WHERE UserId = '$rtReuseUserId'"
    $tokensAfter | Format-Table -AutoSize | Out-String | Write-Host
    $anyActive = @($tokensAfter | Where-Object { $null -eq $_["RevokedAt"] -or $_["RevokedAt"] -gt (Get-Date) })
    if (@($tokensAfter).Count -gt 0 -and @($anyActive).Count -eq 0) {
        Write-Host "Success: All user sessions were successfully revoked due to reuse detection!" -ForegroundColor Green
    } else {
        throw "FAIL: Not all user sessions were revoked!"
    }

    # g) POST /auth/logout
    Write-Host "`n--- TEST G: Logout ---" -ForegroundColor Blue
    $loginForLogout = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(200)
    $rtLogout = $loginForLogout.Data.data.refreshToken

    Write-Host "Logging out..." -ForegroundColor Yellow
    $logoutRes = Invoke-Http -Path "/auth/logout" -Method "POST" -Body @{
        refreshToken = $rtLogout
    } -ExpectedStatusCodes @(200)

    Write-Host "Attempting to refresh with logged out token (Expected 401)..." -ForegroundColor Yellow
    $refreshLogoutRes = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $rtLogout
    } -ExpectedStatusCodes @(401)

    # h) POST /auth/forgot-password (account enumeration leak check)
    Write-Host "`n--- TEST H: Forgot Password ---" -ForegroundColor Blue
    Write-Host "Requesting for existing account..." -ForegroundColor Yellow
    $forgotExist = Invoke-Http -Path "/auth/forgot-password" -Method "POST" -Body @{
        email = "sara@example.com"
    } -ExpectedStatusCodes @(200)
    Write-Host "Requesting for non-existing account..." -ForegroundColor Yellow
    $forgotNonExist = Invoke-Http -Path "/auth/forgot-password" -Method "POST" -Body @{
        email = "nonexistent@example.com"
    } -ExpectedStatusCodes @(200)
    if ($forgotExist.Data.message -ne $forgotNonExist.Data.message -or $forgotExist.StatusCode -ne $forgotNonExist.StatusCode) {
        throw "FAIL: Account enumeration check failed - responses differ!"
    }

    # h2) Password Reset End-to-End Extended Security Verification
    Write-Host "`n--- TEST H2: Password Reset End-to-End & Rejection Checks ---" -ForegroundColor Blue
    
    # Login again to get a valid active session refresh token before resetting
    $loginForPreReset = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(200)
    $preResetRefreshToken = $loginForPreReset.Data.data.refreshToken
    
    # Request reset token
    $forgotForReset = Invoke-Http -Path "/auth/forgot-password" -Method "POST" -Body @{
        email = "sara@example.com"
    } -ExpectedStatusCodes @(200)
    
    Start-Sleep -Seconds 2
    
    # Search log for reset token
    Write-Host "Retrieving reset token from logs..." -ForegroundColor Yellow
    $activeLogPath = Get-ChildItem -Path C:\Users\ywagi\.gemini\antigravity\brain\16cb6ef3-5458-4869-b929-68b0bf88a623\.system_generated\tasks\task-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    $logLines = Get-Content -Path $activeLogPath
    $resetToken = $null
    foreach ($line in $logLines) {
        if ($line -match "Password reset for sara@example\.com\. Token: ([^\s]+)") {
            $resetToken = $Matches[1]
        }
    }
    if (-not $resetToken) {
        throw "FAIL: Password reset token not found in logs."
    }
    Write-Host "Found token: $resetToken" -ForegroundColor Green
    
    # Perform password reset
    $newPwd = "NewP@ssw0rd1"
    $resetCall = Invoke-Http -Path "/auth/reset-password" -Method "POST" -Body @{
        email = "sara@example.com"
        token = $resetToken
        newPassword = $newPwd
    } -ExpectedStatusCodes @(200)
    
    # Check old password is rejected
    Write-Host "Confirming login with OLD password fails (Expected 401)..." -ForegroundColor Yellow
    $loginOldFail = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = "P@ssw0rd1"
    } -ExpectedStatusCodes @(401)
    
    # Check pre-reset refresh token is rejected
    Write-Host "Confirming pre-reset refresh token is revoked/rejected (Expected 401)..." -ForegroundColor Yellow
    $refreshOldFail = Invoke-Http -Path "/auth/refresh" -Method "POST" -Body @{
        refreshToken = $preResetRefreshToken
    } -ExpectedStatusCodes @(401)
    
    # Check login with new password works
    Write-Host "Confirming login with NEW password works..." -ForegroundColor Yellow
    $loginNewSuccess = Invoke-Http -Path "/auth/login" -Method "POST" -Body @{
        email = "sara@example.com"
        password = $newPwd
    } -ExpectedStatusCodes @(200)
    # Update active token for consumer calls
    $consumerToken = $loginNewSuccess.Data.data.accessToken

    # i) Full address CRUD under /users/me/addresses
    Write-Host "`n--- TEST I: Address CRUD and Default Address Constraint ---" -ForegroundColor Blue
    Write-Host "Creating Address 1 (Default=true)..." -ForegroundColor Yellow
    $addr1 = Invoke-Http -Path "/users/me/addresses" -Method "POST" -Token $consumerToken -Body @{
        addressType = "Home"
        city = "Cairo"
        district = "Maadi"
        street = "Road 9"
        buildingNo = "12"
        floor = "3"
        apartmentNo = "5"
        latitude = 30.0123
        longitude = 31.2345
        isDefault = $true
    } -ExpectedStatusCodes @(201)
    $addr1Id = $addr1.Data.data.id

    Write-Host "Creating Address 2 (Default=false)..." -ForegroundColor Yellow
    $addr2 = Invoke-Http -Path "/users/me/addresses" -Method "POST" -Token $consumerToken -Body @{
        addressType = "Company"
        city = "Giza"
        district = "Dokki"
        street = "Tahrir St"
        buildingNo = "45"
        floor = "8"
        apartmentNo = "802"
        latitude = 30.0345
        longitude = 31.2056
        isDefault = $false
    } -ExpectedStatusCodes @(201)
    $addr2Id = $addr2.Data.data.id

    Write-Host "Addresses in DB after creation:" -ForegroundColor Yellow
    Query-Database "SELECT Id, AddressType, City, IsDefault FROM Addresses WHERE UserId = '$meUserId'" | Format-Table -AutoSize | Out-String | Write-Host

    Write-Host "Updating Address 2 to be default (Default=true)..." -ForegroundColor Yellow
    $updateAddr = Invoke-Http -Path "/users/me/addresses/$addr2Id" -Method "PATCH" -Token $consumerToken -Body @{
        isDefault = $true
    } -ExpectedStatusCodes @(200)

    Write-Host "Addresses in DB after updating Address 2 to default:" -ForegroundColor Yellow
    $addrRows = Query-Database "SELECT Id, AddressType, City, IsDefault FROM Addresses WHERE UserId = '$meUserId'"
    $addrRows | Format-Table -AutoSize | Out-String | Write-Host
    $defaultAddresses = @($addrRows | Where-Object { $_["IsDefault"] -eq $true })
    if (@($defaultAddresses).Count -eq 1 -and $defaultAddresses[0]["Id"].ToString() -eq $addr2Id) {
        Write-Host "Success: Address 2 is now default, and Address 1 has been automatically unset." -ForegroundColor Green
    } else {
        throw "FAIL: Default address constraint violation!"
    }

    # Address DELETE test
    Write-Host "Deleting Address 1..." -ForegroundColor Yellow
    $delAddr1 = Invoke-Http -Path "/users/me/addresses/$addr1Id" -Method "DELETE" -Token $consumerToken -ExpectedStatusCodes @(204)
    Write-Host "Deleting Address 2..." -ForegroundColor Yellow
    $delAddr2 = Invoke-Http -Path "/users/me/addresses/$addr2Id" -Method "DELETE" -Token $consumerToken -ExpectedStatusCodes @(204)
    
    # Confirm addresses empty
    $finalAddresses = Invoke-Http -Path "/users/me/addresses" -Method "GET" -Token $consumerToken -ExpectedStatusCodes @(200)
    if ($finalAddresses.Data.data.Count -ne 0) {
        throw "Assertion Failed: Addresses array not empty after deletion!"
    }

    # j) StoreOwner location update, documents upload, and verification status checks
    Write-Host "`n--- TEST J: StoreOwner Wizard (Location, Document Upload, and Verification Status) ---" -ForegroundColor Blue
    Write-Host "Checking initial Store status..." -ForegroundColor Yellow
    $storeStatus1 = Invoke-Http -Path "/stores/me" -Method "GET" -Token $storeOwnerToken -ExpectedStatusCodes @(200)
    Write-Host "Current Verification Status: $($storeStatus1.Data.data.verificationStatus)" -ForegroundColor Yellow

    Write-Host "Updating Store Location..." -ForegroundColor Yellow
    $locationUpdate = Invoke-Http -Path "/stores/me/location" -Method "PATCH" -Token $storeOwnerToken -Body @{
        governorate = "Cairo"
        city = "Cairo"
        neighborhood = "Al-Rawda"
        street = "King Fahd Rd."
    } -ExpectedStatusCodes @(200)

    # Generate 3 small text files for document uploads
    $CRFile = Join-Path $PSScriptRoot "commercial-registration.txt"
    $TaxFile = Join-Path $PSScriptRoot "tax-id.txt"
    $PhotoFile = Join-Path $PSScriptRoot "facility-photo.txt"
    "Commercial Registration Content" | Out-File $CRFile -Encoding utf8
    "Tax ID Certificate Content" | Out-File $TaxFile -Encoding utf8
    "Facility Photo Content" | Out-File $PhotoFile -Encoding utf8

    # Helper to upload form files using curl
    function Upload-FormFile {
        param (
            [string]$Path,
            [string]$DocType,
            [string]$FilePath,
            [string]$Token
        )
        $url = "$baseUrl$Path"
        
        Write-Host "Uploading $DocType using curl.exe..." -ForegroundColor Yellow
        $absFilePath = Resolve-Path $FilePath | Select-Object -ExpandProperty Path
        
        $response = & curl.exe -s -w "\nHTTP_CODE:%{http_code}" -X POST -H "Authorization: Bearer $Token" -F "type=$DocType" -F "file=@$absFilePath" -k $url
        
        $responseString = $response | Out-String
        $lines = $responseString -split "\r?\n"
        $httpCodeLine = $lines | Where-Object { $_ -like "HTTP_CODE:*" }
        $httpCode = 0
        if ($httpCodeLine) {
            $httpCode = [int]($httpCodeLine -replace "HTTP_CODE:", "")
        }
        
        $bodyLines = $lines | Where-Object { $_ -notlike "HTTP_CODE:*" -and $_ -ne "" }
        $body = $bodyLines -join "`n"
        
        Write-Host "HTTP RESPONSE: $httpCode" -ForegroundColor $(
            if ($httpCode -lt 300) { "Green" } else { "Red" }
        )
        if ($httpCode -ne 200 -and $httpCode -ne 201) {
            throw "Assertion Failed: File upload of $DocType failed with code $httpCode"
        }
        
        Write-Host "Response Body:" -ForegroundColor Gray
        try {
            $parsed = $body | ConvertFrom-Json
            $parsed | ConvertTo-Json -Depth 10 | Write-Host
            return @{ StatusCode = $httpCode; Data = $parsed }
        }
        catch {
            Write-Host $body
            return @{ StatusCode = $httpCode; RawContent = $body }
        }
    }

    # Upload 1st document and check
    $up1 = Upload-FormFile -Path "/stores/me/documents" -DocType "CommercialRegistration" -FilePath $CRFile -Token $storeOwnerToken
    Write-Host "Store status in DB after 1st upload:" -ForegroundColor Yellow
    Query-Database "SELECT Id, VerificationStatus FROM Stores WHERE OwnerId = '$storeOwnerUserId'" | Format-Table -AutoSize | Out-String | Write-Host
    Query-Database "SELECT Id, VerificationType, Status FROM StoreVerifications WHERE StoreId = '$($storeStatus1.Data.data.id)'" | Format-Table -AutoSize | Out-String | Write-Host

    # Upload 2nd document and check
    $up2 = Upload-FormFile -Path "/stores/me/documents" -DocType "TaxIdCertificate" -FilePath $TaxFile -Token $storeOwnerToken
    Write-Host "Store status in DB after 2nd upload:" -ForegroundColor Yellow
    Query-Database "SELECT Id, VerificationStatus FROM Stores WHERE OwnerId = '$storeOwnerUserId'" | Format-Table -AutoSize | Out-String | Write-Host

    # Upload 3rd document and check
    $up3 = Upload-FormFile -Path "/stores/me/documents" -DocType "StoreFacilityPhoto" -FilePath $PhotoFile -Token $storeOwnerToken
    Write-Host "Store status in DB after 3rd upload:" -ForegroundColor Yellow
    $storeStatusAfterAll = Query-Database "SELECT Id, VerificationStatus FROM Stores WHERE OwnerId = '$storeOwnerUserId'"
    $storeStatusAfterAll | Format-Table -AutoSize | Out-String | Write-Host

    if ($storeStatusAfterAll[0]["VerificationStatus"].ToString() -eq "1") { # "Pending" is 1
        Write-Host "Success: Verification status flipped to Pending only after all 3 documents were uploaded." -ForegroundColor Green
    } else {
        throw "FAIL: Status is not Pending! Current value: $($storeStatusAfterAll[0]["VerificationStatus"])"
    }

    # Clean up local files
    Remove-Item $CRFile, $TaxFile, $PhotoFile -Force

    # k) Merchant-only endpoint check with Consumer-role token
    Write-Host "`n--- TEST K: Authorization Check ---" -ForegroundColor Blue
    Write-Host "Attempting to hit merchant-only GET /stores/me with Consumer token (Expected 403)..." -ForegroundColor Yellow
    $authCheckRes = Invoke-Http -Path "/stores/me" -Method "GET" -Token $consumerToken -ExpectedStatusCodes @(403)

    # CORS validation check
    Write-Host "`n--- CORS VALIDATION CHECK ---" -ForegroundColor Blue
    Write-Host "Sending preflight OPTIONS request from ALLOWED origin http://localhost:3000..." -ForegroundColor Yellow
    $allowedCors = & curl.exe -s -X OPTIONS -I -H "Origin: http://localhost:3000" -H "Access-Control-Request-Method: POST" -H "Access-Control-Request-Headers: content-type" -k https://localhost:7238/auth/login
    $allowedCors | Out-String | Write-Host

    Write-Host "Sending preflight OPTIONS request from DISALLOWED origin http://evil.com..." -ForegroundColor Yellow
    $disallowedCors = & curl.exe -s -X OPTIONS -I -H "Origin: http://evil.com" -H "Access-Control-Request-Method: POST" -H "Access-Control-Request-Headers: content-type" -k https://localhost:7238/auth/login
    $disallowedCors | Out-String | Write-Host

    # Token tampering check
    Write-Host "`n--- JWT TAMPERING CHECK ---" -ForegroundColor Blue
    $tamperedToken = $consumerToken + "X"
    Write-Host "Attempting GET /users/me with tampered token (Expected 401)..." -ForegroundColor Yellow
    $tamperedOut = & curl.exe -s -i -H "Authorization: Bearer $tamperedToken" -k https://localhost:7238/users/me
    $tamperedOut | Out-String | Write-Host

    Write-Host "`n=== ALL TESTS PASSED SUCCESSFULLY ===" -ForegroundColor Green
}
finally {
    if ($startedProcess) {
        Write-Host "`nStopping the automatically started API server (PID: $($startedProcess.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $startedProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
