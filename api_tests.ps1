# FoodLoop API Automated Test Suite (PowerShell Edition)
# This script executes happy paths, validation failures, not found, unauthorized, and state transition test scenarios.

$ErrorActionPreference = "Stop"

# --- Configuration & Environment Setup ---
$BaseUrl = "https://localhost:7001" # Default local URL, can also be "https://foodloop.runasp.net"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "FoodLoop Automated Integration Test Suite Starting" -ForegroundColor Cyan
Write-Host "Target Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Global State Variables (to pass IDs between steps)
$AdminToken = ""
$MerchantToken = ""
$CustomerToken = ""
$OrganizationId = ""
$ProductId = ""
$OrderId = ""
$SupportTicketId = ""
$NotificationId = ""

# Helper function to execute Web Requests and return response + status code
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
    } elseif ($Body -and $ContentType -like "multipart/form-data*") {
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
            $Json = $Response.Content | ConvertFrom-Json
        }

        return [PSCustomObject]@{
            StatusCode = $Response.StatusCode
            Data = $Json
            Success = $true
        }
    }
    catch {
        $ErrorResponse = $_.Exception.Response
        $StatusCode = 0
        $Json = $null
        if ($ErrorResponse) {
            $StatusCode = [int]$ErrorResponse.StatusCode
            $StreamReader = [System.IO.StreamReader]::new($ErrorResponse.GetResponseStream())
            $Content = $StreamReader.ReadToEnd()
            if ($Content) {
                try { $Json = $Content | ConvertFrom-Json } catch {}
            }
        }
        return [PSCustomObject]@{
            StatusCode = $StatusCode
            Data = $Json
            Success = $false
        }
    }
}

# Helper function to assert statuses
function Assert-Status {
    param (
        [string]$Scenario,
        [int]$ActualCode,
        [int]$ExpectedCode
    )
    if ($ActualCode -eq $ExpectedCode) {
        Write-Host "[PASS] $Scenario (HTTP $ActualCode)" -ForegroundColor Green
        return $true
    } else {
        Write-Host "[FAIL] $Scenario (Expected: $ExpectedCode, Got: $ActualCode)" -ForegroundColor Red
        return $false
    }
}

# ==============================================================================
# SECTION 1: AUTHENTICATION MODULE (/auth)
# ==============================================================================
Write-Host "`n--- Testing Authentication Endpoints ---" -ForegroundColor Yellow

# Scenario 1.1: Register (Happy Path)
$MerchantEmail = "merchant.test" + (Get-Random) + "@example.com"
$RegisterPayload = @{
    name = "Test Merchant"
    email = $MerchantEmail
    password = "Password@123"
    role = "Merchant"
    businessName = "Test Organic Shop"
}
$Res = Send-Request -Method "Post" -Route "/auth/register" -Body $RegisterPayload
Assert-Status -Scenario "1.1: Register New Merchant Account" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 1.2: Register (Bad Request / Validation Failure)
$BadRegisterPayload = @{
    name = "" # Empty name fails validation
    email = "invalidemail"
    password = "123"
    role = "InvalidRole"
}
$Res = Send-Request -Method "Post" -Route "/auth/register" -Body $BadRegisterPayload
Assert-Status -Scenario "1.2: Register With Bad Validation Fields" -ActualCode $Res.StatusCode -ExpectedCode 400

# Scenario 1.3: Login (Happy Path)
$LoginPayload = @{
    email = $MerchantEmail
    password = "Password@123"
}
$Res = Send-Request -Method "Post" -Route "/auth/login" -Body $LoginPayload
if (Assert-Status -Scenario "1.3: Log In Merchant Account" -ActualCode $Res.StatusCode -ExpectedCode 200) {
    $MerchantToken = $Res.Data.data.accessToken
}

# Scenario 1.4: Login (Unauthorized Credentials)
$BadLoginPayload = @{
    email = $MerchantEmail
    password = "WrongPassword"
}
$Res = Send-Request -Method "Post" -Route "/auth/login" -Body $BadLoginPayload
Assert-Status -Scenario "1.4: Log In With Wrong Password" -ActualCode $Res.StatusCode -ExpectedCode 400

# Scenario 1.5: Forgot Password (Happy Path)
$ForgotPayload = @{ email = $MerchantEmail }
$Res = Send-Request -Method "Post" -Route "/auth/forgot-password" -Body $ForgotPayload
Assert-Status -Scenario "1.5: Request Password Reset Verification" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 1.6: Log In System Admin
$AdminLoginPayload = @{
    email = "admin@foodloop.com"
    password = "Password@123"
}
$Res = Send-Request -Method "Post" -Route "/auth/login" -Body $AdminLoginPayload
if ($Res.StatusCode -eq 200) {
    $AdminToken = $Res.Data.data.accessToken
    Write-Host "[INFO] Loaded System Admin token successfully." -ForegroundColor Cyan
} else {
    Write-Host "[WARNING] System Admin login failed. Admin-locked tests will fall back." -ForegroundColor Yellow
}

# Log In Customer
$CustomerEmail = "customer.test" + (Get-Random) + "@example.com"
$CustomerRegPayload = @{
    name = "Test Customer"
    email = $CustomerEmail
    password = "Password@123"
    role = "Customer"
}
$Res = Send-Request -Method "Post" -Route "/auth/register" -Body $CustomerRegPayload
$Res = Send-Request -Method "Post" -Route "/auth/login" -Body @{ email = $CustomerEmail; password = "Password@123" }
if ($Res.StatusCode -eq 200) {
    $CustomerToken = $Res.Data.data.accessToken
    Write-Host "[INFO] Loaded Customer token successfully." -ForegroundColor Cyan
}

# ==============================================================================
# SECTION 2: USER PROFILE & ADDRESS MODULE (/users)
# ==============================================================================
Write-Host "`n--- Testing User Profiles & Addresses ---" -ForegroundColor Yellow

# Scenario 2.1: Get My Profile (Happy Path)
$Res = Send-Request -Method "Get" -Route "/users/me" -Token $CustomerToken
Assert-Status -Scenario "2.1: Get Current Customer Profile" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 2.2: Get My Profile (Unauthorized / Missing Token)
$Res = Send-Request -Method "Get" -Route "/users/me"
Assert-Status -Scenario "2.2: Get Profile Without Bearer Token" -ActualCode $Res.StatusCode -ExpectedCode 401

# Scenario 2.3: Update Profile (Happy Path)
$UpdateProfilePayload = @{
    fullName = "Updated Customer Name"
    language = "ar"
}
$Res = Send-Request -Method "Patch" -Route "/users/me" -Body $UpdateProfilePayload -Token $CustomerToken
Assert-Status -Scenario "2.3: Update Customer Profile Details" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 2.4: Create Address (Happy Path)
$AddressPayload = @{
    label = "Home"
    city = "Cairo"
    district = "Maadi"
    street = "El-Nasr St"
    buildingNo = "15"
    floor = 2
    apartmentNo = "6"
    latitude = 30.0444
    longitude = 31.2357
    isDefault = $true
}
$Res = Send-Request -Method "Post" -Route "/users/me/addresses" -Body $AddressPayload -Token $CustomerToken
Assert-Status -Scenario "2.4: Add New Delivery Address" -ActualCode $Res.StatusCode -ExpectedCode 200
$AddressId = $Res.Data.data.id

# Scenario 2.5: Get Addresses (Happy Path)
$Res = Send-Request -Method "Get" -Route "/users/me/addresses" -Token $CustomerToken
Assert-Status -Scenario "2.5: List All Saved User Addresses" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 2.6: Delete Address (Happy Path)
$Res = Send-Request -Method "Delete" -Route "/users/me/addresses/$AddressId" -Token $CustomerToken
Assert-Status -Scenario "2.6: Remove Saved Address" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 2.7: Delete Non-existent Address (Not Found)
$Res = Send-Request -Method "Delete" -Route "/users/me/addresses/00000000-0000-0000-0000-000000000000" -Token $CustomerToken
Assert-Status -Scenario "2.7: Remove Non-existent Address ID" -ActualCode $Res.StatusCode -ExpectedCode 404

# ==============================================================================
# SECTION 3: STORES & ORGANIZATIONS (/stores)
# ==============================================================================
Write-Host "`n--- Testing Stores & Organizations ---" -ForegroundColor Yellow

# Scenario 3.1: Get My Store Profile
$Res = Send-Request -Method "Get" -Route "/stores/me" -Token $MerchantToken
Assert-Status -Scenario "3.1: Retrieve Merchant Store Details" -ActualCode $Res.StatusCode -ExpectedCode 200
$OrganizationId = $Res.Data.data.id

# Scenario 3.2: Update Store Profile Location (Happy Path)
$LocPayload = @{
    latitude = 30.0450
    longitude = 31.2360
    city = "Cairo"
    neighborhood = "Maadi"
    street = "Street 9"
    buildingNo = "24"
}
$Res = Send-Request -Method "Patch" -Route "/stores/me/location" -Body $LocPayload -Token $MerchantToken
Assert-Status -Scenario "3.2: Update Store Location Details" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 3.3: Submit Store Documents (Mock multipart upload)
# For testing convenience, we submit the text representation of form keys.
$Boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"
$MultipartBody = "--$Boundary$LF" +
                 "Content-Disposition: form-data; name=`"Email``"$LF$LF" +
                 "$MerchantEmail$LF" +
                 "--$Boundary$LF" +
                 "Content-Disposition: form-data; name=`"Type``"$LF$LF" +
                 "CommercialRegistration$LF" +
                 "--$Boundary$LF" +
                 "Content-Disposition: form-data; name=`"File``; filename=`"mock_cr.pdf``"$LF" +
                 "Content-Type: application/pdf$LF$LF" +
                 "PDF-MOCK-CONTENT-GOES-HERE$LF" +
                 "--$Boundary--$LF"

$Res = Send-Request -Method "Post" -Route "/stores/me/documents" -Body $MultipartBody -Token $MerchantToken -ContentType "multipart/form-data; boundary=$Boundary"
Assert-Status -Scenario "3.3: Upload Store Verification Documents" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 4: MERCHANT INVENTORY (/stores/me/products)
# ==============================================================================
Write-Host "`n--- Testing Inventory Management ---" -ForegroundColor Yellow

# Fetch Bakery Category ID (needed to create a product)
$CategoryRes = Send-Request -Method "Get" -Route "/marketplace/products" -Token $CustomerToken
$CategoryId = "e4fa0739-b96b-4aea-9c07-45bb63de2058" # Default seeded Bakery category

# Scenario 4.1: Add Product (Happy Path)
$PrdPayload = @{
    categoryId = $CategoryId
    title = "Artisan Sourdough Loaf"
    titleAr = "خبز ساوردو يدوي"
    description = "Crispy sourdough bread baked fresh."
    descriptionAr = "خبز مخبوز طازج مقرمش."
    originalPrice = 15.00
    discountedPrice = 7.50
    quantityAvailable = 10
    expirationDate = "2026-08-15"
}
$Res = Send-Request -Method "Post" -Route "/stores/me/products" -Body $PrdPayload -Token $MerchantToken
Assert-Status -Scenario "4.1: Add Product to Store Inventory" -ActualCode $Res.StatusCode -ExpectedCode 200
$ProductId = $Res.Data.data.id

# Scenario 4.2: Add Product (Validation Fail - Expired Date)
$BadPrdPayload = @{
    categoryId = $CategoryId
    title = "Expired Item"
    originalPrice = 10.00
    discountedPrice = 5.00
    quantityAvailable = 5
    expirationDate = "2020-01-01" # Past date fails model validation
}
$Res = Send-Request -Method "Post" -Route "/stores/me/products" -Body $BadPrdPayload -Token $MerchantToken
Assert-Status -Scenario "4.2: Add Product With Expired Date" -ActualCode $Res.StatusCode -ExpectedCode 400

# Scenario 4.3: Get Single Product details
$Res = Send-Request -Method "Get" -Route "/stores/me/products/$ProductId" -Token $MerchantToken
Assert-Status -Scenario "4.3: Get Single Product Inventory Details" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 4.4: Update Product Stock & Price (Happy Path)
$UpdatePrdPayload = @{
    discountedPrice = 6.00
    quantityAvailable = 8
    status = "Active"
}
$Res = Send-Request -Method "Patch" -Route "/stores/me/products/$ProductId" -Body $UpdatePrdPayload -Token $MerchantToken
Assert-Status -Scenario "4.4: Update Product Pricing & Stock Levels" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 5: MARKETPLACE (/marketplace)
# ==============================================================================
Write-Host "`n--- Testing Public Marketplace ---" -ForegroundColor Yellow

# Scenario 5.1: Retrieve Active Near Products
$Res = Send-Request -Method "Get" -Route "/marketplace/products?latitude=30.0450&longitude=31.2360&maxDistance=10" -Token $CustomerToken
Assert-Status -Scenario "5.1: Get Nearby Marketplace Products" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 6: ORDERS & CHECKOUT (/orders)
# ==============================================================================
Write-Host "`n--- Testing Orders & Checkout ---" -ForegroundColor Yellow

# First, Admin approves the Merchant Store to allow checkouts
if ($AdminToken) {
    Send-Request -Method "Patch" -Route "/admin/stores/$OrganizationId/verify" -Body @{ status = "Approved"; adminNotes = "Verified via QA Script" } -Token $AdminToken
}

# Scenario 6.1: Checkout Cart (Happy Path)
$OrderPayload = @{
    items = @(
        @{ productId = $ProductId; quantity = 2 }
    )
}
$Res = Send-Request -Method "Post" -Route "/orders" -Body $OrderPayload -Token $CustomerToken
Assert-Status -Scenario "6.1: Place Checkout Order" -ActualCode $Res.StatusCode -ExpectedCode 200
if ($Res.StatusCode -eq 200) {
    $OrderId = $Res.Data.data.id
}

# Scenario 6.2: Checkout (Validation Failure - Exceeding Stock)
$OverStockPayload = @{
    items = @(
        @{ productId = $ProductId; quantity = 100 } # Exceeds available stock of 8
    )
}
$Res = Send-Request -Method "Post" -Route "/orders" -Body $OverStockPayload -Token $CustomerToken
Assert-Status -Scenario "6.2: Place Order Exceeding Stock Level" -ActualCode $Res.StatusCode -ExpectedCode 400

# Scenario 6.3: Get Order Details (Happy Path)
$Res = Send-Request -Method "Get" -Route "/orders/$OrderId" -Token $CustomerToken
Assert-Status -Scenario "6.3: Retrieve Checkout Order Details" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 7: STORES REVIEWS (/reviews)
# ==============================================================================
Write-Host "`n--- Testing Store Reviews ---" -ForegroundColor Yellow

# Scenario 7.1: Leave Review (Happy Path)
$ReviewPayload = @{
    orderId = $OrderId
    organizationId = $OrganizationId
    rating = 5
    comment = "Exceptional service and very tasty sourdough loaf!"
}
$Res = Send-Request -Method "Post" -Route "/reviews" -Body $ReviewPayload -Token $CustomerToken
Assert-Status -Scenario "7.1: Post Order Rating Review" -ActualCode $Res.StatusCode -ExpectedCode 200

# Scenario 7.2: Leave Review (Validation Failure - Rating Out Of Range)
$BadReviewPayload = @{
    orderId = $OrderId
    organizationId = $OrganizationId
    rating = 10 # Ratings must be 1 to 5
    comment = "Too high rating"
}
$Res = Send-Request -Method "Post" -Route "/reviews" -Body $BadReviewPayload -Token $CustomerToken
Assert-Status -Scenario "7.2: Post Review Rating Value Out Of Bounds" -ActualCode $Res.StatusCode -ExpectedCode 400

# ==============================================================================
# SECTION 8: NOTIFICATIONS HUB (/notifications)
# ==============================================================================
Write-Host "`n--- Testing Notifications ---" -ForegroundColor Yellow

# Scenario 8.1: Get User Notifications
$Res = Send-Request -Method "Get" -Route "/notifications" -Token $CustomerToken
Assert-Status -Scenario "8.1: Get Customer Notifications Feed" -ActualCode $Res.StatusCode -ExpectedCode 200
if ($Res.Data.data.Count -gt 0) {
    $NotificationId = $Res.Data.data[0].id
}

# Scenario 8.2: Mark Notification Read (Happy Path)
if ($NotificationId) {
    $Res = Send-Request -Method "Patch" -Route "/notifications/$NotificationId/read" -Token $CustomerToken
    Assert-Status -Scenario "8.2: Mark Notification Feed Alert As Read" -ActualCode $Res.StatusCode -ExpectedCode 200
}

# Scenario 8.3: Mark All Read
$Res = Send-Request -Method "Patch" -Route "/notifications/read-all" -Token $CustomerToken
Assert-Status -Scenario "8.3: Mark All User Notifications As Read" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 9: CUSTOMER SUPPORT MODULE (/support-tickets)
# ==============================================================================
Write-Host "`n--- Testing Customer Support Tickets ---" -ForegroundColor Yellow

# Scenario 9.1: Create Support Ticket (Happy Path)
$TicketPayload = @{
    category = "Refund"
    message = "I cancelled my order but the funds haven't returned to my balance yet."
    priority = "High"
}
$Res = Send-Request -Method "Post" -Route "/support-tickets" -Body $TicketPayload -Token $CustomerToken
Assert-Status -Scenario "9.1: Create Customer Support Ticket" -ActualCode $Res.StatusCode -ExpectedCode 200
$SupportTicketId = $Res.Data.data.id

# Scenario 9.2: Reply to Support Ticket (Happy Path)
$ReplyPayload = @{
    message = "Please expedite this issue."
}
$Res = Send-Request -Method "Post" -Route "/support-tickets/$SupportTicketId/reply" -Body $ReplyPayload -Token $CustomerToken
Assert-Status -Scenario "9.2: Post Customer Support Message Reply" -ActualCode $Res.StatusCode -ExpectedCode 200

# ==============================================================================
# SECTION 10: ADMIN ACTIONS (/admin)
# ==============================================================================
Write-Host "`n--- Testing Admin Operations ---" -ForegroundColor Yellow

if ($AdminToken) {
    # Scenario 10.1: Get Pending Verification Stores
    $Res = Send-Request -Method "Get" -Route "/admin/stores/pending" -Token $AdminToken
    Assert-Status -Scenario "10.1: Retrieve Pending Onboarding Queue" -ActualCode $Res.StatusCode -ExpectedCode 200

    # Scenario 10.2: Ban User Profile
    # Ban a test customer
    $Res = Send-Request -Method "Patch" -Route "/admin/users/usr-customer2-guid-0000-0000-000000000007/status" -Body @{ status = "Banned" } -Token $AdminToken
    Assert-Status -Scenario "10.2: Ban User Account Profile" -ActualCode $Res.StatusCode -ExpectedCode 200
} else {
    Write-Host "[WARNING] Skipping Admin-locked checks due to missing admin token." -ForegroundColor Yellow
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "FoodLoop Automated Integration Test Suite Completed" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
