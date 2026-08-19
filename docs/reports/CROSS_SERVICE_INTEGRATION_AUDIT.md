# Cross-Service Communication & Contract Verification Audit

This audit validates the contract fidelity, schema alignment, tracing mechanisms, safety guardrails, and resilience policies between the .NET 10 Clean Architecture Backend and the Python FastAPI AI Microservice.

---

## 1. Contract Compatibility Matrix

The table below details the JSON serialization alignment between the .NET DTO models (serialized using lower snake case: `JsonNamingPolicy.SnakeCaseLower`) and the FastAPI Pydantic v2 schemas.

| .NET DTO / Field Name | .NET Type | FastAPI Pydantic Field | FastAPI Type | Serialization Key (`snake_case`) |
| :--- | :--- | :--- | :--- | :--- |
| **`MonitoringRequestDto`** | `record` | **`MonitoringRequest`** | `class` | - |
| `Product` | `ProductMetadataDto` | `product` | `ProductMetadata` | `product` |
| `Inventory` | `InventoryMetricsDto` | `inventory` | `InventoryMetrics` | `inventory` |
| `Demand` | `DemandContextDto` | `demand` | `DemandContext` | `demand` |
| `Expiry` | `ExpiryContextDto` | `expiry` | `ExpiryContext` | `expiry` |
| `Location` | `LocationContextDto` | `location` | `LocationContext` | `location` |
| `StorePolicy` | `StorePolicyDto?` | `store_policy` | `StorePolicy | None` | `store_policy` |
| `Timestamp` | `DateTimeOffset` | `timestamp` | `datetime` | `timestamp` |
| **`MonitoringResponseDto`**| `record` | **`MonitoringResponse`**| `class` | - |
| `Route` | `string` (Enum) | `route` | `Route` (Enum) | `route` |
| `RiskLevel` | `string` (Enum) | `risk_level` | `RiskLevel` (Enum) | `risk_level` |
| `Reason` | `string` | `reason` | `str` | `reason` |
| `Confidence` | `double` | `confidence` | `float` | `confidence` |
| **`PricingBatchRequestDto`**| `record` | **`PricingBatchRequest`**| `class` | - |
| `StoreId` | `string` | `store_id` | `str` | `store_id` |
| `StorePolicy` | `StorePolicyDto?` | `store_policy` | `StorePolicy | None` | `store_policy` |
| `Products` | `IReadOnlyList<...>` | `products` | `list[PricingProductContext]`| `products` |
| **`PricingBatchResponseDto`**| `record` | **`PricingBatchResponse`**| `class` | - |
| `StoreId` | `string` | `store_id` | `str` | `store_id` |
| `Decisions` | `IReadOnlyList<...>` | `decisions` | `list[PricingDecision]` | `decisions` |
| **`HistoricalIngestionRequestDto`**| `record` | **`HistoricalPricingIngestionRequest`**| `class` | - |
| `Events` | `IReadOnlyList<...>` | `events` | `list[HistoricalPricingEvent]`| `events` |
| **`HistoricalIngestionResponseDto`**| `record` | **`HistoricalPricingIngestionResponse`**| `class` | - |
| `AcceptedCount` | `int` | `accepted_count` | `int` | `accepted_count` |
| `UpsertedCount` | `int` | `upserted_count` | `int` | `upserted_count` |
| `FailedCount` | `int` | `failed_count` | `int` | `failed_count` |
| `DocumentIds` | `IReadOnlyList<string>` | `document_ids` | `list[str]` | `document_ids` |

---

## 2. Route, Method, & Header Mapping Table

The table below maps the .NET client method signatures to the physical HTTP routing endpoints in the AI service, including trace propagation and time limit policies.

| .NET Client Method | HTTP Verb | Route Path | Auth Requirement | Correlation Headers | Timeout |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `GetHealthAsync()` | `GET` | `/health` | None | `X-Correlation-ID` | 3 seconds |
| `GetReadyAsync()` | `GET` | `/ready` | None | `X-Correlation-ID` | 3 seconds |
| `GetVersionAsync()` | `GET` | `/version` | None | `X-Correlation-ID` | 3 seconds |
| `AnalyzeMonitoringAsync(...)` | `POST` | `/api/v1/monitoring/analyze` | None | `X-Correlation-ID` | 30 seconds |
| `RecommendPricingAsync(...)` | `POST` | `/api/v1/pricing/recommend` | None | `X-Correlation-ID` | 30 seconds |
| `IngestHistoricalPricingAsync(...)` | `POST`| `/api/v1/pricing/knowledge/ingest` | None | `X-Correlation-ID` | 30 seconds |

---

## 3. Header Tracing & Correlation Middleware

*   **Propagation (.NET Backend)**: The .NET `CorrelationIdDelegatingHandler` intercepts all outgoing requests from `AiServiceClient` and attaches the `X-Correlation-ID` header sourced from `ICorrelationIdAccessor`.
*   **Correlation Tracking (FastAPI)**: The FastAPI `CorrelationIdMiddleware` intercepts all incoming requests, extracts the `X-Correlation-ID` header, sets it in request state context, and automatically attaches it back to the response headers for complete tracing logs.

---

## 4. Resilience, Invariant, & Guardrail Verification

### Discount Ceiling Guard
*   Both services enforce a strict ceiling limit:
    $$\text{DiscountPercentage} \le 15.0\%$$
*   If the AI service recommendation breaches this limit ($>15.0\%$), the .NET `AiServiceClient` throws `AiServiceContractException` to abort transaction execution.

### Confidence Range Guard
*   The `Confidence` score must be strictly within:
    $$\text{Confidence} \in [0.0, 1.0]$$
*   If out-of-bounds, the client immediately throws `AiServiceContractException`.

### Validation Error Mapping
*   If the request fails validation on the FastAPI microservice (Pydantic rule violation), FastAPI returns `HTTP 422 Unprocessable Entity`.
*   The .NET client intercepts this and maps it to `AiServiceValidationException` which extracts the raw validation error body for structured logging.

### Polly v8 Circuit Breaker Status
*   **`AiServiceBusinessPipeline`**:
    *   *Retry*: Retries up to 3 times, exponential backoff with jitter.
    *   *Timeout*: Max 30 seconds execution limit.
    *   *Circuit Breaker*: Set to open if failure ratio exceeds 50% (minimum 5 calls sampling window over 60s) with a 30s break duration, raising `AiServiceUnavailableException`.
*   **`AiServiceHealthPipeline`**:
    *   *Retry*: Retries at most once, constant backoff.
    *   *Timeout*: Fails fast in 3 seconds.

---

## 5. Verification Status & Test Execution Results

All unit and integration test suites run against the respective projects have executed successfully.

### 1. .NET 10 Backend Test Suite
```text
Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 64 ms - FoodLoop.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 139 ms - FoodLoop.Application.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:   208, Skipped:     0, Total:   208, Duration: 18 s - FoodLoop.Infrastructure.Tests.dll (net10.0)

Total Test Suite Status: 247 Passed, 0 Failed, 100% Success Rate.
```

### 2. Python FastAPI AI Service Test Suite
```text
tests/test_bge_m3_embeddings.py .........s
tests/test_context_analysis.py ..........
tests/test_context_tools.py .......
tests/test_e2e_scenarios.py .............
tests/test_embeddings.py ..............
tests/test_final_hardening.py ........
tests/test_health.py .
tests/test_historical_pricing.py .....................
tests/test_historical_pricing_ingestion.py ......................s
tests/test_live_historical_ingestion.py s
tests/test_llm_factory.py ....
tests/test_llm_live.py s
tests/test_monitoring_agent.py ......
tests/test_monitoring_api.py ..
tests/test_monitoring_schema.py ............
tests/test_nager_holidays.py ................s
tests/test_open_meteo_weather.py .........s
tests/test_pricing_agent.py ...........
tests/test_pricing_api.py ..
tests/test_pricing_recommendation_scenarios.py ...................
tests/test_pricing_retrieval.py ...................
tests/test_pricing_schema.py ...........
tests/test_pricing_signals.py ........
tests/test_qdrant_live.py s
tests/test_qdrant_vector_store.py ...................
tests/test_risk_assessment.py .....
tests/test_risk_signals.py ......
tests/test_routing.py ......
tests/test_store_policy.py ........
tests/test_vector_store.py .......................

================= 291 passed, 7 skipped, 4 warnings in 54.34s =================
```
