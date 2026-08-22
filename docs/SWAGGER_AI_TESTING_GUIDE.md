# 🧪 FoodLoop AI Integration — Complete Swagger Testing Guide & Scenarios

This guide provides **100% complete test payloads**, real IDs, authentication credentials, and step-by-step verification flows for **every scenario across the AI integration pipeline** in **Swagger UI** (`https://foodloop.runasp.net/swagger` or `http://localhost:5000/swagger`).

---

## 📑 Complete Scenario Index

1. [🔑 0. Authentication Setup & Credentials](#0-authentication-setup--credentials)
2. [📦 Scenario 1: Product Creation with AI Verification Metadata](#scenario-1-product-creation-with-ai-verification-metadata)
3. [📑 Scenario 2: High-Speed Bulk CSV Product Ingestion](#scenario-2-high-speed-bulk-csv-product-ingestion)
4. [📷 Scenario 3: Stateless AI Packaging Vision & OCR Scanner](#scenario-3-stateless-ai-packaging-vision--ocr-scanner)
5. [🛰️ Scenario 4: AI Inventory Monitoring & Risk Signals Matrix](#scenario-4-ai-inventory-monitoring--risk-signals-matrix)
6. [⏰ Scenario 5: AI Cycle Scheduling & Real-Time Background Status](#scenario-5-ai-cycle-scheduling--real-time-background-status)
7. [💡 Scenario 6: Dynamic Pricing in Assisted Mode (Merchant Approvals)](#scenario-6-dynamic-pricing-in-assisted-mode-merchant-approvals)
8. [⚡ Scenario 7: Dynamic Pricing in Autonomous Mode (Auto-Execution)](#scenario-7-dynamic-pricing-in-autonomous-mode-auto-execution)
9. [🛡️ Scenario 8: Price Floor Protection & Manual Markdown Auditing](#scenario-8-price-floor-protection--manual-markdown-auditing)
10. [📚 Scenario 9: RAG Vector Knowledge Ingestion & Episode Correction](#scenario-9-rag-vector-knowledge-ingestion--episode-correction)
11. [🛡️ Scenario 10: Low-Confidence AI Moderation & Admin Governance](#scenario-10-low-confidence-ai-moderation--admin-governance)
12. [🌐 Scenario 11: Direct AWS EC2 AI Microservice Contract Testing](#scenario-11-direct-aws-ec2-ai-microservice-contract-testing)

---

## 🔑 0. Authentication Setup & Credentials

### Step 1: Open Swagger UI
Navigate to **`https://foodloop.runasp.net/swagger`** in your browser.

### Step 2: Obtain Bearer Tokens via `POST /auth/login`

#### 👑 Administrator Account (For Background Triggers & Governance):
* **Request Body**:
```json
{
  "email": "admin@foodloop.com",
  "password": "Admin@123"
}
```

#### 🏪 Merchant Account (Spinneys Store Manager):
* **Request Body**:
```json
{
  "email": "merchant.spinneys@example.com",
  "password": "Password@123"
}
```

#### 🏪 Merchant Account (Carrefour Store Manager):
* **Request Body**:
```json
{
  "email": "merchant.carrefour@example.com",
  "password": "Password@123"
}
```

#### 👤 Customer Account:
* **Request Body**:
```json
{
  "email": "customer.ahmed@example.com",
  "password": "Password@123"
}
```

### Step 3: Authorize Swagger Session
1. Copy the `accessToken` value from the login response.
2. Click the green **`Authorize 🔓`** button at the top right.
3. Paste the token into the input box and click **Authorize**.

---

## 📦 Scenario 1: Product Creation with AI Verification Metadata

Simulates adding a perishable product with pre-verified OCR metadata to test AI intake.

* **Role**: Merchant (`merchant.spinneys@example.com`)
* **Endpoint**: `POST /stores/me/products`
* **Request Body**:
```json
{
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Almarai Greek Style Natural Yoghurt 170g",
  "description": "High-protein fresh Greek yoghurt, sealed retail cup.",
  "originalPrice": 35.00,
  "discountedPrice": 35.00,
  "quantityAvailable": 25,
  "expirationDate": "2026-08-23",
  "expiryVerificationState": "Verified",
  "ocrConfidence": 0.96,
  "ocrText": "EXP: 23/08/2026 LOT: ALM-9012"
}
```

* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": {
    "id": "7b819f00-3412-4211-8a90-3bf01e389ab2",
    "title": "Almarai Greek Style Natural Yoghurt 170g",
    "originalPrice": 35.00,
    "discountedPrice": 35.00,
    "quantityAvailable": 25,
    "expirationDate": "2026-08-23",
    "status": "PendingModeration",
    "expiryVerificationState": "Verified"
  }
}
```

---

## 📑 Scenario 2: High-Speed Bulk CSV Product Ingestion

Tests high-volume batch uploading of perishable items with bilingual header support.

* **Role**: Merchant (`merchant.spinneys@example.com`)
* **Endpoint**: `POST /stores/me/products/bulk`
* **Request Type**: `multipart/form-data`
* **Sample CSV Content** (Save as `bulk_products.csv` and upload):
```csv
Title,Description,OriginalPrice,DiscountedPrice,QuantityAvailable,ExpirationDate,Category,ExpiryVerificationState
Lactel Full Cream Milk 1L,Fresh cow milk,42.00,42.00,50,2026-08-23,Dairy,Verified
Domiati White Feta Cheese 500g,Soft white cheese,65.00,65.00,30,2026-08-24,Dairy,Verified
Juhayna Pure Orange Juice 1L,100% pure juice no added sugar,38.00,38.00,40,2026-08-25,Beverages,Verified
Rich Bake Toast Bread White,Sliced sandwich bread,32.00,32.00,20,2026-08-22,Bakery,Verified
```

* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": [
    { "title": "Lactel Full Cream Milk 1L", "originalPrice": 42.00, "status": "PendingModeration" },
    { "title": "Domiati White Feta Cheese 500g", "originalPrice": 65.00, "status": "PendingModeration" },
    { "title": "Juhayna Pure Orange Juice 1L", "originalPrice": 38.00, "status": "PendingModeration" },
    { "title": "Rich Bake Toast Bread White", "originalPrice": 32.00, "status": "PendingModeration" }
  ]
}
```

---

## 📷 Scenario 3: Stateless AI Packaging Vision & OCR Scanner

Extracts expiration dates, title hints, and bilingual category suggestions from a packaging photo without saving to the database.

* **Role**: Merchant
* **Endpoint**: `POST /stores/me/products/ocr-scan`
* **Request Type**: `multipart/form-data`
* **Parameter `file`**: Attach any food packaging photo (`.jpg` / `.png`).
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": {
    "detectedProduct": "Greek Yoghurt Natural 150g",
    "extractedExpiryDate": "2026-08-25",
    "confidenceScore": 0.92,
    "suggestedCategoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "suggestedCategoryName": "Dairy & Eggs",
    "suggestedCategoryNameAr": "منتجات الألبان والبيض",
    "extractedText": "BEST BEFORE: 25/08/2026 LOT: 48921 150g NET"
  }
}
```

---

## 🛰️ Scenario 4: AI Inventory Monitoring & Risk Signals Matrix

Evaluates inventory metrics (shelf-life, sales velocity, demand coverage) against the AWS AI monitoring microservice.

### Step 4.1: Trigger Global Monitoring Scan (Admin)
* **Role**: Admin (`admin@foodloop.com`)
* **Endpoint**: `POST /admin/monitoring-scan`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": "AI monitoring scan completed successfully."
}
```

### Step 4.2: Inspect Store Risk Distribution in English (Merchant)
* **Role**: Merchant (`merchant.spinneys@example.com`)
* **Endpoint**: `GET /stores/me/products/risk-analysis`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": {
    "criticalRiskCount": 3,
    "highRiskCount": 5,
    "mediumRiskCount": 2,
    "lowRiskCount": 12,
    "criticalProducts": [
      {
        "id": "01e48ed0-4322-4153-bc3d-c50f3e2d7ee1",
        "title": "Fresh Pasteurised Milk 1L",
        "currentPrice": 45.00,
        "quantityAvailable": 18,
        "expirationDate": "2026-08-22",
        "daysRemaining": 1,
        "riskLevel": "CRITICAL"
      }
    ]
  }
}
```

### Step 4.3: Inspect Store Risk Distribution in Arabic RTL (Merchant)
* **Header**: `Accept-Language: ar`
* **Endpoint**: `GET /stores/me/products/risk-analysis`
* **Expected Response**: Bilingual category labels and Arabic reason strings.

---

## ⏰ Scenario 5: AI Cycle Scheduling & Real-Time Background Status

Enables frontends to query when the next automated cycles will execute and inspect service health.

### Step 5.1: Merchant AI Pricing & Monitoring Schedule
Enables merchant dashboards to render live countdown timers (*"Next automated pricing evaluation in 42 minutes"*).

* **Role**: Merchant (`merchant.spinneys@example.com`)
* **Endpoint**: `GET /stores/me/ai-recommendations/schedule`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": {
    "nextPricingBatchAt": "2026-08-22T19:24:00+00:00",
    "nextMonitoringScanAt": "2026-08-22T19:24:00+00:00",
    "pricingIntervalMinutes": 60,
    "isPricingBatchRunning": false,
    "automationMode": "Assisted"
  }
}
```

### Step 5.2: Admin Platform-Wide AI Background Cycles Overview
Provides complete telemetry on the 3 background services (`MonitoringScanner`, `PricingBatch`, `HistoricalIngestion`).

* **Role**: Admin (`admin@foodloop.com`)
* **Endpoint**: `GET /admin/ai-status`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": {
    "monitoringScanner": {
      "cycleName": "MonitoringScanner",
      "isRunning": false,
      "lastRunStartedAt": "2026-08-22T18:24:00+00:00",
      "lastRunCompletedAt": "2026-08-22T18:24:02+00:00",
      "status": "Success",
      "lastError": null,
      "nextRunExpectedAt": "2026-08-22T19:24:00+00:00",
      "intervalMinutes": 60
    },
    "pricingBatch": {
      "cycleName": "PricingBatch",
      "isRunning": false,
      "lastRunStartedAt": "2026-08-22T18:24:00+00:00",
      "lastRunCompletedAt": "2026-08-22T18:24:03+00:00",
      "status": "Success",
      "lastError": null,
      "nextRunExpectedAt": "2026-08-22T19:24:00+00:00",
      "intervalMinutes": 60
    },
    "historicalIngestion": {
      "cycleName": "HistoricalIngestion",
      "isRunning": false,
      "lastRunStartedAt": "2026-08-22T18:24:00+00:00",
      "lastRunCompletedAt": "2026-08-22T18:24:01+00:00",
      "status": "Success",
      "lastError": null,
      "nextRunExpectedAt": "2026-08-22T19:24:00+00:00",
      "intervalMinutes": 60
    },
    "nextUpcomingCycleAt": "2026-08-22T19:24:00+00:00"
  }
}
```

---

## 💡 Scenario 6: Dynamic Pricing in Assisted Mode (Merchant Approvals)

Tests the human-in-the-loop workflow where AI suggests markdowns and presents complete financial numbers for merchant review.

### Step 6.1: Configure Assisted Mode for Store (Merchant)
* **Endpoint**: `PATCH /stores/me/ai-settings`
* **Request Body**:
```json
{
  "automationMode": "Assisted",
  "aiAutoDiscountEnabled": true,
  "aiAutoDiscountPercent": 15,
  "aiAutoDiscountDaysBeforeExpiry": 3,
  "aiAutoPricingEnabled": true
}
```

### Step 6.2: Run Pricing Recommendation Batch Sweep (Admin)
* **Role**: Admin
* **Endpoint**: `POST /admin/pricing-batch`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": "AI pricing batch sweep completed successfully."
}
```

### Step 6.3: Fetch Pending AI Recommendations with Rich Numbers (Merchant)
Returns complete pricing details (`originalPrice`, `currentPrice`, `recommendedPrice`, `discountAmount`, `quantityAvailable`, `expirationDate`, `productImageUrl`, `riskLevel`) for each pending item:

* **Role**: Merchant (`merchant.spinneys@example.com`)
* **Endpoint**: `GET /stores/me/ai-recommendations`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": [
    {
      "id": "e749f988-f9cd-4f2c-aa7d-7d5605c58c74",
      "productId": "01e48ed0-4322-4153-bc3d-c50f3e2d7ee1",
      "productName": "Fresh Pasteurised Milk 1L",
      "originalPrice": 50.00,
      "currentPrice": 45.00,
      "recommendedPrice": 40.00,
      "discountPercentage": 10.0,
      "discountAmount": 5.00,
      "quantityAvailable": 18,
      "expirationDate": "2026-08-24",
      "daysRemaining": 2,
      "productImageUrl": "https://images.example.com/milk.jpg",
      "riskLevel": "CRITICAL",
      "reason": "1 day remaining with 18 units in stock. Recommending 10% markdown grounded on 4 similar SOLD_OUT historical episodes.",
      "confidence": 0.94,
      "actionRequirement": "APPROVAL_REQUIRED",
      "actionReason": "Store is in Assisted Mode.",
      "status": "Pending",
      "correlationId": "corr-48190",
      "createdAt": "2026-08-22T18:00:00Z"
    }
  ]
}
```

### Step 6.4: Approve Recommendation (Merchant)
* **Endpoint**: `POST /stores/me/ai-recommendations/e749f988-f9cd-4f2c-aa7d-7d5605c58c74/approve`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "message": "Recommendation approved successfully.",
  "data": null
}
```
*(The product's `DiscountedPrice` is updated to 40.00, and a new record is created in `PriceHistories`).*

### Step 6.5: Reject Recommendation with Audit Reason (Merchant)
* **Endpoint**: `POST /stores/me/ai-recommendations/{id}/reject`
* **Request Body**:
```json
{
  "reason": "Bundling with breakfast combo promotion instead."
}
```

### Step 6.6: Verify Double-Approval Conflict Guard
* Attempt to approve the already-processed recommendation again:
* **Endpoint**: `POST /stores/me/ai-recommendations/e749f988-f9cd-4f2c-aa7d-7d5605c58c74/approve`
* **Expected Response (`409 Conflict`)**: Rejection preventing double-price mutations.

---

## ⚡ Scenario 7: Dynamic Pricing in Autonomous Mode (Auto-Execution)

Tests fully autonomous AI markdown execution.

### Step 7.1: Set Store to Autonomous Mode (Merchant)
* **Endpoint**: `PATCH /stores/me/ai-settings`
* **Request Body**:
```json
{
  "automationMode": "Autonomous",
  "aiAutoDiscountEnabled": true,
  "aiAutoDiscountPercent": 15,
  "aiAutoDiscountDaysBeforeExpiry": 3,
  "aiAutoPricingEnabled": true
}
```

### Step 7.2: Trigger Batch Execution (Admin)
* **Role**: Admin
* **Endpoint**: `POST /admin/pricing-batch`

### Step 7.3: Verify Auto-Applied Prices (Merchant)
* **Endpoint**: `GET /stores/me/products`
* All eligible products will reflect updated `DiscountedPrice` with status `Active`, having bypassed manual approval queues.

---

## 🛡️ Scenario 8: Price Floor Protection & Manual Markdown Auditing

### Step 8.1: View Store Pricing Health Summary (Merchant)
* **Endpoint**: `GET /stores/me/products/pricing`
* **Expected Response**:
```json
{
  "success": true,
  "data": {
    "totalProducts": 32,
    "discountedProducts": 14,
    "averageDiscountPercentage": 12.5,
    "estimatedLossPrevented": 4250.00
  }
}
```

### Step 8.2: Apply Manual Discount with Reason (Merchant)
* **Endpoint**: `PATCH /stores/me/products/01e48ed0-4322-4153-bc3d-c50f3e2d7ee1/discount`
* **Request Body**:
```json
{
  "discountedPrice": 38.00,
  "changeReason": "Flash sale: 2 hours to store closing."
}
```

### Step 8.3: Audit Price History Log (Merchant)
* **Endpoint**: `GET /stores/me/products/01e48ed0-4322-4153-bc3d-c50f3e2d7ee1/price-history`
* **Expected Response**:
```json
{
  "success": true,
  "data": [
    {
      "id": "6a9f8b22-8321-4d1a-8c90-2bf01e389df1",
      "oldPrice": 45.00,
      "newPrice": 38.00,
      "discountPercentage": 15.5,
      "changeReason": "Flash sale: 2 hours to store closing.",
      "changedAt": "2026-08-22T17:00:00Z"
    }
  ]
}
```

---

## 📚 Scenario 9: RAG Vector Knowledge Ingestion & Episode Correction

### Step 9.1: Ingest Closed Sales Episodes into Qdrant (Admin)
* **Endpoint**: `POST /admin/historical-ingestion`
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": "Historical ingestion sweep completed successfully."
}
```

### Step 9.2: Correct a Historical Episode (Admin)
* **Endpoint**: `POST /admin/historical-episodes/correct`
* **Request Body**:
```json
{
  "productId": "01e48ed0-4322-4153-bc3d-c50f3e2d7ee1",
  "newFinalUnitsSold": 45,
  "newOutcome": "SOLD_OUT",
  "auditReason": "Manual stock count verified all units sold prior to expiration."
}
```
* **Expected Response (`200 OK`)**:
```json
{
  "success": true,
  "data": "Historical episode corrected successfully. It is now eligible for re-ingestion."
}
```

---

## 🛡️ Scenario 10: Low-Confidence AI Moderation & Admin Governance

### Step 10.1: Query Flagged Low-Confidence AI Submissions (Admin)
* **Endpoint**: `GET /admin/products/pending-ai?confidenceThreshold=0.85`
* **Expected Response**: List of products whose OCR extraction confidence fell below 85%.

### Step 10.2: Moderate Product (Approve / Reject / Request Changes) (Admin)

#### Approve:
* `PATCH /admin/products/{id}/approve`

#### Reject:
* `PATCH /admin/products/{id}/reject`
* **Request Body**:
```json
{
  "note": "Label image is out of focus. Please re-upload a clear photograph of the expiration date."
}
```

#### Request Changes:
* `PATCH /admin/products/{id}/request-changes`
* **Request Body**:
```json
{
  "note": "Incorrect category detected. Please reassign from Bakery to Dairy."
}
```

---

## 🌐 Scenario 11: Direct AWS EC2 AI Microservice Contract Testing

Direct HTTP endpoints on AWS EC2 (`http://3.94.7.125:8000`):

### 1. Liveness Health Check
* `GET http://3.94.7.125:8000/health`
* **Response**: `{"status": "ok"}`

### 2. Readiness Check
* `GET http://3.94.7.125:8000/ready`
* **Response**: `{"status": "ready", "checks": {"configuration": "ok", "vector_store_provider": "memory"}}`

### 3. Service Version
* `GET http://3.94.7.125:8000/version`
* **Response**: `{"app_name": "FoodLoop AI Service", "version": "1.0.0"}`

### 4. Direct Inventory Risk Analysis
* **Endpoint**: `POST http://3.94.7.125:8000/api/v1/monitoring/analyze`
* **Request Body**:
```json
{
  "product": {
    "id": "prod-100",
    "title": "Fresh Pasteurised Milk 1L",
    "category": "Dairy"
  },
  "inventory": {
    "quantity": 40,
    "original_price": 45.0,
    "current_price": 45.0,
    "price_floor": 25.0
  },
  "demand": {
    "daily_sales_velocity": 2.5,
    "historical_sales": {
      "average_daily_sales": 4.0
    }
  },
  "expiry": {
    "expiration_date": "2026-08-22T15:00:00Z",
    "hours_remaining": 24.0
  },
  "location": {
    "latitude": 30.0444,
    "longitude": 31.2357,
    "store_id": "store-cairo-01"
  },
  "store_policy": {
    "store_id": "store-cairo-01",
    "operating_mode": "autonomous"
  },
  "timestamp": "2026-08-21T15:00:00Z"
}
```
* **Response (`200 OK`)**:
```json
{
  "route": "PRICING",
  "risk_level": "CRITICAL",
  "reason": "HIGH expiry_pressure and HIGH inventory_pressure indicate critical risk.",
  "confidence": 1.0
}
```

### 5. Direct Batch Pricing Recommendation
* **Endpoint**: `POST http://3.94.7.125:8000/api/v1/pricing/recommend`
* **Request Body**:
```json
{
  "store_id": "store-cairo-01",
  "store_policy": {
    "store_id": "store-cairo-01",
    "operating_mode": "autonomous"
  },
  "products": [
    {
      "product_id": "prod-100",
      "product_name": "Fresh Pasteurised Milk 1L",
      "category": "Dairy",
      "inventory": {
        "quantity": 40,
        "original_price": 45.0,
        "current_price": 45.0,
        "price_floor": 25.0
      },
      "demand": {
        "daily_sales_velocity": 2.5,
        "historical_sales": {
          "average_daily_sales": 4.0
        }
      },
      "expiry": {
        "expiration_date": "2026-08-22T15:00:00Z",
        "hours_remaining": 24.0
      },
      "risk_assessment": {
        "risk_level": "CRITICAL",
        "reason": "1 day to expiry with high stock",
        "confidence": 0.95
      }
    }
  ]
}
```
* **Response (`200 OK`)**:
```json
{
  "store_id": "store-cairo-01",
  "decisions": [
    {
      "product_id": "prod-100",
      "discount_percentage": 10.0,
      "confidence": 0.95,
      "reason": "High expiry pressure with 24 hours remaining. Recommending a 10% discount to accelerate sell-through.",
      "action_requirement": "AUTOMATIC_EXECUTION_ELIGIBLE"
    }
  ]
}
```

### 6. Direct Historical RAG Ingestion
* **Endpoint**: `POST http://3.94.7.125:8000/api/v1/pricing/knowledge/ingest`
* **Request Body**:
```json
{
  "events": [
    {
      "event_id": "8f3b2100-4521-4133-9c88-1af01e389bc4",
      "store_id": "store-cairo-01",
      "product_id": "prod-100",
      "category": "Dairy",
      "recorded_at": "2026-08-21T15:00:00Z",
      "quantity": 50,
      "current_price": 40.0,
      "original_price": 50.0,
      "price_floor": 25.0,
      "sales_velocity": 5.0,
      "historical_average_daily_sales": 6.0,
      "hours_remaining": 24.0,
      "discount_percentage": 15.0,
      "units_sold_after_discount": 48,
      "sell_through_rate": 0.96,
      "outcome": "SOLD_OUT"
    }
  ]
}
```
* **Response (`200 OK`)**:
```json
{
  "accepted_count": 1,
  "upserted_count": 1,
  "failed_count": 0,
  "document_ids": ["8f3b2100-4521-4133-9c88-1af01e389bc4"]
}
```
