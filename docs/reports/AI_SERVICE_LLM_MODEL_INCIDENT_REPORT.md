# FoodLoop AI Microservice — LLM Model 404 Incident Report & Fix Guide

**Document Version:** 1.0.0  
**Date:** 19 August 2026  
**Target Audience:** AI Engineering Lead / Autonomous AI Agent (Claude, ChatGPT, Cursor, Copilot)  
**Service Host:** AWS EC2 (`http://54.92.183.187:8000`)  
**Status:** High Priority — Active Deterministic Fallback Triggered  

---

## 1. Executive Summary

The FoodLoop AI Microservice deployed on AWS at `http://54.92.183.187:8000` is currently failing its primary LLM inference requests due to an upstream **HTTP 404 `model_not_found`** exception. 

The service is attempting to invoke `gemma-2-27b-it`, which was recently deprecated / removed from Groq's active model catalog. 

Because the microservice was engineered with high-resilience fallback handlers, the API does not crash and continues returning HTTP `200 OK` via its deterministic rule-based algorithms. However, **all dynamic natural-language reasoning, multi-context weighting, and LLM discount synthesis are completely bypassed**.

---

## 2. Observed Error Logs & Exceptions

### 2.1 Server Console Log Traces (AWS Container)

```text
INFO:     127.0.0.1:36804 - "GET /health HTTP/1.1" 200 OK

Context analysis LLM failed (Error code: 404 - {'error': {'code': 'model_not_found', 'message': 'The model gemma-2-27b-it does not exist or you do not have access to it.', 'param': 'model', 'type': 'invalid_request_error'}, 'request_id': 'a31c648c'}), falling back to deterministic sufficiency check.

Risk assessment LLM failed (Error code: 404 - {'error': {'code': 'model_not_found', 'message': 'The model gemma-2-27b-it does not exist or you do not have access to it.', 'param': 'model', 'type': 'invalid_request_error'}, 'request_id': '2cda6741'}), falling back to deterministic risk signals.

INFO:     156.195.194.54:53164 - "POST /api/v1/monitoring/analyze HTTP/1.1" 200 OK

Pricing recommendation LLM failed (Error code: 404 - {'error': {'code': 'model_not_found', 'message': 'The model gemma-2-27b-it does not exist or you do not have access to it.', 'param': 'model', 'type': 'invalid_request_error'}, 'request_id': '95e4aa38'}), falling back to deterministic business pricing logic.

INFO:     156.195.194.54:53164 - "POST /api/v1/pricing/recommend HTTP/1.1" 200 OK
```

### 2.2 Upstream Provider Exception Payload
When the application attempts to initialize or invoke the chat model:
```json
{
  "error": {
    "code": "model_not_found",
    "message": "The model gemma-2-27b-it does not exist or you do not have access to it.",
    "param": "model",
    "type": "invalid_request_error"
  },
  "request_id": "a31c648c"
}
```

---

## 3. Impacted Endpoints & Pipeline Symptoms

| Endpoint | Intended Behavior (Tier 1) | Current Behavior (Tier 2 Fallback) |
| :--- | :--- | :--- |
| `POST /api/v1/monitoring/analyze` | Evaluates inventory context, holiday/weather external data, and uses LLM to generate reasoned risk assessment. | Catches 404 $\rightarrow$ executes rule-based formula $\rightarrow$ appends `"(Fallback evaluation)"` to `reason`. |
| `POST /api/v1/pricing/recommend` | Queries Qdrant vector store, retrieves historical episodes, and uses LLM to generate optimal discount % (0–15%) and explanation. | Catches 404 $\rightarrow$ executes linear discount scaling $\rightarrow$ appends `"(rule-based fallback)"` to `reason`. |

### Sample Response Indicating Fallback State
```json
{
  "route": "PRICING",
  "risk_level": "CRITICAL",
  "reason": "CRITICAL risk: 16.0 hours remaining, high inventory pressure. (Fallback evaluation)",
  "confidence": 0.8
}
```

---

## 4. Root Cause

1. **Provider Catalog Update:** Groq removed the model ID `gemma-2-27b-it` from its active API endpoints.
2. **Hardcoded or Stale Configuration:** The environment variable `LLM_MODEL` (or internal default in `app/core/config.py` / `llm_factory.py`) is set to `gemma-2-27b-it`.

---

## 5. Step-by-Step Resolution Guide (For AI Service Repository)

> **Workflow Note:** You only need to update the AI service codebase locally in your repository, test it locally, and push your changes to Git. The deployment team will handle pulling and restarting on the AWS server.

### Step 1: Update Model Configuration in Code & Config Files
In your local AI service codebase, replace all occurrences of `gemma-2-27b-it` with `llama-3.3-70b-versatile`:

1. **`app/core/config.py` (or `settings.py`):**
   Ensure the default model fallback is updated:
   ```python
   # Before:
   llm_model: str = "gemma-2-27b-it"

   # After:
   llm_model: str = os.getenv("LLM_MODEL", "llama-3.3-70b-versatile")
   ```

2. **`.env` & `.env.example`:**
   ```ini
   # LLM Model Configuration
   LLM_MODEL=llama-3.3-70b-versatile
   ```

3. **`docker-compose.yml` (if applicable):**
   Update the default environment block:
   ```yaml
   environment:
     - LLM_MODEL=llama-3.3-70b-versatile
   ```

4. **`app/services/llm_factory.py` (or where LLM instances are initialized):**
   Ensure the LangChain/Groq model factory uses `settings.LLM_MODEL` and has no hardcoded fallback strings to `gemma-2-27b-it`.

---

### Step 2: Local Verification (Before Pushing to Git)

1. **Run Local Unit & Integration Tests:**
   ```bash
   pytest
   ```

2. **Start the Service Locally:**
   ```bash
   uvicorn app.main:app --reload --port 8000
   ```

3. **Test Local Monitoring Endpoint:**
   ```bash
   curl -X POST "http://localhost:8000/api/v1/monitoring/analyze" \
     -H "Content-Type: application/json" \
     -d '{
       "product_id": "prod-verify-01",
       "product_name": "Fresh Whole Milk 1L",
       "category": "Dairy",
       "store_id": "store-cairo-01",
       "inventory": { "quantity": 25, "original_price": 45.0, "current_price": 45.0, "price_floor": 25.0 },
       "demand": { "sales_velocity": 2.0, "historical_average_daily_sales": 6.0 },
       "expiry": { "expires_at": "2026-08-20T12:00:00Z", "hours_remaining": 16.0 },
       "location": { "latitude": 30.0444, "longitude": 31.2357, "store_id": "store-cairo-01" },
       "timestamp": "2026-08-19T20:00:00Z"
     }'
   ```

4. **Test Local Pricing Recommendation Endpoint:**
   ```bash
   curl -X POST "http://localhost:8000/api/v1/pricing/recommend" \
     -H "Content-Type: application/json" \
     -d '{
       "store_id": "store-cairo-01",
       "store_policy": { "store_id": "store-cairo-01", "operating_mode": "autonomous" },
       "products": [
         {
           "product_id": "prod-verify-01",
           "product_name": "Fresh Whole Milk 1L",
           "category": "Dairy",
           "inventory": { "quantity": 25, "original_price": 45.0, "current_price": 45.0, "price_floor": 25.0 },
           "demand": { "sales_velocity": 2.0, "historical_average_daily_sales": 6.0 },
           "expiry": { "expires_at": "2026-08-20T12:00:00Z", "hours_remaining": 16.0 }
         }
       ]
     }'
   ```

---

### Step 3: Git Commit & Push
Once local verification passes with zero fallback logs:
```bash
git add .
git commit -m "fix(llm): update default LLM model from deprecated gemma-2-27b-it to llama-3.3-70b-versatile"
git push origin <your-branch>
```
Notify the deployment team with your commit hash so they can pull the updates to the server.

---

### Verification Test 2: Dynamic Pricing Batch Recommendation
```bash
curl -X POST "http://54.92.183.187:8000/api/v1/pricing/recommend" \
  -H "Content-Type: application/json" \
  -d '{
    "store_id": "store-cairo-01",
    "store_policy": { "store_id": "store-cairo-01", "operating_mode": "autonomous" },
    "products": [
      {
        "product_id": "prod-verify-01",
        "product_name": "Fresh Milk 1L",
        "category": "Dairy",
        "inventory": { "quantity": 25, "original_price": 45.0, "current_price": 45.0, "price_floor": 25.0 },
        "demand": { "sales_velocity": 2.0, "historical_average_daily_sales": 6.0 },
        "expiry": { "expires_at": "2026-08-20T12:00:00Z", "hours_remaining": 16.0 }
      }
    ]
  }'
```

**Success Criteria:**
1. Server logs show `200 OK` with zero LLM failure warnings.
2. The `decisions[0].reason` contains LLM generated rationale and `action_requirement` matches `"AUTOMATIC_EXECUTION_ELIGIBLE"`.

---

## 7. ⚠️ Mandatory Quality Gate & Pre-Handoff Checklist

> **IMPORTANT REQUIREMENT FOR THE ENGINEER / AI AGENT:**  
> Do **NOT** notify the backend team or mark this task complete until you have performed the following checks and verified that the service produces pure LLM-driven output without triggering fallbacks.

### ✅ Sign-Off Checklist (Must Pass All 4 Items)

- [ ] **1. Live Container Log Audit:**
  Run `docker logs -f <container_name>` (or inspect console logs) while firing the verification requests. Confirm **ZERO** occurrences of:
  * `Error code: 404`
  * `model_not_found`
  * `Context analysis LLM failed`
  * `Risk assessment LLM failed`
  * `Pricing recommendation LLM failed`
  * `falling back to deterministic`

- [ ] **2. No Fallback Strings in Responses:**
  Inspect the JSON returned by both `/api/v1/monitoring/analyze` and `/api/v1/pricing/recommend`. Verify that the `reason` field:
  * ❌ Does **NOT** contain `(Fallback evaluation)`
  * ❌ Does **NOT** contain `(rule-based fallback)`
  * ❌ Does **NOT** contain `(Fallback`
  * ✅ Contains dynamic, contextual reasoning sentences generated by Llama 3.3.

- [ ] **3. Readiness and Health Checks:**
  Ensure `GET /health` and `GET /ready` both return HTTP 200:
  ```bash
  curl -s http://54.92.183.187:8000/health
  # Expected: {"status":"ok"}
  
  curl -s http://54.92.183.187:8000/ready
  # Expected: {"status":"ready","checks":{"configuration":"ok",...}}
  ```

- [ ] **4. Handoff Confirmation Output:**
  When asking the backend team to resume testing, include the actual JSON output of the verification cURL requests in your reply as proof that Tier 1 (LLM Reasoning) is active.

