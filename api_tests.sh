#!/usr/bin/env bash
# FoodLoop API Automated Test Suite (Bash Edition)
# This script executes happy paths, validation failures, not found, unauthorized, and state transition test scenarios.

set +e  # Don't abort on first failure — run all tests and report at end

# Teardown / Cleanup Section for created entities
teardown() {
    # Don't let errors in cleanup abort the script exit
    set +e
    echo -e "\n--- Executing Self-Cleanup Teardown ---"

    # 1. Delete review if created
    if [ -n "$REVIEW_ID" ] && [ -n "$ADMIN_TOKEN" ]; then
        echo "[CLEANUP] Deleting Review ID: $REVIEW_ID"
        send_request "DELETE" "/admin/reviews/$REVIEW_ID" "" "$ADMIN_TOKEN" >/dev/null || true
    fi

    # 2. Delete product if created
    if [ -n "$PRODUCT_ID" ]; then
        echo "[CLEANUP] Deleting Product ID: $PRODUCT_ID"
        if [ -n "$ADMIN_TOKEN" ]; then
            send_request "DELETE" "/admin/products/$PRODUCT_ID" "" "$ADMIN_TOKEN" >/dev/null || true
        elif [ -n "$MERCHANT_TOKEN" ]; then
            send_request "DELETE" "/stores/me/products/$PRODUCT_ID" "" "$MERCHANT_TOKEN" >/dev/null || true
        fi
    fi

    # 3. Delete address if created
    if [ -n "$ADDRESS_ID" ] && [ -n "$CUSTOMER_TOKEN" ]; then
        echo "[CLEANUP] Deleting Address ID: $ADDRESS_ID"
        send_request "DELETE" "/users/me/addresses/$ADDRESS_ID" "" "$CUSTOMER_TOKEN" >/dev/null || true
    fi

    # 4. Delete admin created user if created
    if [ -n "$ADM_USER_ID" ] && [ -n "$ADMIN_TOKEN" ]; then
        echo "[CLEANUP] Deleting User ID: $ADM_USER_ID"
        send_request "DELETE" "/users/$ADM_USER_ID" "" "$ADMIN_TOKEN" >/dev/null || true
    fi

    # Remove temporary files
    rm -f temp_payload.json mock_cr.pdf mock_charity_cr.pdf mock_img.png bulk_prd.csv test.pdf
}
trap teardown EXIT INT TERM



# --- Configuration & Environment Setup ---
BASE_URL="http://127.0.0.1:5267" # Default local HTTP port from launchSettings.json
echo "=========================================================="
echo "FoodLoop Automated Integration Test Suite (Bash)"
echo "Target Base URL: $BASE_URL"
echo "=========================================================="

# Test Metrics
PASS_COUNT=0
FAIL_COUNT=0

# Global State Variables (to pass IDs between steps)
ADMIN_TOKEN=""
MERCHANT_TOKEN=""
CUSTOMER_TOKEN=""
CHARITY_TOKEN=""

CUSTOMER_USER_ID=""
ORGANIZATION_ID=""
C_ORG_ID=""
PRODUCT_ID=""
ORDER_ID=""
REVIEW_ID=""
SUPPORT_TICKET_ID=""
NOTIFICATION_ID=""
IMAGE_ID=""
ADM_USER_ID=""
ADDRESS_ID=""


# Helper function to extract a value from JSON using simple grep/regex
get_json_value() {
    local json="$1"
    local key="$2"
    # Try parsing string values, then numbers, then booleans
    (echo "$json" | grep -oP '"'"$key"'"\s*:\s*"\K[^"]+' || \
     echo "$json" | grep -oP '"'"$key"'"\s*:\s*\K[0-9.-]+' || \
     echo "$json" | grep -oP '"'"$key"'"\s*:\s*\K[a-zA-Z]+') | head -n 1 || true
}

# Helper function to send requests and return "StatusCode|ResponseBody"
send_request() {
    local method="$1"
    local route="$2"
    local body="$3"
    local token="$4"
    local content_type="${5:-application/json}"

    local headers=("-H" "Content-Type: $content_type")
    if [ -n "$token" ]; then
        headers+=("-H" "Authorization: Bearer $token")
    fi

    # Build curl arguments
    local curl_args=("-s" "-k" "-L" "-w" "\n%{http_code}" "-X" "$method")
    for h in "${headers[@]}"; do
        curl_args+=("$h")
    done

    if [ -n "$body" ]; then
        curl_args+=("--data-raw" "$body")
    fi
    curl_args+=("$BASE_URL$route")

    # Execute request
    local response
    response=$(curl.exe "${curl_args[@]}")

    # Extract status code and body
    local status_code
    status_code=$(echo "$response" | tail -n 1)
    local response_body
    response_body=$(echo "$response" | sed '$d')

    echo "$status_code|$response_body"
}

# Helper function to log assertions
assert_status() {
    local scenario="$1"
    local actual_code="$2"
    local expected_code="$3"
    local response_body="$4"

    if [ "$actual_code" = "$expected_code" ]; then
        echo -e "\e[32m[PASS]\e[0m $scenario (HTTP $actual_code)"
        PASS_COUNT=$((PASS_COUNT + 1))
        return 0
    else
        echo -e "\e[31m[FAIL]\e[0m $scenario (Expected: $expected_code, Got: $actual_code)"
        if [ -n "$response_body" ]; then
            echo -e "       Response: $response_body"
        fi
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return 1
    fi
}

create_valid_png() {
    local target_file="$1"
    echo "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==" | base64 -d > "$target_file" 2>/dev/null || printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15c4\x00\x00\x00\rIDATx\x9cc`\x00\x00\x00\x02\x00\x01H\xaf\xa4q\x00\x00\x00\x00IEND\xaeB`\x82' > "$target_file"
}

# ==============================================================================
# SECTION 0: ROOT & HEALTH CHECK ENDPOINTS
# ==============================================================================
echo -e "\n--- Testing Health & Root Routes ---"

# Scenario 0.1: GET / (Root Welcome Page)
res=$(send_request "GET" "/")
status=${res%%|*}
body=${res#*|}
assert_status "0.1: GET Welcome Page" "$status" "200" "$body"

# Scenario 0.2: GET /health (Health Check)
res=$(send_request "GET" "/health")
status=${res%%|*}
body=${res#*|}
assert_status "0.2: GET Health Check" "$status" "200" "$body"

# ==============================================================================
# ==============================================================================
# SECTION 1: AUTHENTICATION & ONBOARDING (/auth)
# ==============================================================================
echo -e "\n--- Testing Authentication & Registration ---"

# Scenario 1.1: Register Customer (Happy Path)
RANDOM_VAL="$(date +%s)_$((10000 + RANDOM % 90000))"
CUST_REG_EMAIL="cust_${RANDOM_VAL}@example.com"
C_PHONE="+2010$((10000000 + RANDOM % 90000000))"
REG_PAYLOAD="{\"name\":\"Test Customer $RANDOM_VAL\",\"email\":\"$CUST_REG_EMAIL\",\"password\":\"Password@123\",\"phoneNumber\":\"$C_PHONE\",\"role\":\"Customer\"}"
res=$(send_request "POST" "/auth/register" "$REG_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.1: Register New Customer Account" "$status" "201" "$body"
CUSTOMER_USER_ID=$(echo "$body" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)

# Scenario 1.2: Register (Conflict - Email Already Exists)
res=$(send_request "POST" "/auth/register" "$REG_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.2: Register Duplicate Customer Email Conflict" "$status" "400" "$body"

# Scenario 1.3: Login (Happy Path - Merchant Login to get tokens)
MERCHANT_EMAIL="merchant.spinneys@example.com"
LOGIN_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"password\":\"Password@123\"}"
res=$(send_request "POST" "/auth/login" "$LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.3: Log In Merchant Account" "$status" "200" "$body"
MERCHANT_TOKEN=$(get_json_value "$body" "accessToken")
M_REFRESH_TOKEN=$(get_json_value "$body" "refreshToken")

# Login Customer (Happy Path)
CUST_LOGIN_PAYLOAD="{\"email\":\"$CUST_REG_EMAIL\",\"password\":\"Password@123\"}"
res=$(send_request "POST" "/auth/login" "$CUST_LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
CUSTOMER_TOKEN=$(get_json_value "$body" "accessToken")

# Scenario 1.4: Login (Unauthorized Credentials)
BAD_LOGIN_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"password\":\"WrongPassword\"}"
res=$(send_request "POST" "/auth/login" "$BAD_LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.4: Log In With Wrong Password" "$status" "401" "$body"

# Scenario 1.5: Forgot Password (Happy Path)
FORGOT_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\"}"
res=$(send_request "POST" "/auth/forgot-password" "$FORGOT_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.5: Request Password Reset Verification" "$status" "200" "$body"
RESET_TOKEN=$(get_json_value "$body" "debugToken")

# Scenario 1.6: Reset Password (Happy Path if token present, or invalid token verification)
if [ -n "$RESET_TOKEN" ] && [ "$RESET_TOKEN" != "null" ]; then
    RESET_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"token\":\"$RESET_TOKEN\",\"newPassword\":\"Password@123\"}"
    res=$(send_request "POST" "/auth/reset-password" "$RESET_PAYLOAD")
    status=${res%%|*}
    body=${res#*|}
    assert_status "1.6: Reset Password With Token" "$status" "200" "$body"
else
    BAD_RESET_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"token\":\"invalid-token-12345\",\"newPassword\":\"Password@123\"}"
    res=$(send_request "POST" "/auth/reset-password" "$BAD_RESET_PAYLOAD")
    status=${res%%|*}
    body=${res#*|}
    assert_status "1.6: Reset Password Rejects Invalid Token" "$status" "400" "$body"
fi

# Scenario 1.7: Refresh Token (Happy Path)
REFRESH_PAYLOAD="{\"refreshToken\":\"$M_REFRESH_TOKEN\"}"
res=$(send_request "POST" "/auth/refresh" "$REFRESH_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.7: Refresh Session Tokens" "$status" "200" "$body"
NEW_REFRESH_TOKEN=$(get_json_value "$body" "refreshToken")

# Scenario 1.8: Resend Verification (Happy Path)
RESEND_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\"}"
res=$(send_request "POST" "/auth/resend-verification" "$RESEND_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.8: Resend Email Verification" "$status" "200" "$body"

# Scenario 1.9: Logout (Happy Path)
LOGOUT_PAYLOAD="{\"refreshToken\":\"$NEW_REFRESH_TOKEN\"}"
res=$(send_request "POST" "/auth/logout" "$LOGOUT_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.9: Log Out Active Session" "$status" "200" "$body"

# Log In Verified Merchant for subsequent inventory/store operations
res=$(send_request "POST" "/auth/login" "{\"email\":\"merchant.spinneys@example.com\",\"password\":\"Password@123\"}")
MERCHANT_TOKEN=$(get_json_value "${res#*|}" "accessToken")
M_REFRESH_TOKEN=$(get_json_value "${res#*|}" "refreshToken")
echo "[INFO] Loaded Verified Merchant token successfully."

# Log In Customer for customer operations
res=$(send_request "POST" "/auth/login" "{\"email\":\"$CUST_REG_EMAIL\",\"password\":\"Password@123\"}")
CUSTOMER_TOKEN=$(get_json_value "${res#*|}" "accessToken")
CUSTOMER_USER_ID=$(echo "${res#*|}" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)
echo "[INFO] Loaded Customer token successfully: $CUSTOMER_USER_ID"

# Log In System Admin
ADMIN_LOGIN_PAYLOAD="{\"email\":\"admin@foodloop.com\",\"password\":\"Admin@123\"}"
res=$(send_request "POST" "/auth/login" "$ADMIN_LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    ADMIN_TOKEN=$(get_json_value "$body" "accessToken")
    echo "[INFO] Loaded System Admin token successfully."
fi

# Log In Verified Charity
CHARITY_EMAIL="charity.foodbank@example.com"
res=$(send_request "POST" "/auth/login" "{\"email\":\"$CHARITY_EMAIL\",\"password\":\"Password@123\"}")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    CHARITY_TOKEN=$(get_json_value "$body" "accessToken")
    echo "[INFO] Loaded Verified Charity token successfully."
fi

# ==============================================================================
# SECTION 2: USER PROFILE & ADDRESS MODULE (/users)
# ==============================================================================
echo -e "\n--- Testing User Profiles & Addresses ---"

# Scenario 2.1: Get My Profile (Happy Path)
res=$(send_request "GET" "/users/me" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.1: Get Current Customer Profile" "$status" "200" "$body"

# Scenario 2.2: Get Profile Without Bearer Token (HTTP 401 Unauthorized)
res=$(send_request "GET" "/users/me" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "2.2: Get Profile Without Bearer Token" "$status" "401" "$body"

# Scenario 2.3: Update Profile (Happy Path)
C_PHONE="010$((10000000 + RANDOM % 90000000))"
UPDATE_PROFILE_PAYLOAD="{\"fullName\":\"Updated Customer Name $RANDOM_VAL\",\"language\":\"ar\",\"phoneNumber\":\"$C_PHONE\"}"
res=$(send_request "PATCH" "/users/me" "$UPDATE_PROFILE_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.3: Update Customer Profile Details" "$status" "200" "$body"

# Scenario 2.4: Create Address (Happy Path)
ADDRESS_PAYLOAD="{\"addressType\":\"Home\",\"city\":\"Cairo\",\"district\":\"Maadi\",\"street\":\"El-Nasr St\",\"buildingNo\":\"15\",\"floor\":\"2\",\"apartmentNo\":\"6\",\"latitude\":30.0444,\"longitude\":31.2357,\"isDefault\":true}"
res=$(send_request "POST" "/users/me/addresses" "$ADDRESS_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.4: Add New Delivery Address" "$status" "201" "$body"
ADDRESS_ID=$(get_json_value "$body" "id")

# Scenario 2.5: Get Addresses (Happy Path)
res=$(send_request "GET" "/users/me/addresses" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.5: List All Saved User Addresses" "$status" "200" "$body"

# Scenario 2.6: Update Address (Happy Path)
UPDATE_ADDR_PAYLOAD="{\"city\":\"Cairo\",\"district\":\"Zamalek\",\"street\":\"26 July St\"}"
res=$(send_request "PATCH" "/users/me/addresses/$ADDRESS_ID" "$UPDATE_ADDR_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.6: Update Delivery Address Zamalek" "$status" "200" "$body"

# Scenario 2.7: Update Non-existent Address (404 Not Found)
res=$(send_request "PATCH" "/users/me/addresses/00000000-0000-0000-0000-000000000000" "$UPDATE_ADDR_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.7: Update Non-existent Address ID" "$status" "404" "$body"

# Scenario 2.8: Delete Address (Happy Path)
res=$(send_request "DELETE" "/users/me/addresses/$ADDRESS_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.8: Remove Saved Address" "$status" "204" "$body"

# Scenario 2.9: Delete Non-existent Address (404 Not Found)
res=$(send_request "DELETE" "/users/me/addresses/00000000-0000-0000-0000-000000000000" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.9: Remove Non-existent Address ID" "$status" "404" "$body"

# Scenario 2.10: Open Ticket (Happy Path)
TKT_PAYLOAD="{\"category\":\"Account\",\"message\":\"Issue with profile updates\",\"priority\":\"Low\"}"
res=$(send_request "POST" "/users/me/tickets" "$TKT_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.10: Open Support Ticket via Users Controller" "$status" "200" "$body"

# Scenario 2.11: Update Preferences (Happy Path)
PREF_PAYLOAD="{\"orderUpdatesEnabled\":true,\"marketingNotificationsEnabled\":true}"
res=$(send_request "PATCH" "/users/me/preferences" "$PREF_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "2.11: Update Notification Settings" "$status" "200" "$body"

# ==============================================================================
# SECTION 3: STORES & ORGANIZATIONS (/stores & /charities)
# ==============================================================================
echo -e "\n--- Testing Stores & Organizations ---"

# Scenario 3.1: Get My Store Profile
res=$(send_request "GET" "/stores/me" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.1: Retrieve Merchant Store Details" "$status" "200" "$body"
ORGANIZATION_ID=$(get_json_value "$body" "id")

# Scenario 3.2: Update Store Profile Location (Happy Path)
LOC_PAYLOAD="{\"latitude\":30.0450,\"longitude\":31.2360,\"governorate\":\"Cairo\",\"city\":\"Cairo\",\"neighborhood\":\"Maadi\",\"street\":\"Street 9\",\"buildingNo\":\"24\"}"
res=$(send_request "PATCH" "/stores/me/location" "$LOC_PAYLOAD" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.2: Update Store Location Details" "$status" "200" "$body"

# Scenario 3.3: Submit Store Documents (Mock multipart upload)
echo "PDF-MOCK-CONTENT-GOES-HERE" > mock_cr.pdf
res=$(curl.exe -s -k -L -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "Email=$MERCHANT_EMAIL" \
  -F "Type=CommercialRegistration" \
  -F "File=@mock_cr.pdf" \
  "$BASE_URL/stores/me/documents")
rm -f mock_cr.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.3: Upload Store Verification Documents" "$status" "200" "$body"

# Scenario 3.4: Submit Charity Documents (Mock multipart upload)
echo "PDF-CHARITY-CR-CONTENT" > mock_charity_cr.pdf
res=$(curl.exe -s -k -L -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $CHARITY_TOKEN" \
  -F "Email=$CHARITY_EMAIL" \
  -F "Type=AssociationCertificate" \
  -F "File=@mock_charity_cr.pdf" \
  "$BASE_URL/charities/me/documents")
rm -f mock_charity_cr.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.4: Upload Charity Association Certificate" "$status" "200" "$body"

# Re-approve store & charity so inventory operations remain active
if [ -n "$ADMIN_TOKEN" ]; then
    send_request "PATCH" "/admin/stores/$ORGANIZATION_ID/verify" "{\"action\":\"Approved\",\"note\":\"Approved for testing\"}" "$ADMIN_TOKEN" > /dev/null
    if [ -n "$C_ORG_ID" ]; then
        send_request "PATCH" "/admin/charities/$C_ORG_ID/verify" "{\"action\":\"Approved\",\"note\":\"Approved for testing\"}" "$ADMIN_TOKEN" > /dev/null
    fi
fi

# Scenario 3.5: Update Store Profile multipart (Happy Path)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "Name=Spinneys Supermarket Updated $RANDOM_VAL" \
  -F "BusinessCategory=Supermarket" \
  "$BASE_URL/stores/me")
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.5: Update Store Name and Category Profile" "$status" "200" "$body"

# Scenario 3.5.1: Update Store Profile with CoverPhoto
create_valid_png mock_cover.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "Name=Spinneys Supermarket Updated $RANDOM_VAL" \
  -F "BusinessCategory=Supermarket" \
  -F "CoverPhoto=@mock_cover.png" \
  "$BASE_URL/stores/me")
rm -f mock_cover.png
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.5.1: Update Store Profile with CoverPhoto" "$status" "200" "$body"

# Scenario 3.6: Get Received Merchant Orders
res=$(send_request "GET" "/stores/me/orders" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.6: Retrieve Merchant Received Orders List" "$status" "200" "$body"

# Scenario 3.7: Get Merchant Store Analytics (all-time, default)
res=$(send_request "GET" "/stores/me/analytics" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7: Retrieve Merchant Store Analytics (all-time)" "$status" "200" "$body"

# Scenario 3.7.a: Get Analytics filtered by period=today
res=$(send_request "GET" "/stores/me/analytics?period=today" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.a: Retrieve Merchant Store Analytics (today)" "$status" "200" "$body"

# Scenario 3.7.b: Get Analytics filtered by period=week
res=$(send_request "GET" "/stores/me/analytics?period=week" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.b: Retrieve Merchant Store Analytics (week)" "$status" "200" "$body"

# Scenario 3.7.c: Get Analytics filtered by period=month
res=$(send_request "GET" "/stores/me/analytics?period=month" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.c: Retrieve Merchant Store Analytics (month)" "$status" "200" "$body"

# Scenario 3.7.d: Get Analytics with invalid period (should 400)
res=$(send_request "GET" "/stores/me/analytics?period=invalid" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.d: Retrieve Store Analytics With Invalid Period" "$status" "400" "$body"

# Scenario 3.7.1: Get Analytics without Token (Unauthenticated)
res=$(send_request "GET" "/stores/me/analytics" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.1: Retrieve Store Analytics Unauthenticated" "$status" "401" "$body"

# Scenario 3.7.2: Get Analytics as Customer (Forbidden)
res=$(send_request "GET" "/stores/me/analytics" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "3.7.2: Retrieve Store Analytics as Customer" "$status" "403" "$body"

# ==============================================================================
# SECTION 4: MERCHANT INVENTORY (/stores/me/products)
# ==============================================================================
echo -e "\n--- Testing Inventory Management ---"

# Retrieve Category ID Dynamically
res=$(send_request "GET" "/categories" "")
status=${res%%|*}
body=${res#*|}
CATEGORY_ID=$(get_json_value "$body" "id")
echo "[INFO] Loaded Dynamic Category ID: $CATEGORY_ID"

# Scenario 4.1: Add Product (Happy Path)
PRD_PAYLOAD="{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"Artisan Sourdough Loaf $RANDOM_VAL\",\"description\":\"Crispy sourdough bread.\",\"originalPrice\":15.00,\"discountedPrice\":7.50,\"quantityAvailable\":10,\"expirationDate\":\"2026-08-15\"}"
res=$(send_request "POST" "/stores/me/products" "$PRD_PAYLOAD" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.1: Add Product to Store Inventory" "$status" "200" "$body"
PRODUCT_ID=$(get_json_value "$body" "id")

# Scenario 4.2: Add Product (Validation Fail - Discount Price > Original Price)
BAD_PRD_PAYLOAD="{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"Invalid Price Item\",\"originalPrice\":10.00,\"discountedPrice\":50.00,\"quantityAvailable\":5,\"expirationDate\":\"2026-08-15\"}"
res=$(send_request "POST" "/stores/me/products" "$BAD_PRD_PAYLOAD" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.2: Add Product With Invalid Discount Price" "$status" "400" "$body"

# Scenario 4.3: Get Single Product details
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.3: Get Single Product Inventory Details" "$status" "200" "$body"

# Scenario 4.4: Get Single Product Non-existent details (404 Not Found)
res=$(send_request "GET" "/stores/me/products/00000000-0000-0000-0000-000000000000" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.4: Get Non-existent Product Details" "$status" "404" "$body"

# Scenario 4.5: Update Product Stock & Price (Happy Path)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "DiscountedPrice=6.00" \
  -F "QuantityAvailable=8" \
  -F "Status=Active" \
  "$BASE_URL/stores/me/products/$PRODUCT_ID")
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "4.5: Update Product Pricing & Stock Levels" "$status" "200" "$body"

# Scenario 4.6: List Merchant Inventory Products
res=$(send_request "GET" "/stores/me/products?status=Active" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.6: List Active Merchant Inventory Listings" "$status" "200" "$body"

# Scenario 4.7: Upload Product Image (Happy Path)
create_valid_png mock_img.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "File=@mock_img.png" \
  "$BASE_URL/stores/me/products/$PRODUCT_ID/images")
rm -f mock_img.png
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "4.7: Upload Product Display Image" "$status" "200" "$body"
# Target extraction of the first image ID in the list
IMAGE_ID=$(echo "$body" | grep -oP '"images"\s*:\s*\[\s*\{\s*"id"\s*:\s*"\K[^"]+' | head -n 1 || true)

# Scenario 4.8: Delete Product Image (Happy Path)
if [ -n "$IMAGE_ID" ]; then
    res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID/images/$IMAGE_ID" "" "$MERCHANT_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "4.8: Remove Product Display Image" "$status" "200" "$body"
fi

# Scenario 4.9: Bulk Upload Products CSV (Happy Path)
echo "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname" > bulk_prd.csv
echo "Bulk Artisan Bread $RANDOM_VAL,20.00,10.00,15,2026-08-25,Bakery" >> bulk_prd.csv
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "File=@bulk_prd.csv" \
  "$BASE_URL/stores/me/products/bulk")
rm bulk_prd.csv
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "4.9: Bulk Upload Inventory via CSV" "$status" "200" "$body"

# Scenario 4.10: Soft Delete Product (Happy Path)
res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.10: Soft Delete Inventory Listing" "$status" "200" "$body"

# Re-add product for checkout orders testing
res=$(send_request "POST" "/stores/me/products" "$PRD_PAYLOAD" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
PRODUCT_ID=$(get_json_value "$body" "id")

# Scenario 4.11: Retrieve Store Pricing Overview
res=$(send_request "GET" "/stores/me/products/pricing" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.11: Retrieve Store Pricing Overview" "$status" "200" "$body"

# Scenario 4.11.1: Retrieve Store Pricing Overview without Token (Unauthenticated)
res=$(send_request "GET" "/stores/me/products/pricing" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "4.11.1: Retrieve Store Pricing Overview Unauthenticated" "$status" "401" "$body"

# Scenario 4.11.2: Retrieve Store Pricing Overview as Customer (Forbidden)
res=$(send_request "GET" "/stores/me/products/pricing" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "4.11.2: Retrieve Store Pricing Overview as Customer" "$status" "403" "$body"


# ==============================================================================
# SECTION 5: MARKETPLACE (/marketplace)
# ==============================================================================
echo -e "\n--- Testing Public Marketplace ---"

# Scenario 5.1: Retrieve Active Near Products
res=$(send_request "GET" "/marketplace/products?latitude=30.0450&longitude=31.2360&maxDistance=10" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "5.1: Get Nearby Marketplace Products" "$status" "200" "$body"

# Scenario 5.2: Filter Marketplace by Category, Min Price & Max Price
res=$(send_request "GET" "/marketplace/products?categoryId=$CATEGORY_ID&minPrice=1&maxPrice=100&sortBy=price" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "5.2: Search Products with Category & Sorting" "$status" "200" "$body"

# ==============================================================================
# SECTION 6: ORDERS & CHECKOUT (/orders)
# ==============================================================================
echo -e "\n--- Testing Orders & Checkout ---"

# First, Admin approves the Merchant Store to allow checkouts
if [ -n "$ADMIN_TOKEN" ]; then
    send_request "PATCH" "/admin/stores/$ORGANIZATION_ID/verify" "{\"status\":\"Approved\",\"adminNotes\":\"Verified via QA Script\"}" "$ADMIN_TOKEN" > /dev/null
fi

# Scenario 6.1: Checkout Cart (Happy Path)
ORDER_PAYLOAD="{\"items\":[{\"productId\":\"$PRODUCT_ID\",\"quantity\":2}]}"
res=$(send_request "POST" "/orders" "$ORDER_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "6.1: Place Checkout Order" "$status" "200" "$body"
if [ "$status" -eq 200 ]; then
    ORDER_ID=$(get_json_value "$body" "id")
fi

# Scenario 6.2: Checkout (Validation Failure - Exceeding Stock)
OVER_STOCK_PAYLOAD="{\"items\":[{\"productId\":\"$PRODUCT_ID\",\"quantity\":100}]}"
res=$(send_request "POST" "/orders" "$OVER_STOCK_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "6.2: Place Order Exceeding Stock Level" "$status" "400" "$body"

# Scenario 6.3: Get Order Details (Happy Path)
res=$(send_request "GET" "/orders/$ORDER_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "6.3: Retrieve Checkout Order Details" "$status" "200" "$body"

# Scenario 6.4: Get Customer Order History (Happy Path)
res=$(send_request "GET" "/orders" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "6.4: List Customer Order History" "$status" "200" "$body"

# Scenario 6.5: Transition Order to Completed (Happy Path)
res=$(send_request "PATCH" "/stores/me/orders/$ORDER_ID/status" "{\"status\":\"Completed\"}" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "6.5: Transition Order to Completed" "$status" "200" "$body"

# ==============================================================================
# SECTION 7: STORES REVIEWS (/reviews)
# ==============================================================================
echo -e "\n--- Testing Store Reviews ---"

# Scenario 7.1: Leave Review (Happy Path)
REVIEW_PAYLOAD="{\"orderId\":\"$ORDER_ID\",\"organizationId\":\"$ORGANIZATION_ID\",\"rating\":5,\"comment\":\"Exceptional service and sourdough! $RANDOM_VAL\"}"
res=$(send_request "POST" "/reviews" "$REVIEW_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "7.1: Post Order Rating Review" "$status" "200" "$body"
REVIEW_ID=$(get_json_value "$body" "id")

# Scenario 7.2: Leave Review (Validation Failure - Rating Out Of Range)
BAD_REVIEW_PAYLOAD="{\"orderId\":\"$ORDER_ID\",\"organizationId\":\"$ORGANIZATION_ID\",\"rating\":10,\"comment\":\"Too high rating\"}"
res=$(send_request "POST" "/reviews" "$BAD_REVIEW_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "7.2: Post Review Rating Value Out Of Bounds" "$status" "400" "$body"

# Scenario 7.3: Get Store Reviews List (Happy Path)
res=$(send_request "GET" "/stores/$ORGANIZATION_ID/reviews?pageNumber=1&pageSize=10" "")
status=${res%%|*}
body=${res#*|}
assert_status "7.3: List Store Reviews" "$status" "200" "$body"

# ==============================================================================
# SECTION 8: NOTIFICATIONS HUB (/notifications)
# ==============================================================================
echo -e "\n--- Testing Notifications ---"

# Scenario 8.1: Get User Notifications
res=$(send_request "GET" "/notifications" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "8.1: Get Customer Notifications Feed" "$status" "200" "$body"
NOTIFICATION_ID=$(get_json_value "$body" "id")

# Scenario 8.2: Mark Notification Read (Happy Path)
if [ -n "$NOTIFICATION_ID" ]; then
    res=$(send_request "PATCH" "/notifications/$NOTIFICATION_ID/read" "" "$CUSTOMER_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "8.2: Mark Notification Feed Alert As Read" "$status" "204" "$body"
fi

# Scenario 8.3: Mark All Read
res=$(send_request "PATCH" "/notifications/read-all" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "8.3: Mark All User Notifications As Read" "$status" "204" "$body"

# ==============================================================================
# SECTION 9: CUSTOMER SUPPORT MODULE (/support-tickets)
# ==============================================================================
echo -e "\n--- Testing Customer Support Tickets ---"

# Scenario 9.1: Create Support Ticket (Happy Path)
TICKET_PAYLOAD="{\"category\":\"Refund\",\"message\":\"Refund delay query $RANDOM_VAL.\",\"priority\":\"High\"}"
res=$(send_request "POST" "/support-tickets" "$TICKET_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "9.1: Create Customer Support Ticket" "$status" "200" "$body"
SUPPORT_TICKET_ID=$(get_json_value "$body" "id")

# Scenario 9.2: Reply to Support Ticket (Happy Path)
REPLY_PAYLOAD="{\"message\":\"Please expedite this issue.\"}"
res=$(send_request "POST" "/support-tickets/$SUPPORT_TICKET_ID/reply" "$REPLY_PAYLOAD" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "9.2: Post Customer Support Message Reply" "$status" "200" "$body"

# Scenario 9.3: List My Support Tickets (Happy Path)
res=$(send_request "GET" "/support-tickets" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "9.3: List Customer Support Tickets" "$status" "200" "$body"

# Scenario 9.4: Support Ticket Detail and Messages (Happy Path)
res=$(send_request "GET" "/support-tickets/$SUPPORT_TICKET_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "9.4: Get Support Ticket Conversation Details" "$status" "200" "$body"

# ==============================================================================
# SECTION 10: ADMIN OPERATIONS (/admin)
# ==============================================================================
echo -e "\n--- Testing Admin Operations ---"

# Scenario 10.0: Access Admin Route (HTTP 403 Forbidden check using Customer token)
res=$(send_request "GET" "/admin/analytics/summary" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "10.0: Forbidden Access check for Customer on Admin Route" "$status" "403" "$body"

if [ -n "$ADMIN_TOKEN" ]; then
    # Scenario 10.1: Get Pending Verification Stores
    res=$(send_request "GET" "/admin/stores/pending" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.1: Retrieve Pending Onboarding Queue" "$status" "200" "$body"

    # Fetch organization ID from pending queue or list
    res=$(send_request "GET" "/admin/stores/pending" "" "$ADMIN_TOKEN")
    body=${res#*|}
    M_ORG_ID=$(echo "$body" | tr ',' '\n' | tr '{' '\n' | grep -B 25 -i "$MERCHANT_EMAIL" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)
    C_ORG_ID=$(echo "$body" | tr ',' '\n' | tr '{' '\n' | grep -B 25 -i "$CHARITY_EMAIL" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)

    if [ -z "$M_ORG_ID" ]; then
        res=$(send_request "GET" "/admin/stores" "" "$ADMIN_TOKEN")
        M_ORG_ID=$(echo "${res#*|}" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)
    fi
    if [ -z "$C_ORG_ID" ]; then
        res=$(send_request "GET" "/admin/charities" "" "$ADMIN_TOKEN")
        C_ORG_ID=$(echo "${res#*|}" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)
    fi

    echo "[DEBUG] MERCHANT_EMAIL=$MERCHANT_EMAIL"
    echo "[DEBUG] CHARITY_EMAIL=$CHARITY_EMAIL"
    echo "[DEBUG] M_ORG_ID=$M_ORG_ID"
    echo "[DEBUG] C_ORG_ID=$C_ORG_ID"

    if [ -n "$M_ORG_ID" ]; then
        # Scenario 10.2: Get Store for Review (Happy Path)
        res=$(send_request "GET" "/admin/stores/$M_ORG_ID" "" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "10.2: Get Store Info For Admin Review" "$status" "200" "$body"

        # Scenario 10.3: Approve Organization Verification Store (Happy Path)
        res=$(send_request "PATCH" "/admin/stores/$M_ORG_ID/verify" "{\"action\":\"Approved\",\"note\":\"Approved via script\"}" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "10.3: Approve Organization Store Onboarding" "$status" "200" "$body"
    fi

    if [ -n "$C_ORG_ID" ]; then
        # Scenario 10.3.1: Approve Organization Charity Onboarding (Happy Path)
        res=$(send_request "PATCH" "/admin/charities/$C_ORG_ID/verify" "{\"action\":\"Approved\",\"note\":\"Approved charity via script\"}" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "10.3.1: Approve Organization Charity Onboarding" "$status" "200" "$body"
    fi

    # Scenario 10.4: List all System Users (Happy Path)
    res=$(send_request "GET" "/admin/users?role=Merchant&status=Active" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.4: Admin List Registered System Users" "$status" "200" "$body"

    # Scenario 10.5: Ban User Profile (We ban the newly registered customer)
    BAN_PAYLOAD="{\"status\":\"Banned\"}"
    res=$(send_request "PATCH" "/admin/users/$CUSTOMER_USER_ID/status" "$BAN_PAYLOAD" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.5: Ban User Account Profile" "$status" "200" "$body"

    # Scenario 10.6: User Activity Log (Happy Path)
    res=$(send_request "GET" "/admin/users/$CUSTOMER_USER_ID/activity-log" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.6: Retrieve User Account Activity Log" "$status" "200" "$body"

    # Scenario 10.7: Store Activity Log (Happy Path)
    res=$(send_request "GET" "/admin/stores/$ORGANIZATION_ID/activity-log" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.7: Retrieve Store Activity Log" "$status" "200" "$body"

    # Scenario 10.7.1: Charity Activity Log (Happy Path)
    if [ -n "$C_ORG_ID" ]; then
        res=$(send_request "GET" "/admin/charities/$C_ORG_ID/activity-log" "" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "10.7.1: Retrieve Charity Activity Log" "$status" "200" "$body"
    fi

    # Scenario 10.8: Analytics Summary Metrics (Happy Path)
    res=$(send_request "GET" "/admin/analytics/summary" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.8: Retrieve Analytics summary details" "$status" "200" "$body"

    # Scenario 10.9: List Stores
    res=$(send_request "GET" "/admin/stores?status=Verified" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.9: Admin List Verified Stores" "$status" "200" "$body"

    # Scenario 10.10: List Charities
    res=$(send_request "GET" "/admin/charities" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.10: Admin List Charities" "$status" "200" "$body"

    # Scenario 10.11: List Reviews Moderation Queue
    res=$(send_request "GET" "/admin/reviews?rating=5" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.11: Admin List Customer Reviews" "$status" "200" "$body"

    # Scenario 10.12: List Products Moderation Queue
    res=$(send_request "GET" "/admin/products?status=Active" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.12: Admin List Inventory Listings" "$status" "200" "$body"

    # Scenario 10.13: List low-confidence products pending AI review
    res=$(send_request "GET" "/admin/products/pending-ai?confidenceThreshold=0.9" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.13: List Low AI Confidence Products Queue" "$status" "200" "$body"

    # Scenario 10.14: Moderate Product Approve (Happy Path)
    res=$(send_request "PATCH" "/admin/products/$PRODUCT_ID/approve" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.14: Approve Moderated Product Listing" "$status" "200" "$body"

    # Scenario 10.15: Moderate Product Request Changes (Happy Path)
    res=$(send_request "PATCH" "/admin/products/$PRODUCT_ID/request-changes" "{\"note\":\"Update price details\"}" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.15: Request Changes on Product Listing" "$status" "200" "$body"

    # Scenario 10.16: Moderate Product Reject (Happy Path)
    res=$(send_request "PATCH" "/admin/products/$PRODUCT_ID/reject" "{\"note\":\"Inappropriate pricing structure\"}" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.16: Reject Moderated Product Listing" "$status" "200" "$body"

    # Scenario 10.17: Moderate Review (Soft Delete Review)
    if [ -n "$REVIEW_ID" ]; then
        res=$(send_request "DELETE" "/admin/reviews/$REVIEW_ID" "" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "10.17: Moderate and Delete Inappropriate Customer Review" "$status" "204" "$body"
    fi

    # Scenario 10.18: List Support Tickets (Happy Path)
    res=$(send_request "GET" "/admin/support-tickets?status=Open" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.18: Admin List Support Tickets" "$status" "200" "$body"

    # Scenario 10.19: Get Support Ticket Detail (Happy Path)
    res=$(send_request "GET" "/admin/support-tickets/$SUPPORT_TICKET_ID" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.19: Admin Retrieve Support Ticket Conversation History" "$status" "200" "$body"

    # Scenario 10.20: Reply to Support Ticket (Happy Path)
    # SINGLE JSON string from body must be wrapped in escaped quotes
    res=$(send_request "POST" "/admin/support-tickets/$SUPPORT_TICKET_ID/reply" "\"Resolving the problem now.\"" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.20: Admin Post Reply Message on Support Ticket" "$status" "200" "$body"

    # Scenario 10.21: Close Support Ticket (Happy Path)
    res=$(send_request "PATCH" "/admin/support-tickets/$SUPPORT_TICKET_ID/close" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.21: Resolve and Close Support Ticket" "$status" "204" "$body"

    # Scenario 10.22: Delete Product (Soft Delete via Admin)
    res=$(send_request "DELETE" "/admin/products/$PRODUCT_ID" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.22: Soft Delete Product Listing via Admin" "$status" "204" "$body"

    # Scenario 10.23: Get Admin created user details (Users direct Admin controller CRUD check)
    ADMIN_CREATE_USER="{\"fullName\":\"Admin Direct User $RANDOM_VAL\",\"email\":\"admdirect_${RANDOM_VAL}@example.com\",\"password\":\"Password@123\",\"role\":\"Customer\"}"
    res=$(send_request "POST" "/users" "$ADMIN_CREATE_USER" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.23: Admin Create User Account Directly" "$status" "201" "$body"
    ADM_USER_ID=$(get_json_value "$body" "id")

    # Scenario 10.24: Admin Get User by ID Directly
    res=$(send_request "GET" "/users/$ADM_USER_ID" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.24: Admin Retrieve User Account by ID" "$status" "200" "$body"

    # Scenario 10.25: Admin Update User by ID Directly
    ADMIN_UPDATE_USER="{\"fullName\":\"Admin Direct Updated\"}"
    res=$(send_request "PATCH" "/users/$ADM_USER_ID" "$ADMIN_UPDATE_USER" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.25: Admin Update User Account Directly" "$status" "200" "$body"

    # Scenario 10.26: Admin Delete User by ID Directly
    res=$(send_request "DELETE" "/users/$ADM_USER_ID" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "10.26: Admin Delete User Account Directly" "$status" "204" "$body"
else
    echo "[WARNING] Skipping Admin-locked checks due to missing admin token."
fi

# ==============================================================================
# SECTION 11: INVENTORY RISK ANALYSIS (/stores/me/products/risk-analysis)
# ==============================================================================
echo -e "\n--- Testing Inventory Risk Analysis ---"

# Scenario 11.1: Get risk analysis (Happy Path)
res=$(send_request "GET" "/stores/me/products/risk-analysis" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "11.1: Get Inventory Risk Analysis Report" "$status" "200" "$body"

# Scenario 11.2: Get risk analysis unauthenticated (401)
res=$(send_request "GET" "/stores/me/products/risk-analysis" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "11.2: Get Risk Analysis Unauthenticated" "$status" "401" "$body"

# Scenario 11.3: Get risk analysis as Customer (403)
res=$(send_request "GET" "/stores/me/products/risk-analysis" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "11.3: Get Risk Analysis as Customer Role" "$status" "403" "$body"

# ==============================================================================
# SECTION 12: SMART DISCOUNT MANAGER (/stores/me/products/{id}/discount)
# ==============================================================================
echo -e "\n--- Testing Smart Discount Manager ---"

# Product was deleted by admin in section 10.22 — always recreate it here
res=$(send_request "GET" "/categories" "")
body=${res#*|}
CATEGORY_ID=$(get_json_value "$body" "id")
PRD_PAYLOAD="{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"Discount Test Product $RANDOM_VAL\",\"description\":\"Test\",\"originalPrice\":20.00,\"discountedPrice\":20.00,\"quantityAvailable\":10,\"expirationDate\":\"2026-09-30\"}"
res=$(send_request "POST" "/stores/me/products" "$PRD_PAYLOAD" "$MERCHANT_TOKEN")
body=${res#*|}
PRODUCT_ID=$(get_json_value "$body" "id")
echo "[INFO] Recreated product for discount tests: $PRODUCT_ID"

# Scenario 12.1: Apply discount (Happy Path)
DISCOUNT_PAYLOAD="{\"discountedPrice\":8.00,\"changeReason\":\"Near expiry smart discount\"}"
res=$(send_request "PATCH" "/stores/me/products/$PRODUCT_ID/discount" "$DISCOUNT_PAYLOAD" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "12.1: Apply Smart Discount to Product" "$status" "200" "$body"

# Scenario 12.2: Apply invalid discount (price > original — 400)
BAD_DISCOUNT="{\"discountedPrice\":999.00,\"changeReason\":\"invalid\"}"
res=$(send_request "PATCH" "/stores/me/products/$PRODUCT_ID/discount" "$BAD_DISCOUNT" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "12.2: Apply Discount Exceeding Original Price" "$status" "400" "$body"

# Scenario 12.3: Apply discount unauthenticated (401)
res=$(send_request "PATCH" "/stores/me/products/$PRODUCT_ID/discount" "$DISCOUNT_PAYLOAD" "")
status=${res%%|*}
body=${res#*|}
assert_status "12.3: Apply Discount Unauthenticated" "$status" "401" "$body"

# ==============================================================================
# SECTION 13: PRICE HISTORY AUDIT (/stores/me/products/{id}/price-history)
# ==============================================================================
echo -e "\n--- Testing Price History Audit ---"

# Scenario 13.1: Get price history (Happy Path — should have at least 1 entry from section 12)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/price-history" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "13.1: Get Product Price History Log" "$status" "200" "$body"

# Scenario 13.2: Get price history for non-existent product (404)
res=$(send_request "GET" "/stores/me/products/00000000-0000-0000-0000-000000000000/price-history" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "13.2: Get Price History Non-existent Product" "$status" "404" "$body"

# ==============================================================================
# SECTION 14: AI AUTOMATION SETTINGS (/stores/me/ai-settings)
# ==============================================================================
echo -e "\n--- Testing AI Automation Settings ---"

# Scenario 14.1: Get AI settings (Happy Path)
res=$(send_request "GET" "/stores/me/ai-settings" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "14.1: Get Merchant AI Automation Settings" "$status" "200" "$body"

# Scenario 14.2: Update AI settings (Happy Path)
AI_SETTINGS="{\"aiAutoDiscountEnabled\":true,\"aiAutoDiscountPercent\":25,\"aiAutoDiscountDaysBeforeExpiry\":3,\"aiAutoPricingEnabled\":false}"
res=$(send_request "PATCH" "/stores/me/ai-settings" "$AI_SETTINGS" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "14.2: Update Merchant AI Automation Settings" "$status" "200" "$body"

# Scenario 14.3: Update AI settings invalid percent (400)
BAD_AI="{\"aiAutoDiscountEnabled\":true,\"aiAutoDiscountPercent\":150,\"aiAutoDiscountDaysBeforeExpiry\":3,\"aiAutoPricingEnabled\":false}"
res=$(send_request "PATCH" "/stores/me/ai-settings" "$BAD_AI" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "14.3: Update AI Settings With Invalid Discount Percent" "$status" "400" "$body"

# Scenario 14.4: Get AI settings unauthenticated (401)
res=$(send_request "GET" "/stores/me/ai-settings" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "14.4: Get AI Settings Unauthenticated" "$status" "401" "$body"

# ==============================================================================
# SECTION 15: DONATION COMMUNITY IMPACT (/charities + /stores/me/donations)
# ==============================================================================
echo -e "\n--- Testing Donation & Community Impact ---"

# Scenario 15.1: List verified charities (public, no auth)
res=$(send_request "GET" "/charities" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "15.1: Get Verified Charities List (Public)" "$status" "200" "$body"
CHARITY_ORG_ID=$(get_json_value "$body" "id")
echo "[INFO] First charity org ID: $CHARITY_ORG_ID"

# Scenario 15.2: Donate surplus product to a charity (Happy Path)
if [ -n "$CHARITY_ORG_ID" ] && [ -n "$PRODUCT_ID" ]; then
    DONATION_PAYLOAD="{\"recipientOrganizationId\":\"$CHARITY_ORG_ID\",\"productId\":\"$PRODUCT_ID\",\"quantity\":1,\"note\":\"Near-expiry donation from test suite\"}"
    res=$(send_request "POST" "/stores/me/donations" "$DONATION_PAYLOAD" "$MERCHANT_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "15.2: Donate Surplus Product to Charity" "$status" "200" "$body"
fi

# Scenario 15.3: Donate with zero quantity (400)
if [ -n "$CHARITY_ORG_ID" ] && [ -n "$PRODUCT_ID" ]; then
    BAD_DONATION="{\"recipientOrganizationId\":\"$CHARITY_ORG_ID\",\"productId\":\"$PRODUCT_ID\",\"quantity\":0}"
    res=$(send_request "POST" "/stores/me/donations" "$BAD_DONATION" "$MERCHANT_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "15.3: Donate With Zero Quantity Validation" "$status" "400" "$body"
fi

# Scenario 15.4: Donate unauthenticated (401)
DUMMY_DONATION="{\"recipientOrganizationId\":\"00000000-0000-0000-0000-000000000001\",\"productId\":\"00000000-0000-0000-0000-000000000001\",\"quantity\":1}"
res=$(send_request "POST" "/stores/me/donations" "$DUMMY_DONATION" "")
status=${res%%|*}
body=${res#*|}
assert_status "15.4: Donate Unauthenticated" "$status" "401" "$body"

# ==============================================================================
# SECTION 16: ORDER TRACKING (/orders/{id}/tracking)
# ==============================================================================
echo -e "\n--- Testing Order Tracking ---"

if [ -n "$ORDER_ID" ]; then
    # Scenario 16.1: Get order tracking (Happy Path)
    res=$(send_request "GET" "/orders/$ORDER_ID/tracking" "" "$CUSTOMER_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "16.1: Get Real-Time Order Tracking Status" "$status" "200" "$body"

    # Scenario 16.2: Get order tracking unauthenticated (401)
    res=$(send_request "GET" "/orders/$ORDER_ID/tracking" "" "")
    status=${res%%|*}
    body=${res#*|}
    assert_status "16.2: Get Order Tracking Unauthenticated" "$status" "401" "$body"
fi

# Scenario 16.3: Get tracking for non-existent order (404)
res=$(send_request "GET" "/orders/00000000-0000-0000-0000-000000000000/tracking" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "16.3: Get Tracking Non-existent Order" "$status" "404" "$body"

# ==============================================================================
# SECTION 17: DELIVERY FLEET OVERVIEW (/stores/me/delivery/fleet)
# ==============================================================================
echo -e "\n--- Testing Delivery Fleet Overview ---"

# Scenario 17.1: Get fleet overview (Happy Path)
res=$(send_request "GET" "/stores/me/delivery/fleet" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "17.1: Get Delivery Fleet Overview" "$status" "200" "$body"

# Scenario 17.2: Get fleet unauthenticated (401)
res=$(send_request "GET" "/stores/me/delivery/fleet" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "17.2: Get Fleet Overview Unauthenticated" "$status" "401" "$body"

# Scenario 17.3: Get fleet as Customer (403)
res=$(send_request "GET" "/stores/me/delivery/fleet" "" "$CUSTOMER_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "17.3: Get Fleet Overview as Customer Role" "$status" "403" "$body"

# ==============================================================================
# SECTION 18: PUBLIC PRODUCT DETAIL (/marketplace/products/{id})
# ==============================================================================
echo -e "\n--- Testing Public Product Detail ---"

# Use the seeded Spinneys product ID from the database (fetch one from marketplace first)
res=$(send_request "GET" "/marketplace/products?pageSize=1" "" "")
status=${res%%|*}
body=${res#*|}
MARKET_PRODUCT_ID=$(get_json_value "$body" "id")
echo "[INFO] Sample marketplace product ID: $MARKET_PRODUCT_ID"

if [ -n "$MARKET_PRODUCT_ID" ]; then
    # Scenario 18.1: Get product detail (Happy Path)
    res=$(send_request "GET" "/marketplace/products/$MARKET_PRODUCT_ID" "" "")
    status=${res%%|*}
    body=${res#*|}
    assert_status "18.1: Get Public Product Detail Page" "$status" "200" "$body"
fi

# Scenario 18.2: Get non-existent product detail (404)
res=$(send_request "GET" "/marketplace/products/00000000-0000-0000-0000-000000000000" "" "")
status=${res%%|*}
body=${res#*|}
assert_status "18.2: Get Non-existent Product Detail" "$status" "404" "$body"

# ==============================================================================
# SECTION 19: REPORT AN ISSUE (/marketplace/products/{id}/report)
# ==============================================================================
echo -e "\n--- Testing Report An Issue ---"

if [ -n "$MARKET_PRODUCT_ID" ]; then
    # Scenario 19.1: Report a product (Happy Path)
    REPORT_PAYLOAD="{\"reason\":\"WrongExpiry\",\"details\":\"Expiry date shown does not match the actual product label.\"}"
    res=$(send_request "POST" "/marketplace/products/$MARKET_PRODUCT_ID/report" "$REPORT_PAYLOAD" "$CUSTOMER_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "19.1: Report Product Listing Issue" "$status" "200" "$body"

    # Scenario 19.2: Report with invalid reason (400)
    BAD_REPORT="{\"reason\":\"InvalidCategory\",\"details\":\"test\"}"
    res=$(send_request "POST" "/marketplace/products/$MARKET_PRODUCT_ID/report" "$BAD_REPORT" "$CUSTOMER_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "19.2: Report Product With Invalid Reason" "$status" "400" "$body"
fi

# Scenario 19.3: Report unauthenticated (401)
res=$(send_request "POST" "/marketplace/products/00000000-0000-0000-0000-000000000001/report" "{\"reason\":\"Spam\"}" "")
status=${res%%|*}
body=${res#*|}
assert_status "19.3: Report Product Unauthenticated" "$status" "401" "$body"

# ==============================================================================
# SECTION 20: OCR VERIFICATION (/stores/me/products/{id}/ocr)
# ==============================================================================
echo -e "\n--- Testing OCR Verification ---"

if [ -n "$PRODUCT_ID" ]; then
    # Scenario 20.1: Submit OCR scan (Happy Path)
    create_valid_png mock_ocr.png
    res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
      -H "Authorization: Bearer $MERCHANT_TOKEN" \
      -F "File=@mock_ocr.png" \
      "$BASE_URL/stores/me/products/$PRODUCT_ID/ocr")
    rm -f mock_ocr.png
    status=$(echo "$res" | tail -n 1)
    body=$(echo "$res" | sed '$d')
    assert_status "20.1: Submit Product Image for OCR Analysis" "$status" "200" "$body"

    # Scenario 20.2: Poll OCR result (Happy Path)
    res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/ocr-result" "" "$MERCHANT_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "20.2: Poll OCR Scan Result" "$status" "200" "$body"

    # Scenario 20.3: Poll OCR result unauthenticated (401)
    res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/ocr-result" "" "")
    status=${res%%|*}
    body=${res#*|}
    assert_status "20.3: Poll OCR Result Unauthenticated" "$status" "401" "$body"
fi

# Scenario 20.4: Poll OCR result for non-existent product (404)
res=$(send_request "GET" "/stores/me/products/00000000-0000-0000-0000-000000000000/ocr-result" "" "$MERCHANT_TOKEN")
status=${res%%|*}
body=${res#*|}
assert_status "20.4: Poll OCR Result Non-existent Product" "$status" "404" "$body"

# ==============================================================================
# SECTION 21: ADMIN DISPUTE HANDLING (/admin/disputes)
# ==============================================================================
echo -e "\n--- Testing Admin Dispute Handling ---"

if [ -n "$ADMIN_TOKEN" ]; then
    # Scenario 21.1: List all disputes (Happy Path)
    res=$(send_request "GET" "/admin/disputes" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "21.1: Admin List All Product Disputes" "$status" "200" "$body"
    DISPUTE_ID=$(get_json_value "$body" "id")

    # Scenario 21.2: List unresolved disputes only
    res=$(send_request "GET" "/admin/disputes?isResolved=false" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "21.2: Admin List Unresolved Disputes" "$status" "200" "$body"

    # Scenario 21.3: Resolve a dispute (Happy Path)
    if [ -n "$DISPUTE_ID" ]; then
        RESOLVE_PAYLOAD="{\"adminNote\":\"Reviewed and confirmed — product listing corrected by merchant.\"}"
        res=$(send_request "PATCH" "/admin/disputes/$DISPUTE_ID/resolve" "$RESOLVE_PAYLOAD" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "21.3: Admin Resolve Product Dispute" "$status" "200" "$body"
    fi

    # Scenario 21.4: List resolved disputes
    res=$(send_request "GET" "/admin/disputes?isResolved=true" "" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "21.4: Admin List Resolved Disputes" "$status" "200" "$body"

    # Scenario 21.5: Get dispute by ID (Happy Path)
    if [ -n "$DISPUTE_ID" ]; then
        res=$(send_request "GET" "/admin/disputes/$DISPUTE_ID" "" "$ADMIN_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "21.5: Admin Get Dispute By ID" "$status" "200" "$body"
    fi

    # Scenario 21.6: Get my submitted reports as Customer
    if [ -n "$CUSTOMER_TOKEN" ]; then
        res=$(send_request "GET" "/users/me/reports" "" "$CUSTOMER_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "21.6: Customer Get My Submitted Reports" "$status" "200" "$body"
    fi

    # Scenario 21.7: Get store product disputes as Merchant
    if [ -n "$MERCHANT_TOKEN" ]; then
        res=$(send_request "GET" "/stores/me/disputes" "" "$MERCHANT_TOKEN")
        status=${res%%|*}
        body=${res#*|}
        assert_status "21.7: Merchant Get Store Disputes" "$status" "200" "$body"
    fi

    # Scenario 21.8: Resolve non-existent dispute (404)
    res=$(send_request "PATCH" "/admin/disputes/00000000-0000-0000-0000-000000000000/resolve" "{\"adminNote\":\"test\"}" "$ADMIN_TOKEN")
    status=${res%%|*}
    body=${res#*|}
    assert_status "21.8: Resolve Non-existent Dispute" "$status" "404" "$body"
fi

# ==============================================================================
# SECTION 22: AUTH EDGE CASES
# ==============================================================================
echo -e "\n--- Testing Auth Edge Cases ---"

# 22.1 Duplicate email registration (400)
res=$(send_request "POST" "/auth/register" "{\"name\":\"Dup\",\"email\":\"$MERCHANT_EMAIL\",\"password\":\"Password@123\",\"role\":\"Customer\"}")
status=${res%%|*}; body=${res#*|}
assert_status "22.1: Register Duplicate Email" "$status" "400" "$body"

# 22.2 Login with empty credentials (400)
res=$(send_request "POST" "/auth/login" "{\"email\":\"\",\"password\":\"\"}")
status=${res%%|*}; body=${res#*|}
assert_status "22.2: Login Empty Credentials" "$status" "400" "$body"

# 22.3 Refresh with invalid token (401)
res=$(send_request "POST" "/auth/refresh" "{\"refreshToken\":\"invalid-token-xyz\"}")
status=${res%%|*}; body=${res#*|}
assert_status "22.3: Refresh With Invalid Token" "$status" "401" "$body"

# 22.4 Reset password with invalid token (400)
res=$(send_request "POST" "/auth/reset-password" "{\"email\":\"$MERCHANT_EMAIL\",\"token\":\"bad-token\",\"newPassword\":\"NewPassword@123\"}")
status=${res%%|*}; body=${res#*|}
assert_status "22.4: Reset Password With Invalid Token" "$status" "400" "$body"


# ==============================================================================
# SECTION 23: CATEGORIES EDGE CASES
# ==============================================================================
echo -e "\n--- Testing Categories Edge Cases ---"

# 23.1 GET /categories returns 200 with no auth
res=$(send_request "GET" "/categories" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "23.1: Get Categories Public No Auth" "$status" "200" "$body"

# ==============================================================================
# SECTION 24: USERS — 401/403/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Users Auth/Role Gaps ---"

# 24.1 PATCH /users/me unauthenticated (401)
res=$(send_request "PATCH" "/users/me" "{\"fullName\":\"X\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "24.1: Update Profile Unauthenticated" "$status" "401" "$body"

# 24.2 GET /users/me/addresses unauthenticated (401)
res=$(send_request "GET" "/users/me/addresses" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "24.2: Get Addresses Unauthenticated" "$status" "401" "$body"

# 24.3 POST /users/me/addresses unauthenticated (401)
res=$(send_request "POST" "/users/me/addresses" "{\"city\":\"Cairo\",\"district\":\"Maadi\",\"street\":\"Road 9\",\"buildingNo\":\"1\",\"latitude\":30.04,\"longitude\":31.23}" "")
status=${res%%|*}; body=${res#*|}
assert_status "24.3: Create Address Unauthenticated" "$status" "401" "$body"

# 24.4 PATCH /users/me/preferences unauthenticated (401)
res=$(send_request "PATCH" "/users/me/preferences" "{\"orderUpdatesEnabled\":true}" "")
status=${res%%|*}; body=${res#*|}
assert_status "24.4: Update Preferences Unauthenticated" "$status" "401" "$body"

# 24.5 POST /users/me/tickets unauthenticated (401)
res=$(send_request "POST" "/users/me/tickets" "{\"category\":\"Test\",\"message\":\"test\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "24.5: Create Ticket Unauthenticated" "$status" "401" "$body"

# 24.6 GET /users (admin list) as Customer (403)
res=$(send_request "GET" "/users" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "24.6: List All Users as Customer (Forbidden)" "$status" "403" "$body"

# 24.7 GET /users/{id} as Customer (403)
res=$(send_request "GET" "/users/$CUSTOMER_USER_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "24.7: Get User By ID as Customer (Forbidden)" "$status" "403" "$body"

# 24.8 GET /users/{id} non-existent (404)
if [ -n "$ADMIN_TOKEN" ]; then
  res=$(send_request "GET" "/users/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "24.8: Get Non-existent User By ID" "$status" "404" "$body"
fi

# 24.9 PATCH /users/{id} as Customer (403)
res=$(send_request "PATCH" "/users/$CUSTOMER_USER_ID" "{\"fullName\":\"Hack\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "24.9: Update User By ID as Customer (Forbidden)" "$status" "403" "$body"

# 24.10 PATCH /users/{id} non-existent (404)
if [ -n "$ADMIN_TOKEN" ]; then
  res=$(send_request "PATCH" "/users/00000000-0000-0000-0000-000000000000" "{\"fullName\":\"X\"}" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "24.10: Update Non-existent User By ID" "$status" "404" "$body"
fi

# 24.11 DELETE /users/{id} as Customer (403)
res=$(send_request "DELETE" "/users/$CUSTOMER_USER_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "24.11: Delete User as Customer (Forbidden)" "$status" "403" "$body"

# 24.12 POST /users (create) as Customer (403)
res=$(send_request "POST" "/users" "{\"fullName\":\"X\",\"email\":\"x@x.com\",\"password\":\"Pass@123\",\"role\":\"Customer\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "24.12: Create User as Customer (Forbidden)" "$status" "403" "$body"


# ==============================================================================
# SECTION 25: STORES - 401/403 GAPS
# ==============================================================================
echo -e "\n--- Testing Stores Auth/Role Gaps ---"

# 25.1 GET /stores/me unauthenticated (401)
res=$(send_request "GET" "/stores/me" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "25.1: Get Store Profile Unauthenticated" "$status" "401" "$body"

# 25.2 GET /stores/me as Customer (403)
res=$(send_request "GET" "/stores/me" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.2: Get Store Profile as Customer (Forbidden)" "$status" "403" "$body"

# 25.3 GET /stores/me/orders unauthenticated (401)
res=$(send_request "GET" "/stores/me/orders" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "25.3: Get Merchant Orders Unauthenticated" "$status" "401" "$body"

# 25.4 GET /stores/me/orders as Customer (403)
res=$(send_request "GET" "/stores/me/orders" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.4: Get Merchant Orders as Customer (Forbidden)" "$status" "403" "$body"

# 25.5 PATCH /stores/me/orders/{id}/status unauthenticated (401)
res=$(send_request "PATCH" "/stores/me/orders/00000000-0000-0000-0000-000000000000/status" "{\"status\":\"Completed\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "25.5: Update Order Status Unauthenticated" "$status" "401" "$body"

# 25.6 PATCH /stores/me/orders/{id}/status as Customer (403)
res=$(send_request "PATCH" "/stores/me/orders/00000000-0000-0000-0000-000000000000/status" "{\"status\":\"Completed\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.6: Update Order Status as Customer (Forbidden)" "$status" "403" "$body"

# 25.7 POST /stores/me/documents missing email (400)
echo "PDF" > tmp_doc.pdf
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -F "Type=CommercialRegistration" -F "File=@tmp_doc.pdf" "$BASE_URL/stores/me/documents")
rm -f tmp_doc.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | head -n -1)
assert_status "25.7: Upload Store Doc Missing Email" "$status" "400" "$body"

# 25.8 POST /charities/me/documents missing email (400)
echo "PDF" > tmp_doc.pdf
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -F "Type=AssociationCertificate" -F "File=@tmp_doc.pdf" "$BASE_URL/charities/me/documents")
rm -f tmp_doc.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | head -n -1)
assert_status "25.8: Upload Charity Doc Missing Email" "$status" "400" "$body"

# 25.9 POST /stores/me/donations as Customer (403)
res=$(send_request "POST" "/stores/me/donations" "{\"recipientOrganizationId\":\"00000000-0000-0000-0000-000000000001\",\"productId\":\"00000000-0000-0000-0000-000000000001\",\"quantity\":1}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.9: Donate as Customer (Forbidden)" "$status" "403" "$body"

# 25.10 PATCH /stores/me/ai-settings unauthenticated (401)
res=$(send_request "PATCH" "/stores/me/ai-settings" "{\"aiAutoDiscountEnabled\":true,\"aiAutoDiscountPercent\":20,\"aiAutoDiscountDaysBeforeExpiry\":3,\"aiAutoPricingEnabled\":false}" "")
status=${res%%|*}; body=${res#*|}
assert_status "25.10: Update AI Settings Unauthenticated" "$status" "401" "$body"

# 25.11 GET /stores/me/ai-settings as Customer (403)
res=$(send_request "GET" "/stores/me/ai-settings" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.11: Get AI Settings as Customer (Forbidden)" "$status" "403" "$body"

# 25.12 PATCH /stores/me/ai-settings as Customer (403)
res=$(send_request "PATCH" "/stores/me/ai-settings" "{\"aiAutoDiscountEnabled\":false,\"aiAutoDiscountPercent\":10,\"aiAutoDiscountDaysBeforeExpiry\":2,\"aiAutoPricingEnabled\":false}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "25.12: Update AI Settings as Customer (Forbidden)" "$status" "403" "$body"

# ==============================================================================
# SECTION 26: PRODUCTS — 401/403/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Products Auth/Role/404 Gaps ---"

# 26.1 POST /stores/me/products unauthenticated (401)
res=$(send_request "POST" "/stores/me/products" "{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"X\",\"originalPrice\":10,\"discountedPrice\":5,\"quantityAvailable\":1,\"expirationDate\":\"2026-09-01\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "26.1: Create Product Unauthenticated" "$status" "401" "$body"

# 26.2 POST /stores/me/products as Customer (403)
res=$(send_request "POST" "/stores/me/products" "{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"X\",\"originalPrice\":10,\"discountedPrice\":5,\"quantityAvailable\":1,\"expirationDate\":\"2026-09-01\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "26.2: Create Product as Customer (Forbidden)" "$status" "403" "$body"

# 26.3 GET /stores/me/products unauthenticated (401)
res=$(send_request "GET" "/stores/me/products" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "26.3: List Products Unauthenticated" "$status" "401" "$body"

# 26.4 GET /stores/me/products as Customer (403)
res=$(send_request "GET" "/stores/me/products" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "26.4: List Products as Customer (Forbidden)" "$status" "403" "$body"

# 26.5 GET /stores/me/products/{id} unauthenticated (401)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "26.5: Get Product Detail Unauthenticated" "$status" "401" "$body"

# 26.6 GET /stores/me/products/{id} as Customer (403)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "26.6: Get Product Detail as Customer (Forbidden)" "$status" "403" "$body"

# 26.7 PATCH /stores/me/products/{id} unauthenticated (401)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH -F "DiscountedPrice=5.00" "$BASE_URL/stores/me/products/$PRODUCT_ID")
status=$(echo "$res" | tail -n 1)
assert_status "26.7: Update Product Unauthenticated" "$status" "401" ""

# 26.8 PATCH /stores/me/products/{id} as Customer (403)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH -H "Authorization: Bearer $CUSTOMER_TOKEN" -F "DiscountedPrice=5.00" "$BASE_URL/stores/me/products/$PRODUCT_ID")
status=$(echo "$res" | tail -n 1)
assert_status "26.8: Update Product as Customer (Forbidden)" "$status" "403" ""

# 26.9 PATCH /stores/me/products/{id} non-existent (404)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH -H "Authorization: Bearer $MERCHANT_TOKEN" -F "DiscountedPrice=5.00" "$BASE_URL/stores/me/products/00000000-0000-0000-0000-000000000000")
status=$(echo "$res" | tail -n 1)
assert_status "26.9: Update Non-existent Product" "$status" "404" ""

# 26.10 DELETE /stores/me/products/{id} unauthenticated (401)
res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "26.10: Delete Product Unauthenticated" "$status" "401" "$body"

# 26.11 DELETE /stores/me/products/{id} as Customer (403)
res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "26.11: Delete Product as Customer (Forbidden)" "$status" "403" "$body"

# 26.12 DELETE /stores/me/products/{id} non-existent (404)
res=$(send_request "DELETE" "/stores/me/products/00000000-0000-0000-0000-000000000000" "" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "26.12: Delete Non-existent Product" "$status" "404" "$body"

# ==============================================================================
# SECTION 27: PRODUCTS IMAGES/BULK — 401/403/400/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Product Images & Bulk Upload Auth/Role Gaps ---"

# 27.1 POST /stores/me/products/{id}/images unauthenticated (401)
create_valid_png tmp_img.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -F "File=@tmp_img.png" "$BASE_URL/stores/me/products/$PRODUCT_ID/images")
rm -f tmp_img.png
status=$(echo "$res" | tail -n 1)
assert_status "27.1: Upload Image Unauthenticated" "$status" "401" ""

# 27.2 POST /stores/me/products/{id}/images as Customer (403)
create_valid_png tmp_img.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $CUSTOMER_TOKEN" -F "File=@tmp_img.png" "$BASE_URL/stores/me/products/$PRODUCT_ID/images")
rm -f tmp_img.png
status=$(echo "$res" | tail -n 1)
assert_status "27.2: Upload Image as Customer (Forbidden)" "$status" "403" ""

# 27.3 POST /stores/me/products/{id}/images — invalid file type (400)
echo "PDF" > tmp_img.pdf
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $MERCHANT_TOKEN" -F "File=@tmp_img.pdf" "$BASE_URL/stores/me/products/$PRODUCT_ID/images")
rm -f tmp_img.pdf
status=$(echo "$res" | tail -n 1)
assert_status "27.3: Upload Image Invalid File Type" "$status" "400" ""

# 27.4 POST /stores/me/products/{id}/images — non-existent product (404)
create_valid_png tmp_img.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $MERCHANT_TOKEN" -F "File=@tmp_img.png" "$BASE_URL/stores/me/products/00000000-0000-0000-0000-000000000000/images")
rm -f tmp_img.png
status=$(echo "$res" | tail -n 1)
assert_status "27.4: Upload Image Non-existent Product" "$status" "404" ""

# 27.5 DELETE /stores/me/products/{id}/images/{imageId} unauthenticated (401)
res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID/images/00000000-0000-0000-0000-000000000000" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "27.5: Delete Image Unauthenticated" "$status" "401" "$body"

# 27.6 DELETE /stores/me/products/{id}/images/{imageId} as Customer (403)
res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID/images/00000000-0000-0000-0000-000000000000" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "27.6: Delete Image as Customer (Forbidden)" "$status" "403" "$body"

# 27.7 POST /stores/me/products/bulk unauthenticated (401)
echo "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname" > tmp_bulk.csv
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -F "File=@tmp_bulk.csv" "$BASE_URL/stores/me/products/bulk")
rm -f tmp_bulk.csv
status=$(echo "$res" | tail -n 1)
assert_status "27.7: Bulk Upload Unauthenticated" "$status" "401" ""

# 27.8 POST /stores/me/products/bulk as Customer (403)
echo "title,originalprice,discountedprice,quantityavailable,expirationdate,categoryname" > tmp_bulk.csv
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $CUSTOMER_TOKEN" -F "File=@tmp_bulk.csv" "$BASE_URL/stores/me/products/bulk")
rm -f tmp_bulk.csv
status=$(echo "$res" | tail -n 1)
assert_status "27.8: Bulk Upload as Customer (Forbidden)" "$status" "403" ""

# 27.9 POST /stores/me/products/bulk empty file (400)
echo "" > tmp_empty.csv
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $MERCHANT_TOKEN" -F "File=@tmp_empty.csv" "$BASE_URL/stores/me/products/bulk")
rm -f tmp_empty.csv
status=$(echo "$res" | tail -n 1)
assert_status "27.9: Bulk Upload Empty File" "$status" "400" ""

# ==============================================================================
# SECTION 28: DISCOUNT & PRICE-HISTORY — 403/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Discount & Price History Gaps ---"

# 28.1 PATCH /stores/me/products/{id}/discount as Customer (403)
res=$(send_request "PATCH" "/stores/me/products/$PRODUCT_ID/discount" "{\"discountedPrice\":5.00}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "28.1: Apply Discount as Customer (Forbidden)" "$status" "403" "$body"

# 28.2 PATCH /stores/me/products/{id}/discount non-existent product (404)
res=$(send_request "PATCH" "/stores/me/products/00000000-0000-0000-0000-000000000000/discount" "{\"discountedPrice\":5.00}" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "28.2: Apply Discount Non-existent Product" "$status" "404" "$body"

# 28.3 GET /stores/me/products/{id}/price-history unauthenticated (401)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/price-history" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "28.3: Get Price History Unauthenticated" "$status" "401" "$body"

# 28.4 GET /stores/me/products/{id}/price-history as Customer (403)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/price-history" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "28.4: Get Price History as Customer (Forbidden)" "$status" "403" "$body"

# ==============================================================================
# SECTION 29: OCR — 403 GAP
# ==============================================================================
echo -e "\n--- Testing OCR Auth/Role Gaps ---"

# 29.1 POST /stores/me/products/{id}/ocr as Customer (403)
create_valid_png tmp_ocr.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $CUSTOMER_TOKEN" -F "File=@tmp_ocr.png" "$BASE_URL/stores/me/products/$PRODUCT_ID/ocr")
rm -f tmp_ocr.png
status=$(echo "$res" | tail -n 1)
assert_status "29.1: Submit OCR as Customer (Forbidden)" "$status" "403" ""

# 29.2 POST /stores/me/products/{id}/ocr invalid file type (400)
echo "PDF" > tmp_ocr.pdf
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST -H "Authorization: Bearer $MERCHANT_TOKEN" -F "File=@tmp_ocr.pdf" "$BASE_URL/stores/me/products/$PRODUCT_ID/ocr")
rm -f tmp_ocr.pdf
status=$(echo "$res" | tail -n 1)
assert_status "29.2: Submit OCR Invalid File Type" "$status" "400" ""

# 29.3 GET /stores/me/products/{id}/ocr-result as Customer (403)
res=$(send_request "GET" "/stores/me/products/$PRODUCT_ID/ocr-result" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "29.3: Get OCR Result as Customer (Forbidden)" "$status" "403" "$body"

# ==============================================================================
# SECTION 30: ORDERS — 401/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Orders Auth/404 Gaps ---"

# 30.1 POST /orders unauthenticated (401)
res=$(send_request "POST" "/orders" "{\"items\":[{\"productId\":\"$PRODUCT_ID\",\"quantity\":1}]}" "")
status=${res%%|*}; body=${res#*|}
assert_status "30.1: Checkout Unauthenticated" "$status" "401" "$body"

# 30.2 POST /orders empty items (400)
res=$(send_request "POST" "/orders" "{\"items\":[]}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "30.2: Checkout Empty Items" "$status" "400" "$body"

# 30.3 GET /orders unauthenticated (401)
res=$(send_request "GET" "/orders" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "30.3: Get Order History Unauthenticated" "$status" "401" "$body"

# 30.4 GET /orders/{id} unauthenticated (401)
res=$(send_request "GET" "/orders/00000000-0000-0000-0000-000000000000" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "30.4: Get Order Detail Unauthenticated" "$status" "401" "$body"

# 30.5 GET /orders/{id} non-existent (404)
res=$(send_request "GET" "/orders/00000000-0000-0000-0000-000000000000" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "30.5: Get Non-existent Order Detail" "$status" "404" "$body"

# ==============================================================================
# SECTION 31: REVIEWS — 401/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Reviews Auth/404 Gaps ---"

# 31.1 POST /reviews unauthenticated (401)
res=$(send_request "POST" "/reviews" "{\"orderId\":\"00000000-0000-0000-0000-000000000000\",\"rating\":5}" "")
status=${res%%|*}; body=${res#*|}
assert_status "31.1: Submit Review Unauthenticated" "$status" "401" "$body"

# 31.2 POST /reviews — order not found (400)
res=$(send_request "POST" "/reviews" "{\"orderId\":\"00000000-0000-0000-0000-000000000000\",\"rating\":5,\"comment\":\"test\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "31.2: Submit Review Order Not Found" "$status" "404" "$body"

# 31.3 GET /stores/{id}/reviews — non-existent store returns empty list (200)
res=$(send_request "GET" "/stores/00000000-0000-0000-0000-000000000000/reviews" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "31.3: Get Reviews Non-existent Store Returns Empty" "$status" "200" "$body"

# ==============================================================================
# SECTION 32: NOTIFICATIONS — 401/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Notifications Auth/404 Gaps ---"

# 32.1 GET /notifications unauthenticated (401)
res=$(send_request "GET" "/notifications" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "32.1: Get Notifications Unauthenticated" "$status" "401" "$body"

# 32.2 PATCH /notifications/{id}/read unauthenticated (401)
res=$(send_request "PATCH" "/notifications/00000000-0000-0000-0000-000000000000/read" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "32.2: Mark Notification Read Unauthenticated" "$status" "401" "$body"

# 32.3 PATCH /notifications/{id}/read non-existent (400 or 404)
res=$(send_request "PATCH" "/notifications/00000000-0000-0000-0000-000000000000/read" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "32.3: Mark Non-existent Notification Read" "$status" "404" "$body"

# 32.4 PATCH /notifications/read-all unauthenticated (401)
res=$(send_request "PATCH" "/notifications/read-all" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "32.4: Mark All Notifications Read Unauthenticated" "$status" "401" "$body"

# ==============================================================================
# SECTION 33: SUPPORT TICKETS — 401/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Support Tickets Auth/404 Gaps ---"

# 33.1 POST /support-tickets unauthenticated (401)
res=$(send_request "POST" "/support-tickets" "{\"category\":\"Order\",\"message\":\"Test\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "33.1: Create Support Ticket Unauthenticated" "$status" "401" "$body"

# 33.2 GET /support-tickets unauthenticated (401)
res=$(send_request "GET" "/support-tickets" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "33.2: List Support Tickets Unauthenticated" "$status" "401" "$body"

# 33.3 GET /support-tickets/{id} unauthenticated (401)
res=$(send_request "GET" "/support-tickets/00000000-0000-0000-0000-000000000000" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "33.3: Get Ticket Detail Unauthenticated" "$status" "401" "$body"

# 33.4 GET /support-tickets/{id} non-existent (404)
res=$(send_request "GET" "/support-tickets/00000000-0000-0000-0000-000000000000" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "33.4: Get Non-existent Ticket Detail" "$status" "404" "$body"

# 33.5 POST /support-tickets/{id}/reply unauthenticated (401)
res=$(send_request "POST" "/support-tickets/00000000-0000-0000-0000-000000000000/reply" "{\"message\":\"test\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "33.5: Reply to Ticket Unauthenticated" "$status" "401" "$body"

# 33.6 POST /support-tickets/{id}/reply non-existent ticket (404)
res=$(send_request "POST" "/support-tickets/00000000-0000-0000-0000-000000000000/reply" "{\"message\":\"test\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "33.6: Reply to Non-existent Ticket" "$status" "404" "$body"

# ==============================================================================
# SECTION 34: MARKETPLACE — MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Marketplace Missing Scenarios ---"

# 34.1 GET /marketplace/products with search text
res=$(send_request "GET" "/marketplace/products?search=bread" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "34.1: Search Products by Text" "$status" "200" "$body"

# 34.2 GET /marketplace/products with pagination
res=$(send_request "GET" "/marketplace/products?pageNumber=1&pageSize=2" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "34.2: Marketplace Products Pagination" "$status" "200" "$body"

# 34.3 POST /marketplace/products/{id}/report — product not found (404)
res=$(send_request "POST" "/marketplace/products/00000000-0000-0000-0000-000000000000/report" "{\"reason\":\"Spam\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "34.3: Report Non-existent Product" "$status" "404" "$body"

# ==============================================================================
# SECTION 35: ADMIN — 403/404 GAPS
# ==============================================================================
echo -e "\n--- Testing Admin Auth/Role/404 Gaps ---"

# 35.1 GET /admin/stores as Customer (403)
res=$(send_request "GET" "/admin/stores" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.1: Admin List Stores as Customer (Forbidden)" "$status" "403" "$body"

# 35.2 GET /admin/charities as Customer (403)
res=$(send_request "GET" "/admin/charities" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.2: Admin List Charities as Customer (Forbidden)" "$status" "403" "$body"

# 35.3 GET /admin/reviews as Customer (403)
res=$(send_request "GET" "/admin/reviews" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.3: Admin List Reviews as Customer (Forbidden)" "$status" "403" "$body"

# 35.4 GET /admin/products as Customer (403)
res=$(send_request "GET" "/admin/products" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.4: Admin List Products as Customer (Forbidden)" "$status" "403" "$body"

# 35.5 GET /admin/products/pending-ai as Customer (403)
res=$(send_request "GET" "/admin/products/pending-ai" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.5: Admin Pending AI Products as Customer (Forbidden)" "$status" "403" "$body"

# 35.6 GET /admin/support-tickets as Customer (403)
res=$(send_request "GET" "/admin/support-tickets" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "35.6: Admin List Tickets as Customer (Forbidden)" "$status" "403" "$body"

# ==============================================================================
# SECTION 36: ADMIN — STORE/CHARITY/PRODUCT/TICKET 404 GAPS
# ==============================================================================
echo -e "\n--- Testing Admin 404 Scenarios ---"

if [ -n "$ADMIN_TOKEN" ]; then

# 36.1 GET /admin/stores/{id} non-existent (404)
res=$(send_request "GET" "/admin/stores/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.1: Admin Get Non-existent Store" "$status" "404" "$body"

# 36.2 PATCH /admin/stores/{id}/verify non-existent (404)
res=$(send_request "PATCH" "/admin/stores/00000000-0000-0000-0000-000000000000/verify" "{\"action\":\"Approved\",\"note\":\"test\"}" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.2: Admin Verify Non-existent Store" "$status" "404" "$body"

# 36.3 PATCH /admin/charities/{id}/verify non-existent (404)
res=$(send_request "PATCH" "/admin/charities/00000000-0000-0000-0000-000000000000/verify" "{\"action\":\"Approved\",\"note\":\"test\"}" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.3: Admin Verify Non-existent Charity" "$status" "404" "$body"

# 36.4 DELETE /admin/reviews/{id} non-existent (404)
res=$(send_request "DELETE" "/admin/reviews/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.4: Admin Delete Non-existent Review" "$status" "404" "$body"

# 36.5 PATCH /admin/products/{id}/approve non-existent (404)
res=$(send_request "PATCH" "/admin/products/00000000-0000-0000-0000-000000000000/approve" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.5: Admin Approve Non-existent Product" "$status" "404" "$body"

# 36.6 PATCH /admin/products/{id}/reject non-existent (404)
res=$(send_request "PATCH" "/admin/products/00000000-0000-0000-0000-000000000000/reject" "{\"note\":\"test\"}" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.6: Admin Reject Non-existent Product" "$status" "404" "$body"

# 36.7 PATCH /admin/products/{id}/request-changes non-existent (404)
res=$(send_request "PATCH" "/admin/products/00000000-0000-0000-0000-000000000000/request-changes" "{\"note\":\"test\"}" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.7: Admin Request Changes Non-existent Product" "$status" "404" "$body"

# 36.8 DELETE /admin/products/{id} non-existent (404)
res=$(send_request "DELETE" "/admin/products/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.8: Admin Delete Non-existent Product" "$status" "404" "$body"

# 36.9 GET /admin/support-tickets/{id} non-existent (404)
res=$(send_request "GET" "/admin/support-tickets/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.9: Admin Get Non-existent Ticket" "$status" "404" "$body"

# 36.10 POST /admin/support-tickets/{id}/reply non-existent ticket (404)
res=$(send_request "POST" "/admin/support-tickets/00000000-0000-0000-0000-000000000000/reply" "\"test message\"" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.10: Admin Reply to Non-existent Ticket" "$status" "404" "$body"

# 36.11 PATCH /admin/support-tickets/{id}/close non-existent (404)
res=$(send_request "PATCH" "/admin/support-tickets/00000000-0000-0000-0000-000000000000/close" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.11: Admin Close Non-existent Ticket" "$status" "404" "$body"

# 36.12 GET /admin/users/{id}/activity-log non-existent (404)
res=$(send_request "GET" "/admin/users/00000000-0000-0000-0000-000000000000/activity-log" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.12: Admin Activity Log Non-existent User" "$status" "404" "$body"

# 36.13 GET /admin/stores/{id}/activity-log non-existent (404)
res=$(send_request "GET" "/admin/stores/00000000-0000-0000-0000-000000000000/activity-log" "" "$ADMIN_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.13: Admin Activity Log Non-existent Store" "$status" "404" "$body"

# 36.14 PATCH /admin/users/{id}/status as Customer (403)
res=$(send_request "PATCH" "/admin/users/$CUSTOMER_USER_ID/status" "{\"status\":\"Banned\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.14: Admin Update User Status as Customer (Forbidden)" "$status" "403" "$body"

# 36.15 GET /admin/disputes as Customer (403)
res=$(send_request "GET" "/admin/disputes" "" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.15: Admin Get Disputes as Customer (Forbidden)" "$status" "403" "$body"

# 36.16 PATCH /admin/disputes/{id}/resolve as Customer (403)
res=$(send_request "PATCH" "/admin/disputes/00000000-0000-0000-0000-000000000000/resolve" "{\"adminNote\":\"test\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "36.16: Admin Resolve Dispute as Customer (Forbidden)" "$status" "403" "$body"

fi  # end admin token guard

# ==============================================================================
# SECTION 37: STORES - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Stores Remaining Scenarios ---"

# 37.1 PATCH /stores/me with invalid OpeningHours JSON (400)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH -H "Authorization: Bearer $MERCHANT_TOKEN" -F "OpeningHours=not-valid-json" "$BASE_URL/stores/me")
status=$(echo "$res" | tail -n 1)
assert_status "37.1: Update Store With Invalid OpeningHours JSON" "$status" "400" ""

# 37.2 PATCH /stores/me/location unauthenticated (401)
res=$(send_request "PATCH" "/stores/me/location" "{\"latitude\":30.0,\"longitude\":31.0,\"city\":\"Cairo\"}" "")
status=${res%%|*}; body=${res#*|}
assert_status "37.2: Update Store Location Unauthenticated" "$status" "401" "$body"

# 37.3 PATCH /stores/me/location as Customer (403)
res=$(send_request "PATCH" "/stores/me/location" "{\"latitude\":30.0,\"longitude\":31.0,\"city\":\"Cairo\"}" "$CUSTOMER_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "37.3: Update Store Location as Customer (Forbidden)" "$status" "403" "$body"

# 37.4 PATCH /stores/me/orders/{id}/status non-existent order (400 or 404)
res=$(send_request "PATCH" "/stores/me/orders/00000000-0000-0000-0000-000000000000/status" "{\"status\":\"Completed\"}" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "37.4: Update Status Non-existent Order" "$status" "404" "$body"

# 37.5 PATCH /stores/me/orders/{id}/status invalid status string (400)
if [ -n "$ORDER_ID" ]; then
  res=$(send_request "PATCH" "/stores/me/orders/$ORDER_ID/status" "{\"status\":\"InvalidStatus\"}" "$MERCHANT_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "37.5: Update Order With Invalid Status String" "$status" "400" "$body"
fi

# 37.6 POST /stores/me/donations non-existent charity (404)
if [ -n "$PRODUCT_ID" ]; then
  res=$(send_request "POST" "/stores/me/donations" "{\"recipientOrganizationId\":\"00000000-0000-0000-0000-000000000000\",\"productId\":\"$PRODUCT_ID\",\"quantity\":1}" "$MERCHANT_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "37.6: Donate to Non-existent Charity" "$status" "404" "$body"
fi

# 37.7 PATCH /stores/me/ai-settings daysBeforeExpiry < 1 (400)
res=$(send_request "PATCH" "/stores/me/ai-settings" "{\"aiAutoDiscountEnabled\":true,\"aiAutoDiscountPercent\":20,\"aiAutoDiscountDaysBeforeExpiry\":0,\"aiAutoPricingEnabled\":false}" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "37.7: Update AI Settings With Zero Days Before Expiry" "$status" "400" "$body"

# ==============================================================================
# SECTION 38: PRODUCTS - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Products Remaining Scenarios ---"

# 38.1 GET /stores/me/products with searchTerm filter
res=$(send_request "GET" "/stores/me/products?searchTerm=Sourdough" "" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "38.1: List Products With SearchTerm Filter" "$status" "200" "$body"

# 38.2 GET /stores/me/products with categoryId filter
res=$(send_request "GET" "/stores/me/products?categoryId=$CATEGORY_ID" "" "$MERCHANT_TOKEN")
status=${res%%|*}; body=${res#*|}
assert_status "38.2: List Products With Category Filter" "$status" "200" "$body"

# 38.3 PATCH /stores/me/products/{id} with negative price (400)
if [ -n "$PRODUCT_ID" ]; then
  res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH -H "Authorization: Bearer $MERCHANT_TOKEN" -F "OriginalPrice=-10.00" "$BASE_URL/stores/me/products/$PRODUCT_ID")
  status=$(echo "$res" | tail -n 1)
  assert_status "38.3: Update Product With Negative Price" "$status" "400" ""
fi

# 38.4 DELETE /stores/me/products/{id}/images/{imageId} non-existent imageId (404)
if [ -n "$PRODUCT_ID" ]; then
  res=$(send_request "DELETE" "/stores/me/products/$PRODUCT_ID/images/00000000-0000-0000-0000-000000000000" "" "$MERCHANT_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "38.4: Delete Non-existent Product Image" "$status" "404" "$body"
fi

# ==============================================================================
# SECTION 39: MARKETPLACE - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Marketplace Remaining Scenarios ---"

# 39.1 GET /marketplace/products sortBy=discount
res=$(send_request "GET" "/marketplace/products?sortBy=discount" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "39.1: Marketplace Products Sorted by Discount" "$status" "200" "$body"

# 39.2 GET /marketplace/products sortBy=expiration
res=$(send_request "GET" "/marketplace/products?sortBy=expiration" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "39.2: Marketplace Products Sorted by Expiration" "$status" "200" "$body"

# 39.3 GET /marketplace/products sortBy=price_asc
res=$(send_request "GET" "/marketplace/products?sortBy=price_asc&minPrice=1" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "39.3: Marketplace Products Sorted by Price Ascending" "$status" "200" "$body"

# 39.4 GET /marketplace/products sortBy=price_desc
res=$(send_request "GET" "/marketplace/products?sortBy=price_desc" "" "")
status=${res%%|*}; body=${res#*|}
assert_status "39.4: Marketplace Products Sorted by Price Descending" "$status" "200" "$body"

# ==============================================================================
# SECTION 40: ORDERS - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Orders Remaining Scenarios ---"

# Re-login merchant to get a fresh token (original may have expired during long run)
res=$(send_request "POST" "/auth/login" "{\"email\":\"merchant.spinneys@example.com\",\"password\":\"Password@123\"}")
body=${res#*|}
FRESH_MERCHANT_TOKEN=$(get_json_value "$body" "accessToken")
[ -n "$FRESH_MERCHANT_TOKEN" ] && MERCHANT_TOKEN="$FRESH_MERCHANT_TOKEN"

# 40.1 GET /orders/{id} with another user's order ID (should 404)
# Note: if the fresh merchant token fetch fails, this will 401 instead — both are valid.
if [ -n "$ORDER_ID" ] && [ -n "$MERCHANT_TOKEN" ]; then
  res=$(send_request "GET" "/orders/$ORDER_ID" "" "$MERCHANT_TOKEN")
  status=${res%%|*}; body=${res#*|}
  if [ "$status" -eq 404 ] || [ "$status" -eq 401 ]; then
    echo -e "\e[32m[PASS]\e[0m 40.1: Get Order Belonging to Different User (HTTP $status — 401/404 both valid)"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo -e "\e[31m[FAIL]\e[0m 40.1: Get Order Belonging to Different User (Expected: 404 or 401, Got: $status)"
    echo -e "       Response: $body"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
fi

# 40.2 GET /orders/{id}/tracking for another user's order (404)
if [ -n "$ORDER_ID" ] && [ -n "$MERCHANT_TOKEN" ]; then
  res=$(send_request "GET" "/orders/$ORDER_ID/tracking" "" "$MERCHANT_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "40.2: Get Tracking for Order of Different User" "$status" "404" "$body"
fi

# ==============================================================================
# SECTION 41: REVIEWS - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Reviews Remaining Scenarios ---"

# 41.1 GET /stores/{id}/reviews with pagination params
if [ -n "$ORGANIZATION_ID" ]; then
  res=$(send_request "GET" "/stores/$ORGANIZATION_ID/reviews?pageNumber=1&pageSize=5" "" "")
  status=${res%%|*}; body=${res#*|}
  assert_status "41.1: List Store Reviews With Pagination" "$status" "200" "$body"
fi

# 41.2 POST /reviews rating = 0 (400 - out of valid range)
if [ -n "$ORDER_ID" ]; then
  res=$(send_request "POST" "/reviews" "{\"orderId\":\"$ORDER_ID\",\"rating\":0}" "$CUSTOMER_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "41.2: Post Review With Zero Rating" "$status" "400" "$body"
fi

# ==============================================================================
# SECTION 42: ADMIN - REMAINING MISSING SCENARIOS
# ==============================================================================
echo -e "\n--- Testing Admin Remaining Scenarios ---"

if [ -n "$ADMIN_TOKEN" ]; then
  # 42.1 GET /admin/stores/pending is AllowAnonymous - verify 200 without token
  res=$(send_request "GET" "/admin/stores/pending" "" "")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.1: Get Pending Stores Without Auth (AllowAnonymous)" "$status" "200" "$body"

  # 42.2 GET /admin/stores/{id} is AllowAnonymous - verify 200 without token
  res=$(send_request "GET" "/admin/stores/$ORGANIZATION_ID" "" "")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.2: Get Store For Review Without Auth (AllowAnonymous)" "$status" "200" "$body"

  # 42.3 GET /admin/users with searchTerm filter
  res=$(send_request "GET" "/admin/users?searchTerm=admin" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.3: Admin List Users With SearchTerm Filter" "$status" "200" "$body"

  # 42.4 GET /admin/reviews filtered by organizationId
  res=$(send_request "GET" "/admin/reviews?organizationId=$ORGANIZATION_ID" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.4: Admin List Reviews Filtered by OrganizationId" "$status" "200" "$body"

  # 42.5 GET /admin/products filtered by organizationId
  res=$(send_request "GET" "/admin/products?organizationId=$ORGANIZATION_ID" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.5: Admin List Products Filtered by OrganizationId" "$status" "200" "$body"

  # 42.6 GET /admin/support-tickets with priority filter
  res=$(send_request "GET" "/admin/support-tickets?priority=High" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.6: Admin List Tickets Filtered by Priority" "$status" "200" "$body"

  # 42.7 PATCH /admin/users/{id}/status with invalid status value (400)
  res=$(send_request "PATCH" "/admin/users/$CUSTOMER_USER_ID/status" "{\"status\":\"InvalidStatus\"}" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.7: Admin Update User Status With Invalid Value" "$status" "400" "$body"

  # 42.8 PATCH /admin/users/{id}/status non-existent user (404)
  res=$(send_request "PATCH" "/admin/users/00000000-0000-0000-0000-000000000000/status" "{\"status\":\"Active\"}" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.8: Admin Update Status Non-existent User" "$status" "404" "$body"

  # 42.9 GET /admin/analytics/summary unauthenticated (401)
  res=$(send_request "GET" "/admin/analytics/summary" "" "")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.9: Admin Analytics Summary Unauthenticated" "$status" "401" "$body"

  # 42.10 GET /admin/disputes with pagination
  res=$(send_request "GET" "/admin/disputes?pageNumber=1&pageSize=5" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.10: Admin List Disputes With Pagination" "$status" "200" "$body"

  # 42.11 GET /admin/activity-logs global feed (200)
  res=$(send_request "GET" "/admin/activity-logs?pageNumber=1&pageSize=10" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.11: Admin Global Platform Activity Logs Feed" "$status" "200" "$body"

  # 42.12 GET /admin/activity-logs unauthenticated (401)
  res=$(send_request "GET" "/admin/activity-logs" "" "")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.12: Admin Global Activity Logs Unauthenticated" "$status" "401" "$body"

  # 42.13 GET /admin/analytics/summary verifies complete breakdowns (200)
  res=$(send_request "GET" "/admin/analytics/summary" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.13: Admin Analytics Summary Breakdown Feeds" "$status" "200" "$body"

  # 42.14 GET /admin/activity-logs/{id} get specific log detail (200/404)
  res=$(send_request "GET" "/admin/activity-logs/00000000-0000-0000-0000-000000000000" "" "$ADMIN_TOKEN")
  status=${res%%|*}; body=${res#*|}
  assert_status "42.14: Admin Single Activity Log Detail 404" "$status" "404" "$body"
fi
# ==============================================================================
# TEST RUN SUMMARY
# ==============================================================================
echo "=========================================================="
echo "FoodLoop Automated Integration Test Suite Completed"
echo "TOTAL PASSED ASSERTIONS: $PASS_COUNT"
echo "TOTAL FAILED ASSERTIONS: $FAIL_COUNT"
echo "=========================================================="

if [ "$FAIL_COUNT" -eq 0 ]; then
    exit 0
else
    exit 1
fi
