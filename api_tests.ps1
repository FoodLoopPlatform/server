# FoodLoop API Automated Test Suite (PowerShell Edition)
# This script executes happy paths, validation failures, not found, unauthorized, and state transition test scenarios.

$ErrorActionPreference = "Stop"

# --- Configuration & Environment Setup ---
$BaseUrl = "http://127.0.0.1:5267"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "FoodLoop Automated Integration Test Suite (PowerShell)" -ForegroundColor Cyan
Write-Host "Target Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Test Metrics
$PassCount = 0
$FailCount = 0

# Global State Variables (to pass IDs between steps)
$AdminToken = ""
$MerchantToken = ""
$CustomerToken = ""
$CharityToken = ""

$CustomerUserId = ""
$OrganizationId = ""
$COrgId = ""
$ProductId = ""
$OrderId = ""
$ReviewId = ""
$SupportTicketId = ""
$NotificationId = ""
$ImageId = ""

# Helper function to execute Web Requests
function Send-Request {
    param (
        [string]$Method,
        [string]$Route,
        [object]$Body = $null,
        [string]$Token = $null,
        [string]$ContentType = "application/json"
    )

    $Headers = @{}
    if ($Token) {
        $Headers.Add("Authorization", "Bearer $Token")
    }

    $Uri = "$BaseUrl$Route"
    $BodyJson = $null
    if ($Body -and $ContentType -eq "application/json") {
        $BodyJson = $Body | ConvertTo-Json -Depth 10
    } elseif ($Body) {
        $BodyJson = $Body
    }

    try {
        if ($BodyJson) {
            $Response = Invoke-WebRequest -Uri $Uri -Method $Method -Headers $Headers -Body $BodyJson -ContentType $ContentType -UseBasicParsing -TimeoutSec 10
        } else {
            $Response = Invoke-WebRequest -Uri $Uri -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec 10
        }

        $Json = $null
        if ($Response.Content) {
            try { $Json = $Response.Content | ConvertFrom-Json } catch {}
        }

        return [PSCustomObject]@{
            StatusCode = $Response.StatusCode
            Data = $Json
            Success = $true
            RawContent = $Response.Content
        }
    }
    catch {
        $ErrorResponse = $_.Exception.Response
        $StatusCode = 0
        $Json = $null
        $Raw = ""
        if ($ErrorResponse) {
            $StatusCode = [int]$ErrorResponse.StatusCode
            $StreamReader = [System.IO.StreamReader]::new($ErrorResponse.GetResponseStream())
            $Raw = $StreamReader.ReadToEnd()
            if ($Raw) {
                try { $Json = $Raw | ConvertFrom-Json } catch {}
            }
        }
        return [PSCustomObject]@{
            StatusCode = $StatusCode
            Data = $Json
            Success = $false
            RawContent = $Raw
        }
    }
}

# Helper function to assert statuses
function Assert-Status {
    param (
        [string]$Scenario,
        [int]$ActualCode,
        [int]$ExpectedCode,
        [object]$Data = $null
    )
    if ($ActualCode -eq $ExpectedCode) {
        Write-Host "[PASS] $Scenario (HTTP $ActualCode)" -ForegroundColor Green
        $global:PassCount++
        return $true
    } else {
        Write-Host "[FAIL] $Scenario (Expected: $ExpectedCode, Got: $ActualCode)" -ForegroundColor Red
        if ($Data) {
            Write-Host "       Response: $Data" -ForegroundColor Yellow
        }
        $global:FailCount++
        return $false
    }
}

# ==============================================================================
# SECTION 0: ROOT & HEALTH CHECK ENDPOINTS
# ==============================================================================
Write-Host "`n--- Testing Health & Root Routes ---" -ForegroundColor Yellow

# Scenario 0.1: GET /
$res = Send-Request -Method "GET" -Route "/"
Assert-Status -Scenario "0.1: GET Welcome Page" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.RawContent

# Scenario 0.2: GET /health
$res = Send-Request -Method "GET" -Route "/health"
Assert-Status -Scenario "0.2: GET Health Check" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.RawContent

# ==============================================================================
# SECTION 1: AUTHENTICATION MODULE (/auth)
# ==============================================================================
Write-Host "`n--- Testing Authentication Endpoints ---" -ForegroundColor Yellow

$RandomVal = Get-Random -Min 10000 -Max 99999
$MerchantEmail = "merchant.ps${RandomVal}@example.com"
$RegisterPayload = @{
    name = "Test Merchant PS"
    email = $MerchantEmail
    password = "Password@123"
    role = "Merchant"
    businessName = "Test Organic Shop PS"
}

$res = Send-Request -Method "POST" -Route "/auth/register" -Body $RegisterPayload
Assert-Status -Scenario "1.1: Register New Merchant Account" -ActualCode $res.StatusCode -ExpectedCode 201 -Data $res.Data

# Scenario 1.2: Register (Bad Request / Validation Failure)
$BadRegister = @{
    name = ""
    email = "invalidemail"
    password = "123"
    role = "InvalidRole"
}
$res = Send-Request -Method "POST" -Route "/auth/register" -Body $BadRegister
Assert-Status -Scenario "1.2: Register With Bad Validation Fields" -ActualCode $res.StatusCode -ExpectedCode 400 -Data $res.Data

# Scenario 1.3: Login
$LoginPayload = @{
    email = $MerchantEmail
    password = "Password@123"
}
$res = Send-Request -Method "POST" -Route "/auth/login" -Body $LoginPayload
Assert-Status -Scenario "1.3: Log In Merchant Account" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$MRefreshToken = $res.Data.data.refreshToken

# Log In Verified Merchant for subsequent store/product operations
$VerifiedMerchantLogin = @{
    email = "merchant.spinneys@example.com"
    password = "Password@123"
}
$res = Send-Request -Method "POST" -Route "/auth/login" -Body $VerifiedMerchantLogin
if ($res.StatusCode -eq 200) {
    $global:MerchantToken = $res.Data.data.accessToken
    $global:MRefreshToken = $res.Data.data.refreshToken
    Write-Host "[INFO] Loaded Verified Merchant token successfully." -ForegroundColor Green
}

# Scenario 1.4: Login Wrong Password
$WrongLogin = @{
    email = $MerchantEmail
    password = "WrongPassword"
}
$res = Send-Request -Method "POST" -Route "/auth/login" -Body $WrongLogin
Assert-Status -Scenario "1.4: Log In With Wrong Password" -ActualCode $res.StatusCode -ExpectedCode 401 -Data $res.Data

# Scenario 1.5: Forgot Password
$ForgotPayload = @{ email = $MerchantEmail }
$res = Send-Request -Method "POST" -Route "/auth/forgot-password" -Body $ForgotPayload
Assert-Status -Scenario "1.5: Request Password Reset Verification" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$ResetToken = $res.Data.data.debugToken

# Scenario 1.6: Reset Password
$ResetPayload = @{
    email = $MerchantEmail
    token = $ResetToken
    newPassword = "NewPassword@123"
}
$res = Send-Request -Method "POST" -Route "/auth/reset-password" -Body $ResetPayload
Assert-Status -Scenario "1.6: Reset Password With Token" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 1.7: Refresh Token
$RefreshPayload = @{ refreshToken = $MRefreshToken }
$res = Send-Request -Method "POST" -Route "/auth/refresh" -Body $RefreshPayload
Assert-Status -Scenario "1.7: Refresh Session Tokens" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$NewRefreshToken = $res.Data.data.refreshToken

# Scenario 1.8: Resend Verification
$ResendPayload = @{ email = $MerchantEmail }
$res = Send-Request -Method "POST" -Route "/auth/resend-verification" -Body $ResendPayload
Assert-Status -Scenario "1.8: Resend Email Verification" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 1.9: Logout
$LogoutPayload = @{ refreshToken = $NewRefreshToken }
$res = Send-Request -Method "POST" -Route "/auth/logout" -Body $LogoutPayload
Assert-Status -Scenario "1.9: Log Out Active Session" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Log In System Admin
$AdminLogin = @{
    email = "admin@foodloop.com"
    password = "Admin@123"
}
$res = Send-Request -Method "POST" -Route "/auth/login" -Body $AdminLogin
if ($res.StatusCode -eq 200) {
    $global:AdminToken = $res.Data.data.accessToken
    Write-Host "[INFO] Loaded System Admin token successfully." -ForegroundColor Green
}

# Log In / Register Customer
$CustomerEmail = "customer.ps${RandomVal}@example.com"
$CustomerReg = @{
    name = "Test Customer PS"
    email = $CustomerEmail
    password = "Password@123"
    role = "Customer"
}
$res = Send-Request -Method "POST" -Route "/auth/register" -Body $CustomerReg
$global:CustomerUserId = $res.Data.data.user.id
Write-Host "[INFO] Loaded Registered Customer User ID: $CustomerUserId" -ForegroundColor Green

$res = Send-Request -Method "POST" -Route "/auth/login" -Body @{ email = $CustomerEmail; password = "Password@123" }
if ($res.StatusCode -eq 200) {
    $global:CustomerToken = $res.Data.data.accessToken
    Write-Host "[INFO] Loaded Customer token successfully." -ForegroundColor Green
}

# Log In / Register Charity
$CharityEmail = "charity.ps${RandomVal}@example.com"
$CharityReg = @{
    name = "Test Charity PS"
    email = $CharityEmail
    password = "Password@123"
    role = "Charity"
    businessName = "Test Charity Org PS"
}
$res = Send-Request -Method "POST" -Route "/auth/register" -Body $CharityReg
$res = Send-Request -Method "POST" -Route "/auth/login" -Body @{ email = $CharityEmail; password = "Password@123" }
if ($res.StatusCode -eq 200) {
    $global:CharityToken = $res.Data.data.accessToken
    Write-Host "[INFO] Loaded Charity token successfully." -ForegroundColor Green
}

# ==============================================================================
# SECTION 2: USER PROFILE & ADDRESS MODULE (/users)
# ==============================================================================
Write-Host "`n--- Testing User Profiles & Addresses ---" -ForegroundColor Yellow

# Scenario 2.1: Get Profile
$res = Send-Request -Method "GET" -Route "/users/me" -Token $CustomerToken
Assert-Status -Scenario "2.1: Get Current Customer Profile" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 2.2: Get Profile 401 Unauthorized
$res = Send-Request -Method "GET" -Route "/users/me"
Assert-Status -Scenario "2.2: Get Profile Without Bearer Token" -ActualCode $res.StatusCode -ExpectedCode 401 -Data $res.Data

# Scenario 2.3: Update Profile
$res = Send-Request -Method "PATCH" -Route "/users/me" -Body @{ fullName = "Updated Customer PS"; language = "ar"; phoneNumber = "01012345678" } -Token $CustomerToken
Assert-Status -Scenario "2.3: Update Customer Profile Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 2.4: Create Address
$AddrPayload = @{
    addressType = "Home"
    city = "Cairo"
    district = "Maadi"
    street = "El-Nasr St"
    buildingNo = "15"
    floor = "2"
    apartmentNo = "6"
    latitude = 30.0444
    longitude = 31.2357
    isDefault = $true
}
$res = Send-Request -Method "POST" -Route "/users/me/addresses" -Body $AddrPayload -Token $CustomerToken
Assert-Status -Scenario "2.4: Add New Delivery Address" -ActualCode $res.StatusCode -ExpectedCode 201 -Data $res.Data
$AddressId = $res.Data.data.id

# Scenario 2.5: Get Addresses
$res = Send-Request -Method "GET" -Route "/users/me/addresses" -Token $CustomerToken
Assert-Status -Scenario "2.5: List All Saved User Addresses" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 2.6: Update Address
$res = Send-Request -Method "PATCH" -Route "/users/me/addresses/$AddressId" -Body @{ city = "Cairo"; district = "Zamalek"; street = "26 July St" } -Token $CustomerToken
Assert-Status -Scenario "2.6: Update Delivery Address Zamalek" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 2.7: Update Non-existent Address
$res = Send-Request -Method "PATCH" -Route "/users/me/addresses/00000000-0000-0000-0000-000000000000" -Body @{ city = "Cairo" } -Token $CustomerToken
Assert-Status -Scenario "2.7: Update Non-existent Address ID" -ActualCode $res.StatusCode -ExpectedCode 404 -Data $res.Data

# Scenario 2.8: Delete Address
$res = Send-Request -Method "DELETE" -Route "/users/me/addresses/$AddressId" -Token $CustomerToken
Assert-Status -Scenario "2.8: Remove Saved Address" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data

# Scenario 2.9: Delete Non-existent Address
$res = Send-Request -Method "DELETE" -Route "/users/me/addresses/00000000-0000-0000-0000-000000000000" -Token $CustomerToken
Assert-Status -Scenario "2.9: Remove Non-existent Address ID" -ActualCode $res.StatusCode -ExpectedCode 404 -Data $res.Data

# Scenario 2.10: Open Ticket
$res = Send-Request -Method "POST" -Route "/users/me/tickets" -Body @{ category = "Account"; message = "Issue with profile updates"; priority = "Low" } -Token $CustomerToken
Assert-Status -Scenario "2.10: Open Support Ticket via Users Controller" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 2.11: Update Preferences
$res = Send-Request -Method "PATCH" -Route "/users/me/preferences" -Body @{ orderUpdatesEnabled = $true; marketingNotificationsEnabled = $true } -Token $CustomerToken
Assert-Status -Scenario "2.11: Update Notification Settings" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 3: STORES & ORGANIZATIONS (/stores & /charities)
# ==============================================================================
Write-Host "`n--- Testing Stores & Organizations ---" -ForegroundColor Yellow

# Scenario 3.1: Get My Store details
$res = Send-Request -Method "GET" -Route "/stores/me" -Token $MerchantToken
Assert-Status -Scenario "3.1: Retrieve Merchant Store Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$OrganizationId = $res.Data.data.id

# Scenario 3.2: Update Store Profile Location
$LocPayload = @{
    latitude = 30.0450
    longitude = 31.2360
    governorate = "Cairo"
    city = "Cairo"
    neighborhood = "Maadi"
    street = "Street 9"
    buildingNo = "24"
}
$res = Send-Request -Method "PATCH" -Route "/stores/me/location" -Body $LocPayload -Token $MerchantToken
Assert-Status -Scenario "3.2: Update Store Location Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 3.3: Submit Store Documents (Using standard HttpClient multipart upload or fallback to avoid complex CLI binary uploads in PS)
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"
$multipartBody = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"Email`"$LF",
    $MerchantEmail,
    "--$boundary",
    "Content-Disposition: form-data; name=`"Type`"$LF",
    "CommercialRegistration",
    "--$boundary",
    "Content-Disposition: form-data; name=`"File`"; filename=`"mock_cr.pdf`"",
    "Content-Type: application/pdf$LF",
    "PDF-MOCK-CONTENT-PS",
    "--$boundary--"
) -join $LF

$res = Send-Request -Method "POST" -Route "/stores/me/documents" -Body $multipartBody -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "3.3: Upload Store Verification Documents" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Upload the rest of merchant documents
foreach ($type in @("TaxIdCertificate", "StoreFacilityPhoto")) {
    $multipartBody = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"Email`"$LF",
        $MerchantEmail,
        "--$boundary",
        "Content-Disposition: form-data; name=`"Type`"$LF",
        $type,
        "--$boundary",
        "Content-Disposition: form-data; name=`"File`"; filename=`"mock_cr.pdf`"",
        "Content-Type: application/pdf$LF",
        "PDF-MOCK-CONTENT-PS",
        "--$boundary--"
    ) -join $LF
    $null = Send-Request -Method "POST" -Route "/stores/me/documents" -Body $multipartBody -ContentType "multipart/form-data; boundary=$boundary"
}

# Scenario 3.4: Submit Charity Documents
$multipartBodyCharity = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"Email`"$LF",
    $CharityEmail,
    "--$boundary",
    "Content-Disposition: form-data; name=`"Type`"$LF",
    "AssociationCertificate",
    "--$boundary",
    "Content-Disposition: form-data; name=`"File`"; filename=`"mock_charity_cr.pdf`"",
    "Content-Type: application/pdf$LF",
    "PDF-CHARITY-CR-CONTENT",
    "--$boundary--"
) -join $LF
$res = Send-Request -Method "POST" -Route "/charities/me/documents" -Body $multipartBodyCharity -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "3.4: Upload Charity Association Certificate" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Upload the rest of charity documents
foreach ($type in @("CharityBylaws", "BoardOfDirectorsList")) {
    $multipartBodyCharity = (
        "--$boundary",
        "Content-Disposition: form-data; name=`"Email`"$LF",
        $CharityEmail,
        "--$boundary",
        "Content-Disposition: form-data; name=`"Type`"$LF",
        $type,
        "--$boundary",
        "Content-Disposition: form-data; name=`"File`"; filename=`"mock_charity_cr.pdf`"",
        "Content-Type: application/pdf$LF",
        "PDF-CHARITY-CR-CONTENT",
        "--$boundary--"
    ) -join $LF
    $null = Send-Request -Method "POST" -Route "/charities/me/documents" -Body $multipartBodyCharity -ContentType "multipart/form-data; boundary=$boundary"
}

# Scenario 3.5: Update Store Name and Category Profile
$multipartStoreProfile = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"Name`"$LF",
    "Spinneys Supermarket Updated PS",
    "--$boundary",
    "Content-Disposition: form-data; name=`"BusinessCategory`"$LF",
    "Supermarket",
    "--$boundary--"
) -join $LF
$res = Send-Request -Method "PATCH" -Route "/stores/me" -Body $multipartStoreProfile -Token $MerchantToken -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "3.5: Update Store Name and Category Profile" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 3.6: Get Received Merchant Orders
$res = Send-Request -Method "GET" -Route "/stores/me/orders" -Token $MerchantToken
Assert-Status -Scenario "3.6: Retrieve Merchant Received Orders List" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 4: MERCHANT INVENTORY (/stores/me/products)
# ==============================================================================
Write-Host "`n--- Testing Inventory Management ---" -ForegroundColor Yellow

$res = Send-Request -Method "GET" -Route "/categories"
$CategoryId = $res.Data.data[0].id
Write-Host "[INFO] Loaded Dynamic Category ID: $CategoryId" -ForegroundColor Green

# Scenario 4.1: Add Product
$PrdPayload = @{
    categoryId = $CategoryId
    title = "Artisan Sourdough Loaf PS"
    titleAr = "خبز ساوردو يدوي"
    description = "Crispy sourdough bread."
    descriptionAr = "خبز مخبوز طازج."
    originalPrice = 15.00
    discountedPrice = 7.50
    quantityAvailable = 10
    expirationDate = "2026-08-15"
}
$res = Send-Request -Method "POST" -Route "/stores/me/products" -Body $PrdPayload -Token $MerchantToken
Assert-Status -Scenario "4.1: Add Product to Store Inventory" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$ProductId = $res.Data.data.id

# Scenario 4.2: Invalid Discount Price
$BadPrdPayload = @{
    categoryId = $CategoryId
    title = "Invalid Price"
    originalPrice = 10.00
    discountedPrice = 50.00
    quantityAvailable = 5
    expirationDate = "2026-08-15"
}
$res = Send-Request -Method "POST" -Route "/stores/me/products" -Body $BadPrdPayload -Token $MerchantToken
Assert-Status -Scenario "4.2: Add Product With Invalid Discount Price" -ActualCode $res.StatusCode -ExpectedCode 400 -Data $res.Data

# Scenario 4.3: Get Single Product details
$res = Send-Request -Method "GET" -Route "/stores/me/products/$ProductId" -Token $MerchantToken
Assert-Status -Scenario "4.3: Get Single Product Inventory Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 4.4: Get Non-existent Product details
$res = Send-Request -Method "GET" -Route "/stores/me/products/00000000-0000-0000-0000-000000000000" -Token $MerchantToken
Assert-Status -Scenario "4.4: Get Non-existent Product Details" -ActualCode $res.StatusCode -ExpectedCode 404 -Data $res.Data

# Scenario 4.5: Update Product pricing & stock
$multipartPrdUpdate = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"DiscountedPrice`"$LF",
    "6.00",
    "--$boundary",
    "Content-Disposition: form-data; name=`"QuantityAvailable`"$LF",
    "8",
    "--$boundary",
    "Content-Disposition: form-data; name=`"Status`"$LF",
    "Active",
    "--$boundary--"
) -join $LF
$res = Send-Request -Method "PATCH" -Route "/stores/me/products/$ProductId" -Body $multipartPrdUpdate -Token $MerchantToken -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "4.5: Update Product Pricing & Stock Levels" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 4.6: List Active Listings
$res = Send-Request -Method "GET" -Route "/stores/me/products?status=Active" -Token $MerchantToken
Assert-Status -Scenario "4.6: List Active Merchant Inventory Listings" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 4.7: Upload Product Display Image
$multipartPrdImage = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"File`"; filename=`"mock_img.png`"",
    "Content-Type: image/png$LF",
    "PNG-IMAGE-PAYLOAD-PS",
    "--$boundary--"
) -join $LF
$res = Send-Request -Method "POST" -Route "/stores/me/products/$ProductId/images" -Body $multipartPrdImage -Token $MerchantToken -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "4.7: Upload Product Display Image" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$ImageId = $res.Data.data.images[0].id

# Scenario 4.8: Remove Product Display Image
if ($ImageId) {
    $res = Send-Request -Method "DELETE" -Route "/stores/me/products/$ProductId/images/$ImageId" -Token $MerchantToken
    Assert-Status -Scenario "4.8: Remove Product Display Image" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
}

# Scenario 4.9: Bulk Upload CSV
$csvData = "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname`nBulk Artisan Bread PS,20.00,10.00,15,2026-08-25,Bakery"
$multipartCsv = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"File`"; filename=`"bulk_prd.csv`"",
    "Content-Type: text/csv$LF",
    $csvData,
    "--$boundary--"
) -join $LF
$res = Send-Request -Method "POST" -Route "/stores/me/products/bulk" -Body $multipartCsv -Token $MerchantToken -ContentType "multipart/form-data; boundary=$boundary"
Assert-Status -Scenario "4.9: Bulk Upload Inventory via CSV" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 4.10: Soft Delete Inventory
$res = Send-Request -Method "DELETE" -Route "/stores/me/products/$ProductId" -Token $MerchantToken
Assert-Status -Scenario "4.10: Soft Delete Inventory Listing" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Re-add product for checkout orders testing
$res = Send-Request -Method "POST" -Route "/stores/me/products" -Body $PrdPayload -Token $MerchantToken
$ProductId = $res.Data.data.id

# ==============================================================================
# SECTION 5: MARKETPLACE (/marketplace)
# ==============================================================================
Write-Host "`n--- Testing Public Marketplace ---" -ForegroundColor Yellow

# Scenario 5.1: Get Nearby Products
$res = Send-Request -Method "GET" -Route "/marketplace/products?latitude=30.0450&longitude=31.2360&maxDistance=10" -Token $CustomerToken
Assert-Status -Scenario "5.1: Get Nearby Marketplace Products" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 5.2: Filter & Search Products
$res = Send-Request -Method "GET" -Route "/marketplace/products?categoryId=$CategoryId&minPrice=1&maxPrice=100&sortBy=price" -Token $CustomerToken
Assert-Status -Scenario "5.2: Search Products with Category & Sorting" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 6: ORDERS & CHECKOUT (/orders)
# ==============================================================================
Write-Host "`n--- Testing Orders & Checkout ---" -ForegroundColor Yellow

if ($AdminToken) {
    $null = Send-Request -Method "PATCH" -Route "/admin/stores/$OrganizationId/verify" -Body @{ action = "Approved"; note = "Verified via PS script" } -Token $AdminToken
}

# Scenario 6.1: Place Order
$OrderPayload = @{
    items = @(
        @{ productId = $ProductId; quantity = 2 }
    )
}
$res = Send-Request -Method "POST" -Route "/orders" -Body $OrderPayload -Token $CustomerToken
Assert-Status -Scenario "6.1: Place Checkout Order" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$OrderId = $res.Data.data.id

# Scenario 6.2: Exceeding Stock
$OverStock = @{
    items = @(
        @{ productId = $ProductId; quantity = 100 }
    )
}
$res = Send-Request -Method "POST" -Route "/orders" -Body $OverStock -Token $CustomerToken
Assert-Status -Scenario "6.2: Place Order Exceeding Stock Level" -ActualCode $res.StatusCode -ExpectedCode 400 -Data $res.Data

# Scenario 6.3: Get Order Details
$res = Send-Request -Method "GET" -Route "/orders/$OrderId" -Token $CustomerToken
Assert-Status -Scenario "6.3: Retrieve Checkout Order Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 6.4: Customer order history
$res = Send-Request -Method "GET" -Route "/orders" -Token $CustomerToken
Assert-Status -Scenario "6.4: List Customer Order History" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 6.5: Transition to Completed
$res = Send-Request -Method "PATCH" -Route "/stores/me/orders/$OrderId/status" -Body @{ status = "Completed" } -Token $MerchantToken
Assert-Status -Scenario "6.5: Transition Order to Completed" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 7: STORES REVIEWS (/reviews)
# ==============================================================================
Write-Host "`n--- Testing Store Reviews ---" -ForegroundColor Yellow

# Scenario 7.1: Leave Review
$ReviewPayload = @{
    orderId = $OrderId
    organizationId = $OrganizationId
    rating = 5
    comment = "Exceptional service and sourdough! PS"
}
$res = Send-Request -Method "POST" -Route "/reviews" -Body $ReviewPayload -Token $CustomerToken
Assert-Status -Scenario "7.1: Post Order Rating Review" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$ReviewId = $res.Data.data.id

# Scenario 7.2: Out Of Bounds rating
$BadReview = @{
    orderId = $OrderId
    organizationId = $OrganizationId
    rating = 10
    comment = "Too high rating"
}
$res = Send-Request -Method "POST" -Route "/reviews" -Body $BadReview -Token $CustomerToken
Assert-Status -Scenario "7.2: Post Review Rating Value Out Of Bounds" -ActualCode $res.StatusCode -ExpectedCode 400 -Data $res.Data

# Scenario 7.3: Get Store Reviews
$res = Send-Request -Method "GET" -Route "/stores/$OrganizationId/reviews?pageNumber=1&pageSize=10"
Assert-Status -Scenario "7.3: List Store Reviews" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 8: NOTIFICATIONS HUB (/notifications)
# ==============================================================================
Write-Host "`n--- Testing Notifications ---" -ForegroundColor Yellow

# Scenario 8.1: Get notifications
$res = Send-Request -Method "GET" -Route "/notifications" -Token $CustomerToken
Assert-Status -Scenario "8.1: Get Customer Notifications Feed" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$NotificationId = $res.Data.data[0].id

# Scenario 8.2: Mark Notification Read
if ($NotificationId) {
    $res = Send-Request -Method "PATCH" -Route "/notifications/$NotificationId/read" -Token $CustomerToken
    Assert-Status -Scenario "8.2: Mark Notification Feed Alert As Read" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data
}

# Scenario 8.3: Mark All Read
$res = Send-Request -Method "PATCH" -Route "/notifications/read-all" -Token $CustomerToken
Assert-Status -Scenario "8.3: Mark All User Notifications As Read" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data

# ==============================================================================
# SECTION 9: CUSTOMER SUPPORT MODULE (/support-tickets)
# ==============================================================================
Write-Host "`n--- Testing Customer Support Tickets ---" -ForegroundColor Yellow

# Scenario 9.1: Create Ticket
$TicketPayload = @{
    category = "Refund"
    message = "Refund delay query PS"
    priority = "High"
}
$res = Send-Request -Method "POST" -Route "/support-tickets" -Body $TicketPayload -Token $CustomerToken
Assert-Status -Scenario "9.1: Create Customer Support Ticket" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
$SupportTicketId = $res.Data.data.id

# Scenario 9.2: Reply to Ticket
$res = Send-Request -Method "POST" -Route "/support-tickets/$SupportTicketId/reply" -Body @{ message = "Please expedite this issue PS." } -Token $CustomerToken
Assert-Status -Scenario "9.2: Post Customer Support Message Reply" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 9.3: List tickets
$res = Send-Request -Method "GET" -Route "/support-tickets" -Token $CustomerToken
Assert-Status -Scenario "9.3: List Customer Support Tickets" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# Scenario 9.4: Ticket Details
$res = Send-Request -Method "GET" -Route "/support-tickets/$SupportTicketId" -Token $CustomerToken
Assert-Status -Scenario "9.4: Get Support Ticket Conversation Details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

# ==============================================================================
# SECTION 10: ADMIN OPERATIONS (/admin)
# ==============================================================================
Write-Host "`n--- Testing Admin Operations ---" -ForegroundColor Yellow

# Scenario 10.0: Forbidden check
$res = Send-Request -Method "GET" -Route "/admin/analytics/summary" -Token $CustomerToken
Assert-Status -Scenario "10.0: Forbidden Access check for Customer on Admin Route" -ActualCode $res.StatusCode -ExpectedCode 403 -Data $res.Data

if ($AdminToken) {
    # Scenario 10.1: Pending onboarding
    $res = Send-Request -Method "GET" -Route "/admin/stores/pending" -Token $AdminToken
    Assert-Status -Scenario "10.1: Retrieve Pending Onboarding Queue" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Find store/charity organization IDs from response
    $orgList = $res.Data.data
    $MOrgId = ($orgList | Where-Object { $_.ownerEmail -eq $MerchantEmail } | Select-Object -First 1).id
    $COrgId = ($orgList | Where-Object { $_.ownerEmail -eq $CharityEmail } | Select-Object -First 1).id

    if ($MOrgId) {
        # Scenario 10.2: Get store info for review
        $res = Send-Request -Method "GET" -Route "/admin/stores/$MOrgId" -Token $AdminToken
        Assert-Status -Scenario "10.2: Get Store Info For Admin Review" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

        # Scenario 10.3: Approve organization onboarding
        $res = Send-Request -Method "PATCH" -Route "/admin/stores/$MOrgId/verify" -Body @{ action = "Approved"; note = "Approved via script" } -Token $AdminToken
        Assert-Status -Scenario "10.3: Approve Organization Store Onboarding" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
    }

    if ($COrgId) {
        # Scenario 10.3.1: Approve charity onboarding
        $res = Send-Request -Method "PATCH" -Route "/admin/charities/$COrgId/verify" -Body @{ action = "Approved"; note = "Approved charity via script" } -Token $AdminToken
        Assert-Status -Scenario "10.3.1: Approve Organization Charity Onboarding" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
    }

    # Scenario 10.4: List users
    $res = Send-Request -Method "GET" -Route "/admin/users?role=Merchant&status=Active" -Token $AdminToken
    Assert-Status -Scenario "10.4: Admin List Registered System Users" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.5: Ban user
    $res = Send-Request -Method "PATCH" -Route "/admin/users/$CustomerUserId/status" -Body @{ status = "Banned" } -Token $AdminToken
    Assert-Status -Scenario "10.5: Ban User Account Profile" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.6: User activity log
    $res = Send-Request -Method "GET" -Route "/admin/users/$CustomerUserId/activity-log" -Token $AdminToken
    Assert-Status -Scenario "10.6: Retrieve User Account Activity Log" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.7: Store activity log
    $res = Send-Request -Method "GET" -Route "/admin/stores/$OrganizationId/activity-log" -Token $AdminToken
    Assert-Status -Scenario "10.7: Retrieve Store Activity Log" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.7.1: Charity activity log
    if ($COrgId) {
        $res = Send-Request -Method "GET" -Route "/admin/charities/$COrgId/activity-log" -Token $AdminToken
        Assert-Status -Scenario "10.7.1: Retrieve Charity Activity Log" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data
    }

    # Scenario 10.8: Analytics summary
    $res = Send-Request -Method "GET" -Route "/admin/analytics/summary" -Token $AdminToken
    Assert-Status -Scenario "10.8: Retrieve Analytics summary details" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.9: List verified stores
    $res = Send-Request -Method "GET" -Route "/admin/stores?status=Verified" -Token $AdminToken
    Assert-Status -Scenario "10.9: Admin List Verified Stores" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.10: List charities
    $res = Send-Request -Method "GET" -Route "/admin/charities" -Token $AdminToken
    Assert-Status -Scenario "10.10: Admin List Charities" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.11: List reviews
    $res = Send-Request -Method "GET" -Route "/admin/reviews?rating=5" -Token $AdminToken
    Assert-Status -Scenario "10.11: Admin List Customer Reviews" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.12: List inventory moderation
    $res = Send-Request -Method "GET" -Route "/admin/products?status=Active" -Token $AdminToken
    Assert-Status -Scenario "10.12: Admin List Inventory Listings" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.13: List pending AI
    $res = Send-Request -Method "GET" -Route "/admin/products/pending-ai?confidenceThreshold=0.9" -Token $AdminToken
    Assert-Status -Scenario "10.13: List Low AI Confidence Products Queue" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.14: Moderate approve
    $res = Send-Request -Method "PATCH" -Route "/admin/products/$ProductId/approve" -Token $AdminToken
    Assert-Status -Scenario "10.14: Approve Moderated Product Listing" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.15: Moderate request changes
    $res = Send-Request -Method "PATCH" -Route "/admin/products/$ProductId/request-changes" -Body @{ note = "Update price details" } -Token $AdminToken
    Assert-Status -Scenario "10.15: Request Changes on Product Listing" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.16: Moderate reject
    $res = Send-Request -Method "PATCH" -Route "/admin/products/$ProductId/reject" -Body @{ note = "Inappropriate pricing structure" } -Token $AdminToken
    Assert-Status -Scenario "10.16: Reject Moderated Product Listing" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.17: Moderate Review (Delete review)
    if ($ReviewId) {
        $res = Send-Request -Method "DELETE" -Route "/admin/reviews/$ReviewId" -Token $AdminToken
        Assert-Status -Scenario "10.17: Moderate and Delete Inappropriate Customer Review" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data
    }

    # Scenario 10.18: List tickets
    $res = Send-Request -Method "GET" -Route "/admin/support-tickets?status=Open" -Token $AdminToken
    Assert-Status -Scenario "10.18: Admin List Support Tickets" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.19: Get Ticket details
    $res = Send-Request -Method "GET" -Route "/admin/support-tickets/$SupportTicketId" -Token $AdminToken
    Assert-Status -Scenario "10.19: Admin Retrieve Support Ticket Conversation History" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.20: Reply to ticket
    # Wrapped string value for direct BodyJson
    $res = Send-Request -Method "POST" -Route "/admin/support-tickets/$SupportTicketId/reply" -Body "`"Resolving the problem now.`"" -Token $AdminToken
    Assert-Status -Scenario "10.20: Admin Post Reply Message on Support Ticket" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.21: Close ticket
    $res = Send-Request -Method "PATCH" -Route "/admin/support-tickets/$SupportTicketId/close" -Token $AdminToken
    Assert-Status -Scenario "10.21: Resolve and Close Support Ticket" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data

    # Scenario 10.22: Delete product
    $res = Send-Request -Method "DELETE" -Route "/admin/products/$ProductId" -Token $AdminToken
    Assert-Status -Scenario "10.22: Soft Delete Product Listing via Admin" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data

    # Scenario 10.23: Admin direct user CRUD check
    $res = Send-Request -Method "POST" -Route "/users" -Body @{ fullName = "Admin Direct PS"; email = "admdirectps${RandomVal}@example.com"; password = "Password@123"; role = "Customer" } -Token $AdminToken
    Assert-Status -Scenario "10.23: Admin Create User Account Directly" -ActualCode $res.StatusCode -ExpectedCode 201 -Data $res.Data
    $AdmUserId = $res.Data.data.id

    # Scenario 10.24: Admin retrieve user directly
    $res = Send-Request -Method "GET" -Route "/users/$AdmUserId" -Token $AdminToken
    Assert-Status -Scenario "10.24: Admin Retrieve User Account by ID" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.25: Admin update user directly
    $res = Send-Request -Method "PATCH" -Route "/users/$AdmUserId" -Body @{ fullName = "Admin Direct Updated" } -Token $AdminToken
    Assert-Status -Scenario "10.25: Admin Update User Account Directly" -ActualCode $res.StatusCode -ExpectedCode 200 -Data $res.Data

    # Scenario 10.26: Admin delete user directly
    $res = Send-Request -Method "DELETE" -Route "/users/$AdmUserId" -Token $AdminToken
    Assert-Status -Scenario "10.26: Admin Delete User Account Directly" -ActualCode $res.StatusCode -ExpectedCode 204 -Data $res.Data
}

# ==============================================================================
# TEST RUN SUMMARY
# ==============================================================================
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "FoodLoop Automated Integration Test Suite Completed" -ForegroundColor Cyan
Write-Host "TOTAL PASSED ASSERTIONS: $PassCount" -ForegroundColor Green
Write-Host "TOTAL FAILED ASSERTIONS: $FailCount" -ForegroundColor (If ($FailCount -eq 0) { "Green" } Else { "Red" })
Write-Host "==========================================================" -ForegroundColor Cyan

if ($FailCount -eq 0) {
    exit 0
} else {
    exit 1
}
