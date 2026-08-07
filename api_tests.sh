#!/usr/bin/env bash
# FoodLoop API Automated Test Suite (Bash Edition)
# This script executes happy paths, validation failures, not found, unauthorized, and state transition test scenarios.

set -e

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
    local curl_args=("-s" "-k" "-w" "\n%{http_code}" "-X" "$method")
    for h in "${headers[@]}"; do
        curl_args+=("$h")
    done

    if [ -n "$body" ]; then
        # Write body to temp file to avoid quote mangling on Windows/Git Bash
        echo "$body" > temp_payload.json
        curl_args+=("-d" "@temp_payload.json")
    fi
    curl_args+=("$BASE_URL$route")

    # Execute request
    local response
    response=$(curl.exe "${curl_args[@]}")

    if [ -f temp_payload.json ]; then
        rm temp_payload.json
    fi

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

    if [ "$actual_code" -eq "$expected_code" ]; then
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
# SECTION 1: AUTHENTICATION MODULE (/auth)
# ==============================================================================
echo -e "\n--- Testing Authentication Endpoints ---"

# Scenario 1.1: Register (Happy Path)
RANDOM_VAL="$(date +%s)_$((10000 + RANDOM % 90000))"
MERCHANT_EMAIL="merchant.test${RANDOM_VAL}@example.com"
REGISTER_PAYLOAD="{\"name\":\"Test Merchant $RANDOM_VAL\",\"email\":\"$MERCHANT_EMAIL\",\"password\":\"Password@123\",\"role\":\"Merchant\",\"businessName\":\"Test Organic Shop $RANDOM_VAL\"}"


res=$(send_request "POST" "/auth/register" "$REGISTER_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.1: Register New Merchant Account" "$status" "201" "$body"

# Scenario 1.2: Register (Bad Request / Validation Failure)
BAD_REGISTER_PAYLOAD="{\"name\":\"\",\"email\":\"invalidemail\",\"password\":\"123\",\"role\":\"InvalidRole\"}"
res=$(send_request "POST" "/auth/register" "$BAD_REGISTER_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.2: Register With Bad Validation Fields" "$status" "400" "$body"

# Scenario 1.3: Login (Happy Path - Unverified Merchant returns no token)
LOGIN_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"password\":\"Password@123\"}"
res=$(send_request "POST" "/auth/login" "$LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.3: Log In Merchant Account" "$status" "200" "$body"

# Log In Verified Merchant for subsequent inventory/store operations
VERIFIED_MERCHANT_LOGIN_PAYLOAD="{\"email\":\"merchant.spinneys@example.com\",\"password\":\"Password@123\"}"
res=$(send_request "POST" "/auth/login" "$VERIFIED_MERCHANT_LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    MERCHANT_TOKEN=$(get_json_value "$body" "accessToken")
    M_REFRESH_TOKEN=$(get_json_value "$body" "refreshToken")
    echo "[INFO] Loaded Verified Merchant token successfully."
fi

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

# Scenario 1.6: Reset Password (Happy Path)
RESET_PAYLOAD="{\"email\":\"$MERCHANT_EMAIL\",\"token\":\"$RESET_TOKEN\",\"newPassword\":\"NewPassword@123\"}"
res=$(send_request "POST" "/auth/reset-password" "$RESET_PAYLOAD")
status=${res%%|*}
body=${res#*|}
assert_status "1.6: Reset Password With Token" "$status" "200" "$body"

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

# Log In System Admin
ADMIN_LOGIN_PAYLOAD="{\"email\":\"admin@foodloop.com\",\"password\":\"Admin@123\"}"
res=$(send_request "POST" "/auth/login" "$ADMIN_LOGIN_PAYLOAD")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    ADMIN_TOKEN=$(get_json_value "$body" "accessToken")
    echo "[INFO] Loaded System Admin token successfully."
else
    echo "[WARNING] System Admin login failed. Admin tests will be skipped."
fi

# Log In / Register Customer
CUSTOMER_EMAIL="customer.test${RANDOM_VAL}@example.com"
CUSTOMER_REG_PAYLOAD="{\"name\":\"Test Customer $RANDOM_VAL\",\"email\":\"$CUSTOMER_EMAIL\",\"password\":\"Password@123\",\"role\":\"Customer\"}"
res=$(send_request "POST" "/auth/register" "$CUSTOMER_REG_PAYLOAD")
status=${res%%|*}
body=${res#*|}
CUSTOMER_USER_ID=$(get_json_value "$body" "id")
echo "[INFO] Loaded Registered Customer User ID: $CUSTOMER_USER_ID"

res=$(send_request "POST" "/auth/login" "{\"email\":\"$CUSTOMER_EMAIL\",\"password\":\"Password@123\"}")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    CUSTOMER_TOKEN=$(get_json_value "$body" "accessToken")
    echo "[INFO] Loaded Customer token successfully."
fi

# Log In / Register Charity
CHARITY_EMAIL="charity.test${RANDOM_VAL}@example.com"
CHARITY_REG_PAYLOAD="{\"name\":\"Test Charity $RANDOM_VAL\",\"email\":\"$CHARITY_EMAIL\",\"password\":\"Password@123\",\"role\":\"Charity\",\"businessName\":\"Test Charity Org $RANDOM_VAL\"}"
res=$(send_request "POST" "/auth/register" "$CHARITY_REG_PAYLOAD")

res=$(send_request "POST" "/auth/login" "{\"email\":\"$CHARITY_EMAIL\",\"password\":\"Password@123\"}")
status=${res%%|*}
body=${res#*|}
if [ "$status" -eq 200 ]; then
    CHARITY_TOKEN=$(get_json_value "$body" "accessToken")
    echo "[INFO] Loaded Charity token successfully."
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
curl.exe -s -k -H "Authorization: Bearer $MERCHANT_TOKEN" -F "Email=$MERCHANT_EMAIL" -F "Type=TaxIdCertificate" -F "File=@mock_cr.pdf" "$BASE_URL/stores/me/documents" > /dev/null
curl.exe -s -k -H "Authorization: Bearer $MERCHANT_TOKEN" -F "Email=$MERCHANT_EMAIL" -F "Type=StoreFacilityPhoto" -F "File=@mock_cr.pdf" "$BASE_URL/stores/me/documents" > /dev/null
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "Email=$MERCHANT_EMAIL" \
  -F "Type=CommercialRegistration" \
  -F "File=@mock_cr.pdf" \
  "$BASE_URL/stores/me/documents")
rm mock_cr.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.3: Upload Store Verification Documents" "$status" "200" "$body"

# Scenario 3.4: Submit Charity Documents (Mock multipart upload)
echo "PDF-CHARITY-CR-CONTENT" > mock_charity_cr.pdf
curl.exe -s -k -H "Authorization: Bearer $CHARITY_TOKEN" -F "Email=$CHARITY_EMAIL" -F "Type=CharityBylaws" -F "File=@mock_charity_cr.pdf" "$BASE_URL/charities/me/documents" > /dev/null
curl.exe -s -k -H "Authorization: Bearer $CHARITY_TOKEN" -F "Email=$CHARITY_EMAIL" -F "Type=BoardOfDirectorsList" -F "File=@mock_charity_cr.pdf" "$BASE_URL/charities/me/documents" > /dev/null
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $CHARITY_TOKEN" \
  -F "Email=$CHARITY_EMAIL" \
  -F "Type=AssociationCertificate" \
  -F "File=@mock_charity_cr.pdf" \
  "$BASE_URL/charities/me/documents")
rm mock_charity_cr.pdf
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.4: Upload Charity Association Certificate" "$status" "200" "$body"

# Scenario 3.5: Update Store Profile multipart (Happy Path)
res=$(curl.exe -s -k -w "\n%{http_code}" -X PATCH \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "Name=Spinneys Supermarket Updated $RANDOM_VAL" \
  -F "BusinessCategory=Supermarket" \
  "$BASE_URL/stores/me")
status=$(echo "$res" | tail -n 1)
body=$(echo "$res" | sed '$d')
assert_status "3.5: Update Store Name and Category Profile" "$status" "200" "$body"

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
PRD_PAYLOAD="{\"categoryId\":\"$CATEGORY_ID\",\"title\":\"Artisan Sourdough Loaf $RANDOM_VAL\",\"titleAr\":\"خبز ساوردو يدوي $RANDOM_VAL\",\"description\":\"Crispy sourdough bread.\",\"descriptionAr\":\"خبز مخبوز طازج.\",\"originalPrice\":15.00,\"discountedPrice\":7.50,\"quantityAvailable\":10,\"expirationDate\":\"2026-08-15\"}"
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
echo "PNG-IMAGE-PAYLOAD" > mock_img.png
res=$(curl.exe -s -k -w "\n%{http_code}" -X POST \
  -H "Authorization: Bearer $MERCHANT_TOKEN" \
  -F "File=@mock_img.png" \
  "$BASE_URL/stores/me/products/$PRODUCT_ID/images")
rm mock_img.png
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

    # Fetch organization ID from pending queue
    res=$(send_request "GET" "/admin/stores/pending" "" "$ADMIN_TOKEN")
    body=${res#*|}
    M_ORG_ID=$(echo "$body" | tr ',' '\n' | tr '{' '\n' | grep -B 25 -i "$MERCHANT_EMAIL" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)
    C_ORG_ID=$(echo "$body" | tr ',' '\n' | tr '{' '\n' | grep -B 25 -i "$CHARITY_EMAIL" | grep -oP '"id"\s*:\s*"\K[^"]+' | head -n 1 || true)

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
