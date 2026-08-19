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

## 5. Step-by-Step Resolution Guide

### Option A: For AI Coding Agent (Claude, Cursor, Copilot)
*Prompt to paste into the agent:*
> "In this Python FastAPI AI service repository, search for all occurrences of the model string `gemma-2-27b-it` across `.env`, `docker-compose.yml`, `app/core/config.py`, `app/core/settings.py`, and `app/services/llm_factory.py`. Replace all default references with `llama-3.3-70b-versatile` (or configure it to dynamically read `os.getenv('LLM_MODEL', 'llama-3.3-70b-versatile')`). Ensure all LangChain/LangGraph model wrappers use this model name."

---

### Option B: Manual Fix on the AWS Host

#### 1. Edit the Environment File
On the AWS server where the container is deployed, open the `.env` file:
```bash
nano .env
# or: nano /app/.env
```

#### 2. Update Model Variables
Replace the old model with one of Groq's active production models:

```ini
# ==========================================
# LLM Configuration
# ==========================================
# RECOMMENDED for reasoning, JSON structure, and speed:
LLM_MODEL=llama-3.3-70b-versatile

# ALTERNATIVE (If ultra-low latency is required):
# LLM_MODEL=llama-3.1-8b-instant

# ALTERNATIVE (If Gemma family is required):
# LLM_MODEL=gemma2-9b-it
```

#### 3. Verify Codebase Default Fallbacks
Inspect `app/core/config.py` (or `app/services/llm_factory.py`) to ensure no hardcoded string overrides the `.env`:
```python
# Before:
llm_model: str = "gemma-2-27b-it"

# After:
llm_model: str = os.getenv("LLM_MODEL", "llama-3.3-70b-versatile")
```

#### 4. Restart the Service Container
```bash
# If using Docker Compose:
docker compose down && docker compose up -d

# If using standalone Docker:
docker restart <container_id_or_name>

# If using systemd:
sudo systemctl restart foodloop-ai
```

---

## 6. Verification & Test Commands

Run the following test requests against the AWS instance to verify that LLM reasoning is fully operational:

### Verification Test 1: Monitoring Analysis
```bash
curl -X POST "http://54.92.183.187:8000/api/v1/monitoring/analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "product_id": "prod-verify-01",
    "product_name": "Fresh Milk 1L",
    "category": "Dairy",
    "store_id": "store-cairo-01",
    "inventory": { "quantity": 25, "original_price": 45.0, "current_price": 45.0, "price_floor": 25.0 },
    "demand": { "sales_velocity": 2.0, "historical_average_daily_sales": 6.0 },
    "expiry": { "expires_at": "2026-08-20T12:00:00Z", "hours_remaining": 16.0 },
    "location": { "latitude": 30.0444, "longitude": 31.2357, "store_id": "store-cairo-01" },
    "timestamp": "2026-08-19T20:00:00Z"
  }'
```

**Success Criteria:**
1. Server logs display `INFO: POST /api/v1/monitoring/analyze 200 OK` with **no** `Error code: 404` or `model_not_found` warnings.
2. The `reason` field in the response contains dynamic natural language reasoning without the string `(Fallback evaluation)`.

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
