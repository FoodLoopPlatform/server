# Store Profile Endpoint - Local Testing Guide

## 🚀 Step 1: Start the Application

### Option A: Using Visual Studio
1. Open `FoodLoop.sln` in Visual Studio
2. Set `FoodLoop.API` as the startup project (right-click → Set as Startup Project)
3. Press `F5` or click "Start Debugging"

### Option B: Using Command Line
```bash
# Navigate to the server directory
cd d:\ITI\GP\FoodLoop\server

# Run the API project
dotnet run --project src\FoodLoop.API
```

### Expected Output
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## 🌐 Step 2: Open Swagger UI

### Access Swagger
1. **Open your browser**
2. **Navigate to:** `https://localhost:5001/swagger` or `http://localhost:5000/swagger`
3. **You should see the Swagger UI** with all API endpoints listed

### Find the Store Profile Endpoint
1. Scroll down to the **"Stores"** section (or use Ctrl+F to search for "stores")
2. Look for: **`GET /stores/{id}`**
3. It should show the description: "public store profile endpoint"

---

## 📋 Step 3: Get a Valid Store ID

Before testing, you need to find an existing store ID from your database.

### Method 1: Using Swagger (Easier)
1. Find **`GET /stores/me`** endpoint (returns current merchant's store)
2. Click "Try it out"
3. Click "Execute"
4. Copy the `id` from the response

### Method 2: Using Database Query
If you have database access:
```sql
-- Get the first verified store
SELECT TOP 1 Id, Name, VerificationStatus 
FROM Organizations 
WHERE IsDeleted = 0 AND VerificationStatus = 2
ORDER BY CreatedAt DESC
```

### Method 3: Create a Test Store
If no stores exist, you need to:
1. Register as a Merchant via **`POST /auth/register`**
2. Complete store onboarding
3. Admin approval (or directly update database for testing)

---

## 🧪 Test Scenarios

### ✅ Scenario 1: Valid Store with Reviews

**Purpose:** Test the happy path - store exists and has reviews

#### Steps:
1. In Swagger, locate **`GET /stores/{id}`**
2. Click **"Try it out"** button
3. Enter a valid store ID in the `id` field (e.g., `3fa85f64-5717-4562-b3fc-2c963f66afa6`)
4. Leave default values for pagination:
   - `reviewsPageNumber`: 1
   - `reviewsPageSize`: 5
5. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Fresh Market",
    "description": "Your neighborhood store",
    "logo": "/uploads/stores/logos/abc.jpg",
    "coverPhoto": "/uploads/stores/covers/xyz.jpg",
    "phone": "+20 122 345 6789",
    "email": "store@example.com",
    "businessCategory": "Supermarket",
    "governorate": "Cairo",
    "city": "Nasr City",
    "neighborhood": "Abbas El Akkad",
    "street": "Abbas El Akkad Street",
    "buildingNo": "15",
    "latitude": 30.0444,
    "longitude": 31.2357,
    "openingHours": "{\"monday\":{\"open\":\"08:00\",\"close\":\"22:00\"}}",
    "verificationStatus": "Verified",
    "averageRating": 4.5,
    "totalReviews": 10,
    "ratingDistribution": [
      { "stars": 5, "count": 6 },
      { "stars": 4, "count": 2 },
      { "stars": 3, "count": 1 },
      { "stars": 2, "count": 1 },
      { "stars": 1, "count": 0 }
    ],
    "recentReviews": [
      {
        "id": "...",
        "orderId": "...",
        "userId": "...",
        "userFullName": "Ahmed Mohamed",
        "organizationId": "...",
        "organizationName": "Fresh Market",
        "rating": 5,
        "comment": "Great service!",
        "createdAt": "2026-08-10T14:30:00Z"
      }
    ]
  },
  "message": null,
  "errors": []
}
```

#### Verify:
- ✅ All store fields are populated
- ✅ `averageRating` is between 0.0 and 5.0
- ✅ `ratingDistribution` has 5 entries (stars 1-5)
- ✅ `recentReviews` contains up to 5 reviews
- ✅ Each review has `userFullName` populated

---

### ❌ Scenario 2: Store Not Found

**Purpose:** Test error handling for invalid store ID

#### Steps:
1. In **`GET /stores/{id}`** endpoint
2. Click **"Try it out"**
3. Enter an **invalid/non-existent GUID**: `00000000-0000-0000-0000-000000000000`
4. Click **"Execute"**

#### Expected Result:
❌ **Status: 404 Not Found**

**Response Body:**
```json
{
  "success": false,
  "data": null,
  "message": "Store not found.",
  "errors": ["Store not found."]
}
```

#### Verify:
- ✅ Status code is 404
- ✅ `success` is `false`
- ✅ `data` is `null`
- ✅ Error message is clear and localized

---

### ✅ Scenario 3: Store with No Reviews

**Purpose:** Test how the endpoint handles stores without any reviews

#### Steps:
1. Find a store that has **no reviews** (newly created store)
   - Or create a new merchant account and complete onboarding
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the store ID
5. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    "id": "...",
    "name": "New Store",
    "description": "...",
    // ... other store fields ...
    "verificationStatus": "Verified",
    "averageRating": 0.0,
    "totalReviews": 0,
    "ratingDistribution": [
      { "stars": 5, "count": 0 },
      { "stars": 4, "count": 0 },
      { "stars": 3, "count": 0 },
      { "stars": 2, "count": 0 },
      { "stars": 1, "count": 0 }
    ],
    "recentReviews": []
  },
  "message": null,
  "errors": []
}
```

#### Verify:
- ✅ `averageRating` is `0.0` (not null)
- ✅ `totalReviews` is `0`
- ✅ `ratingDistribution` still has 5 entries, all with count = 0
- ✅ `recentReviews` is empty array `[]` (not null)

---

### ✅ Scenario 4: Review Pagination - First Page

**Purpose:** Test review pagination works correctly

#### Steps:
1. Use a store ID that has **at least 10 reviews**
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the store ID
5. Set pagination parameters:
   - `reviewsPageNumber`: **1**
   - `reviewsPageSize`: **3**
6. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    // ... store fields ...
    "totalReviews": 10,
    "recentReviews": [
      { "id": "...", "rating": 5, "comment": "Most recent review", "createdAt": "2026-08-14T..." },
      { "id": "...", "rating": 4, "comment": "Second most recent", "createdAt": "2026-08-13T..." },
      { "id": "...", "rating": 5, "comment": "Third most recent", "createdAt": "2026-08-12T..." }
    ]
  }
}
```

#### Verify:
- ✅ Exactly **3 reviews** returned
- ✅ Reviews are ordered by `createdAt` **descending** (newest first)
- ✅ `totalReviews` shows total count (10), not just page count

---

### ✅ Scenario 5: Review Pagination - Second Page

**Purpose:** Test pagination for subsequent pages

#### Steps:
1. Use the same store ID from Scenario 4
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the store ID
5. Set pagination parameters:
   - `reviewsPageNumber`: **2**
   - `reviewsPageSize`: **3**
6. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    // ... store fields ...
    "totalReviews": 10,
    "recentReviews": [
      { "id": "...", "rating": 3, "comment": "Fourth review", "createdAt": "2026-08-11T..." },
      { "id": "...", "rating": 5, "comment": "Fifth review", "createdAt": "2026-08-10T..." },
      { "id": "...", "rating": 4, "comment": "Sixth review", "createdAt": "2026-08-09T..." }
    ]
  }
}
```

#### Verify:
- ✅ Reviews are **different** from page 1
- ✅ Reviews are still ordered by date (older than page 1)
- ✅ `totalReviews` remains 10
- ✅ No duplicate reviews between pages

---

### ✅ Scenario 6: Pagination Beyond Last Page

**Purpose:** Test pagination when requesting a page that doesn't exist

#### Steps:
1. Use a store with only 5 reviews
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Set pagination parameters:
   - `reviewsPageNumber`: **10**
   - `reviewsPageSize`: **3**
6. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    // ... store fields ...
    "totalReviews": 5,
    "recentReviews": []
  }
}
```

#### Verify:
- ✅ Still returns 200 (not an error)
- ✅ `recentReviews` is empty array `[]`
- ✅ `totalReviews` shows correct total count

---

### ✅ Scenario 7: Unverified Store

**Purpose:** Test that unverified stores are still accessible

#### Steps:
1. Find a store with `verificationStatus` != "Verified"
   - Could be "Unverified", "Pending", or "Rejected"
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the unverified store ID
5. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    "id": "...",
    "name": "Unverified Store",
    // ... other fields ...
    "verificationStatus": "Pending",
    "averageRating": 0.0,
    "totalReviews": 0,
    "ratingDistribution": [...],
    "recentReviews": []
  }
}
```

#### Verify:
- ✅ Store data is returned (not 404)
- ✅ `verificationStatus` shows "Pending", "Unverified", or "Rejected"
- ✅ All other fields populated normally
- ✅ Mobile app can decide whether to show warning based on status

---

### ❌ Scenario 8: Soft-Deleted Store

**Purpose:** Verify soft-deleted stores are treated as not found

#### Steps:
1. Soft-delete a store in the database:
   ```sql
   UPDATE Organizations 
   SET IsDeleted = 1, DeletedAt = GETUTCDATE() 
   WHERE Id = '<your-store-id>'
   ```
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the soft-deleted store ID
5. Click **"Execute"**

#### Expected Result:
❌ **Status: 404 Not Found**

**Response Body:**
```json
{
  "success": false,
  "data": null,
  "message": "Store not found.",
  "errors": ["Store not found."]
}
```

#### Verify:
- ✅ Soft-deleted stores behave like they don't exist
- ✅ Same error message as non-existent store
- ✅ Data is not exposed

---

### ✅ Scenario 9: Store with Minimal Data

**Purpose:** Test how endpoint handles stores with null/optional fields

#### Steps:
1. Find or create a store with minimal data:
   - No description
   - No logo/cover photo
   - No phone/email
   - No location details
2. In **`GET /stores/{id}`** endpoint
3. Click **"Try it out"**
4. Enter the store ID
5. Click **"Execute"**

#### Expected Result:
✅ **Status: 200 OK**

**Response Body:**
```json
{
  "success": true,
  "data": {
    "id": "...",
    "name": "Minimal Store",
    "description": null,
    "logo": null,
    "coverPhoto": null,
    "phone": null,
    "email": null,
    "businessCategory": null,
    "governorate": null,
    "city": null,
    "neighborhood": null,
    "street": null,
    "buildingNo": null,
    "latitude": null,
    "longitude": null,
    "openingHours": null,
    "verificationStatus": "Unverified",
    "averageRating": 0.0,
    "totalReviews": 0,
    "ratingDistribution": [...],
    "recentReviews": []
  }
}
```

#### Verify:
- ✅ Endpoint doesn't crash with null fields
- ✅ Only required field is `name`
- ✅ All nullable fields show `null` (not omitted)

---

### ✅ Scenario 10: Invalid GUID Format

**Purpose:** Test validation for malformed store ID

#### Steps:
1. In **`GET /stores/{id}`** endpoint
2. Click **"Try it out"**
3. Enter an **invalid GUID format**: `invalid-id-123`
4. Click **"Execute"**

#### Expected Result:
❌ **Status: 400 Bad Request**

**Response Body:**
```json
{
  "success": false,
  "data": null,
  "message": "The value 'invalid-id-123' is not valid.",
  "errors": [...]
}
```

#### Verify:
- ✅ ASP.NET Core model binding catches the invalid format
- ✅ Returns 400 (not 404)
- ✅ Clear validation error message

---

## 🌍 Testing Localization

### Scenario 11: Arabic Error Message

**Purpose:** Test localization works correctly

#### Using Browser DevTools:
1. Open **Browser DevTools** (F12)
2. Go to **Network** tab
3. In **`GET /stores/{id}`** endpoint in Swagger
4. Enter an invalid store ID
5. Before clicking "Execute", **modify the request headers:**
   - In DevTools, right-click the request → Edit and Resend
   - Add header: `Accept-Language: ar`
6. Send the request

#### Expected Result:
❌ **Status: 404 Not Found**

**Response Body:**
```json
{
  "success": false,
  "data": null,
  "message": "المتجر غير موجود.",
  "errors": ["المتجر غير موجود."]
}
```

#### Verify:
- ✅ Error message is in **Arabic**
- ✅ Same functionality, different language

#### Alternative Method (Postman):
```
GET https://localhost:5001/stores/00000000-0000-0000-0000-000000000000
Headers:
  Accept-Language: ar
```

---

## 📊 Testing Checklist

Use this checklist to track your testing progress:

- [ ] ✅ Scenario 1: Valid store with reviews (200 OK)
- [ ] ❌ Scenario 2: Store not found (404)
- [ ] ✅ Scenario 3: Store with no reviews (200 OK, averageRating = 0.0)
- [ ] ✅ Scenario 4: Pagination - First page (3 reviews)
- [ ] ✅ Scenario 5: Pagination - Second page (next 3 reviews)
- [ ] ✅ Scenario 6: Pagination beyond last page (empty array)
- [ ] ✅ Scenario 7: Unverified store (200 OK, verificationStatus != Verified)
- [ ] ❌ Scenario 8: Soft-deleted store (404)
- [ ] ✅ Scenario 9: Store with minimal data (all nulls OK)
- [ ] ❌ Scenario 10: Invalid GUID format (400 Bad Request)
- [ ] 🌍 Scenario 11: Arabic localization (Arabic error message)

---

## 🐛 Common Issues & Solutions

### Issue 1: "Store not found" for valid ID
**Possible Causes:**
- Store is soft-deleted (`IsDeleted = 1`)
- Store doesn't exist in database
- Wrong database connection

**Solution:**
```sql
-- Check if store exists and is not deleted
SELECT Id, Name, IsDeleted, VerificationStatus 
FROM Organizations 
WHERE Id = '<your-store-id>'
```

### Issue 2: No stores available to test
**Solution:**
1. Register as Merchant via `POST /auth/register`
2. Complete onboarding steps
3. Or manually insert test data

### Issue 3: Swagger shows 401 Unauthorized
**Note:** This endpoint is **public**, it should NOT require authentication.

**Check:**
- Verify `[AllowAnonymous]` attribute is present
- Try without any authentication token

### Issue 4: Reviews not showing user names
**Possible Cause:** User records missing from database

**Solution:**
```sql
-- Check if users exist for review user IDs
SELECT r.UserId, u.FullName 
FROM Reviews r
LEFT JOIN Users u ON r.UserId = u.Id
WHERE r.OrganizationId = '<your-store-id>'
```

### Issue 5: Rating distribution all zeros
**Cause:** Store has no reviews (this is correct behavior!)

**Verify:** Check `totalReviews` field should be 0

---

## 🎯 Quick Test Script

If you want to test all scenarios quickly, here's a checklist:

1. **Start the application** (`dotnet run`)
2. **Open Swagger** (`https://localhost:5001/swagger`)
3. **Find a valid store ID** (use `GET /stores/me` or check database)
4. **Run these tests in order:**
   - Valid ID → Should get 200 with full data
   - Invalid ID (all zeros) → Should get 404
   - Pagination page 1 (size 3) → Should get 3 reviews
   - Pagination page 2 (size 3) → Should get next 3 reviews
   - Invalid format ("abc") → Should get 400

---

## 📝 Test Results Template

Use this template to document your test results:

```markdown
## Test Session: [Date]
**Tester:** [Your Name]
**Environment:** Local Development (localhost:5001)

### Test Results:

| Scenario | Status | Notes |
|----------|--------|-------|
| Valid Store | ✅ Pass | All fields populated correctly |
| Store Not Found | ✅ Pass | 404 with correct error message |
| No Reviews | ✅ Pass | averageRating = 0.0, empty array |
| Pagination Page 1 | ✅ Pass | 3 reviews returned |
| Pagination Page 2 | ✅ Pass | Different 3 reviews |
| Beyond Last Page | ✅ Pass | Empty array returned |
| Unverified Store | ✅ Pass | Status shows correctly |
| Soft-Deleted | ✅ Pass | Treated as not found |
| Minimal Data | ✅ Pass | Nulls handled correctly |
| Invalid Format | ✅ Pass | 400 Bad Request |
| Arabic Localization | ✅ Pass | Error in Arabic |

### Issues Found:
[List any bugs or unexpected behavior]

### Summary:
[Overall assessment of the endpoint functionality]
```

---

## ✅ Success Criteria

Your testing is complete when:
- ✅ All 11 scenarios pass
- ✅ No unexpected errors or crashes
- ✅ Response format matches documentation
- ✅ Pagination works correctly
- ✅ Error handling is appropriate
- ✅ Localization works for Arabic
- ✅ Performance is acceptable (<500ms response time)

---

**Happy Testing! 🎉**

If you encounter any issues not covered here, check:
1. Application logs in the console
2. `IMPLEMENTATION_SUMMARY_STORE_PROFILE.md` for technical details
3. Database to verify data exists
