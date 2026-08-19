# Step 2 Test Report: AI Service HTTP Client Infrastructure

This report documents the results of executing the unit, serialization, and HTTP resilience test suite following the implementation of the AI Service HTTP Client Infrastructure in Step 2.

## Test Summary

| Layer | Existing (Step 1) | New (Step 2) | Total | Passing |
|---|---|---|---|---|
| Domain | 28 | 0 | 28 | 28/28 |
| Application | 5 | 6 | 11 | 11/11 |
| Infrastructure | 44 | 14 | 58 | 58/58 |
| **Total** | **77** | **20** | **97** | **100%** |

- **Total Test Projects Executed:** 3
- **Total Tests Passed:** 97
- **Total Tests Failed:** 0
- **Total Tests Skipped:** 0
- **Pass Rate:** 100% (No Regressions)

---

## Test Executed Breakdown

### 1. Domain Entities & Validation Tests (`FoodLoop.Domain.Tests`)
- **Passed Tests:** 28 / 28 (Baseline)

### 2. Application Serialization & Mapping Tests (`FoodLoop.Application.Tests`)
- **Passed Tests:** 11 / 11
- **New Coverage (6 tests):**
  - DTO Serialization - `MonitoringResponseDto`: round-trip test against literal fixture from `AI_Report.md §9.1`.
  - DTO Serialization - `PricingBatchResponseDto`: round-trip test against literal Cairo store Cairo decisions fixture from `AI_Report.md §9.2`.
  - DTO Serialization - `PricingBatchRequestDto`: round-trip test against literal input structure from `AI_Report.md §9.3`.
  - DTO Serialization - `MonitoringRequestDto`: round-trip test verifying exact snake_case JSON shape mapping of all nested properties.
  - Mapping Helper - Operating Mode mapping: validates `Assisted` maps to `"assisted"` and `Autonomous` maps to `"autonomous"`.
  - Mapping Helper - Manual Mode Guard: validates that mapping on `AiOperatingMode.Manual` throws `InvalidOperationException`.

### 3. Infrastructure HTTP Client & Resilience Integration Tests (`FoodLoop.Infrastructure.Tests`)
- **Passed Tests:** 58 / 58
- **New Coverage (14 tests):**
  - **Trace Correlation propagation**: verifies `X-Correlation-ID` header is attached to outgoing requests.
  - **Happy Path (AnalyzeMonitoringAsync)**: verifies request serialization and response parsing structure.
  - **Happy Path (RecommendPricingAsync)**: verifies Cairo decisions parsing and request payload mapping.
  - **Happy Path (GetHealthAsync & GetReadyAsync)**: verifies liveness/readiness parsed permissively.
  - **HTTP 422 Unprocessable Entity**: checks that raw response error body is captured and throws `AiServiceValidationException`.
  - **Transient Retry**: simulates 5xx failures recovering on 3rd attempt, verifying retry success.
  - **Exhausted Retry**: simulates 5xx failures exceeding 3 retry limits, throwing `AiServiceUnavailableException`.
  - **Circuit Breaker**: verifies circuit trips after consecutive failures, immediately failing fast on future requests.
  - **Unknown Product ID Contract Violations**: verifies pricing response containing unregistered product ID throws `AiServiceContractException`.
  - **Out-of-range Metrics Contract Violations**: checks out-of-range discount percentage (> 15.0) and confidence values throw `AiServiceContractException`.
  - **Options Validation**: checks fail-fast startup behavior of `AiServiceOptions` when `BaseUrl` is missing or malformed.
  - **Explicit Timeout Tiering (Health vs Business)**: asserts `GetHealthAsync` times out rapidly under 100ms budget, whereas `AnalyzeMonitoringAsync` succeeds.
  - **Explicit Retry Tiering (Health vs Business)**: verifies `GetHealthAsync` executes at most 1 retry, whereas `AnalyzeMonitoringAsync` retries up to 3 times on 500 errors.
