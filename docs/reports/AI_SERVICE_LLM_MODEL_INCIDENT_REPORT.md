# FoodLoop AI Microservice — LLM Model & Gateway Resolution Report

**Document Version:** 2.0.0  
**Date:** 20 August 2026  
**Target Audience:** AI Engineering Team / Autonomous AI Agent (Claude, ChatGPT, Cursor, Copilot)  
**Service Hosts:** AWS EC2 (`http://54.92.183.187:8000`, `http://184.72.169.156:8000`)  
**Status:** High Priority — Active Deterministic Fallback Triggered  

---

## 1. Executive Summary

The FoodLoop AI Microservice deployed on AWS is currently bypassing its primary LLM inference requests and falling back to internal deterministic rule-based algorithms.

During investigation and live testing against `http://184.72.169.156:8000`, two root causes were identified:
1. **Upstream Model Deprecation:** The previous model `gemma-2-27b-it` was removed from Groq's active model catalog.
2. **Gateway Route & Schema Mismatch (ITI Student API):** When attempting to use the ITI gateway (`apiaccess.iti.net.eg`), configuring standard `OPENAI_BASE_URL="http://apiaccess.iti.net.eg/api/v1"` caused `404 - {'detail': 'Not Found'}` because standard OpenAI clients append `/chat/completions`, whereas the ITI gateway exposes `/student/chat` with a custom payload schema.

Because the AI microservice was engineered with fallback handlers, the API does not crash and continues returning HTTP `200 OK`. However, **all dynamic natural-language reasoning, multi-context weighting, and LLM discount synthesis are bypassed** with fallback strings like `"(Fallback evaluation)"` and `"(rule-based fallback)"`.

---

## 2. Observed Error Logs & Root Cause Analysis

### 2.1 Live Server Log Traces
```text
INFO:     156.195.194.54:52586 - "GET /docs HTTP/1.1" 200 OK
INFO:     156.195.194.54:52586 - "GET /openapi.json HTTP/1.1" 200 OK
Context analysis LLM failed (Error code: 404 - {'detail': 'Not Found'}), falling back to deterministic sufficiency check.
Risk assessment LLM failed (Error code: 404 - {'detail': 'Not Found'}), falling back to deterministic risk signals.
INFO:     156.195.194.54:60774 - "POST /api/v1/monitoring/analyze HTTP/1.1" 200 OK
Pricing recommendation LLM failed (Error code: 404 - {'detail': 'Not Found'}), falling back to deterministic business pricing logic.
INFO:     156.195.194.54:58458 - "POST /api/v1/pricing/recommend HTTP/1.1" 200 OK
```

### 2.2 Root Cause 1: Groq Catalog (`gemma-2-27b-it` Deprecation)
On Groq's official API (`https://api.groq.com/openai/v1`), `gemma-2-27b-it` has been retired. Requesting it returns `model_not_found`.

### 2.3 Root Cause 2: ITI Student Gateway Mismatch (`{'detail': 'Not Found'}`)
The ITI API Gateway at `http://apiaccess.iti.net.eg` is a custom FastAPI service that does **not** adhere to standard OpenAI URL routing or schemas:

| Attribute | Standard OpenAI Client (LangChain / `ChatOpenAI`) | ITI Gateway (`apiaccess.iti.net.eg`) |
| :--- | :--- | :--- |
| **Endpoint URL** | Automatically appends `/chat/completions` (e.g. `http://apiaccess.iti.net.eg/api/v1/chat/completions` ➔ **404**) | **`http://apiaccess.iti.net.eg/api/v1/student/chat`** |
| **Model Field** | `"model": "..."` | `"model_id": "google.gemma-3-27b-it"` |
| **System Prompt** | Sent as message `{"role": "system", "content": "..."}` | Sent as top-level field `"system_prompt": "..."` |
| **Response Field** | `choices[0].message.content` | `output_text` |

#### Verified Working ITI Curl Request:
```bash
curl -X POST "http://apiaccess.iti.net.eg/api/v1/student/chat" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <API_KEY>" \
  -d '{
    "model_id": "google.gemma-3-27b-it",
    "messages": [{"role": "user", "content": "Hello, are you working?"}],
    "system_prompt": "You are a helpful AI assistant."
  }'
```
**Response:**
```json
{
  "request_id": "73061c7f-a6d1-46e3-a3cc-862c01b989fa",
  "model_id": "google.gemma-3-27b-it",
  "region": "us-east-1",
  "output_text": "Yes, I am! I'm always working, ready to help. 😊",
  "usage": { "input_tokens": 23, "output_tokens": 35, "total_tokens": 58, "fallback_used": false },
  "status": "active"
}
```

---

## 3. Backend Impact & Current Mitigation

### 3.1 Backend Fail-Closed Moderation Safeguard
While the AI microservice is in fallback mode, the backend (`FoodLoop.API`) has implemented the following security mitigation:
- In `CreateProductCommandHandler`, the `ExpiryVerificationState` field is client-supplied but is **strictly ignored for automatic product activation**.
- All newly created products are forced to `ProductStatus.PendingModeration` to prevent client-side bypass of the moderation queue and ensure the admin `ProductUploaded` notification always fires.
- Once the AI microservice restores true LLM reasoning and OCR verification, the backend can re-enable automated `Active` transitions for high-confidence predictions.

---

## 4. Actionable Fix Options for the AI Service Team

The AI Service team can choose one of the following two implementation paths:

---

### Option A: Use Groq Directly (Recommended for Zero Code Changes)
If you want to use standard LangChain / `ChatOpenAI` without custom HTTP client adapters:

1. **Update `.env`:**
   ```ini
   LLM_PROVIDER=groq
   LLM_MODEL=llama-3.3-70b-versatile
   GROQ_API_KEY=gsk_your_active_groq_key_here
   # Remove any custom OPENAI_BASE_URL pointing to iti.net.eg
   ```
2. **Update `app/core/config.py` default:**
   ```python
   llm_model: str = os.getenv("LLM_MODEL", "llama-3.3-70b-versatile")
   ```

---

### Option B: Use the ITI Student Gateway (`google.gemma-3-27b-it`)
If you are required to use the ITI gateway at `http://apiaccess.iti.net.eg/api/v1/student/chat`, implement a custom client adapter in the AI service:

1. **`app/services/iti_llm_client.py`:**
   ```python
   import httpx
   from app.core.config import settings

   class ITIStudentChatClient:
       def __init__(self):
           self.url = f"{settings.OPENAI_BASE_URL.rstrip('/')}/student/chat"
           self.headers = {
               "Content-Type": "application/json",
               "Authorization": f"Bearer {settings.OPENAI_API_KEY}"
           }
           self.model_id = getattr(settings, "OPENAI_MODEL", "google.gemma-3-27b-it")

       async def generate(self, user_prompt: str, system_prompt: str = "You are a helpful AI pricing assistant.") -> str:
           payload = {
               "model_id": self.model_id,
               "messages": [{"role": "user", "content": user_prompt}],
               "system_prompt": system_prompt
           }
           async with httpx.AsyncClient(timeout=settings.OPENAI_TIMEOUT_SECONDS) as client:
               response = await client.post(self.url, json=payload, headers=self.headers)
               response.raise_for_status()
               data = response.json()
               return data["output_text"]
   ```

2. **`.env` configuration for Option B:**
   ```ini
   OPENAI_BASE_URL=http://apiaccess.iti.net.eg/api/v1
   OPENAI_MODEL=google.gemma-3-27b-it
   OPENAI_API_KEY=sbg_your_iti_token_here
   OPENAI_TIMEOUT_SECONDS=30.0
   ```

---

## 5. Verification Checklist & Acceptance Criteria

After deploying either Option A or Option B, run the following verification checks:

### 1. Test Monitoring Analysis:
```bash
curl -X POST "http://localhost:8000/api/v1/monitoring/analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "product": { "id": "prod-001", "name": "Whole Milk", "category": "Dairy" },
    "inventory": { "quantity": 8, "original_price": 45.0, "current_price": 45.0, "price_floor": 30.0 },
    "demand": { "sales_velocity": 0.2, "historical_sales": { "average_daily_sales": 3.0 } },
    "expiry": { "expires_at": "2026-08-20T14:00:00Z", "hours_remaining": 14.0 },
    "location": { "latitude": 30.0444, "longitude": 31.2357, "store_id": "store-001" },
    "store_policy": { "store_id": "store-001", "operating_mode": "autonomous" },
    "timestamp": "2026-08-20T00:00:00Z"
  }'
```

### 2. Test Pricing Recommendation:
```bash
curl -X POST "http://localhost:8000/api/v1/pricing/recommend" \
  -H "Content-Type: application/json" \
  -d '{
    "store_id": "store-001",
    "store_policy": { "store_id": "store-001", "operating_mode": "autonomous" },
    "products": [
      {
        "product_id": "prod-001",
        "product_name": "Whole Milk",
        "category": "Dairy",
        "inventory": { "quantity": 8, "original_price": 45.0, "current_price": 45.0, "price_floor": 30.0 },
        "demand": { "sales_velocity": 0.2, "historical_sales": { "average_daily_sales": 3.0 } },
        "expiry": { "expires_at": "2026-08-20T14:00:00Z", "hours_remaining": 14.0 },
        "risk_assessment": { "risk_level": "HIGH", "reason": "Low velocity with 14h remaining", "confidence": 0.9 }
      }
    ]
  }'
```

### 3. Acceptance Sign-Off Criteria:
- [ ] Container logs show **ZERO** `Error code: 404` or `Context analysis LLM failed`.
- [ ] Response `reason` field does **NOT** contain `(Fallback evaluation)` or `(rule-based fallback)`.
- [ ] Response contains genuine dynamic LLM reasoning text.
