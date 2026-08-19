# Step 4 Test Report — Batch Pricing Pipeline

This report documents the verification results and architectural invariants for **Phase 4: Batch Pricing Pipeline**.

---

## 1. Test Suite Summary Table

| Test Layer Project | Number of Tests | Status | Notes |
| :--- | :---: | :---: | :--- |
| **`FoodLoop.Domain.Tests`** | 28 / 28 | **PASSED** | Core domain guards and entities. |
| **`FoodLoop.Application.Tests`** | 11 / 11 | **PASSED** | Request mapping and application CQRS commands. |
| **`FoodLoop.Infrastructure.Tests`** | 82 / 82 | **PASSED** | Includes 14 monitoring scanner tests and 10 new pricing batch tests. |
| **Total Solution Coverage** | **121 / 121** | **PASSED** | **100% Success Rate, Zero regressions.** |

---

## 2. Detailed Test Breakdown (New Step 4 Tests)

All 10 new tests are implemented in [`AiPricingBatchTests.cs`](file:///c:/ITI/server/test/FoodLoop.Infrastructure.Tests/Features/AiIntegration/AiPricingBatchTests.cs):

### 1. `Handle_should_group_candidates_correctly_by_store`
- **Objective**: Asserts that staged candidates across different store organizations are split into separate batches and processed in independent client calls.
- **Verification**: Mock client verify calls verify exactly one request per store key.

### 2. `Handle_should_persist_Pending_recommendation_and_not_mutate_price_for_Assisted_mode`
- **Objective**: Asserts that Assisted-mode recommendations default to `Pending` status and do not mutate product prices.
- **Verification**: Verifies entity saved as `Pending` and `Product.DiscountedPrice` remains unchanged.

### 3. `Handle_should_apply_discount_and_set_AutoExecuted_for_Autonomous_mode_when_above_floor`
- **Objective**: Asserts that Autonomous-mode valid recommendations mutate `Product.DiscountedPrice` and save with `AutoExecuted` status.
- **Verification**: Verifies entity saved as `AutoExecuted` and product price successfully updated to the discounted price.

### 4. `Handle_should_reject_and_not_mutate_price_for_Autonomous_mode_when_below_floor`
- **Objective**: Asserts that Autonomous-mode recommendations violating the price floor are saved with `Rejected` status and leave price unchanged.
- **Verification**: Verifies entity saved as `Rejected` with reason containing `Price Floor Violation`, product price unchanged.

### 5. `Handle_should_defensively_skip_Manual_mode_stores_even_if_staged`
- **Objective**: Asserts that stores configured as `Manual` during batch run are skipped entirely.
- **Verification**: Client calls and recommendations count asserts to zero.

### 6. `Handle_should_not_process_duplicate_staged_candidates`
- **Objective**: Asserts that candidates with existing recommendations are ignored.
- **Verification**: Verifies database-level or query-level guard filters out assessments with existing recommendations.

### 7. `Handle_should_be_resilient_to_individual_store_failures`
- **Objective**: Asserts that an exception thrown by the client for Store A does not prevent Store B's batch from completing.
- **Verification**: Asserts one batch saved, the failed batch skipped.

### 8. `Handle_should_handle_AiServiceContractException_gracefully_per_store`
- **Objective**: Assert that contract validation errors are handled gracefully at the per-store level without crashing the whole command.
- **Verification**: Verifies that the command returns success and no recommendations are saved for the invalid store.

### 9. `Handle_should_continue_other_stores_when_one_store_returns_contract_violation`
- **Objective**: Assert that a contract validation error for one store's batch does not block another store's batch from completing.
- **Verification**: Store A returns contract exception, Store B succeeds normally. Asserts Store B's recommendation is persisted, Store A's is not, and command returns success.

### 10. `Database_should_enforce_uniqueness_on_RiskAssessmentId_constraint`
- **Objective**: Verify that unique database filtered index on `RiskAssessmentId` prevents concurrent duplicates.
- **Verification**: Attempting to insert duplicate recommendation throws a `DbUpdateException`.

---

## 3. Invariant Verification & Price Floor Analysis

### A. Non-Negotiable Architecture Invariant Verification
- **Price Mutation Path**: In accordance with the non-negotiable architecture invariant, the **ONLY** path where `Product.DiscountedPrice` is mutated is inside the handler when `Store.AiOperatingMode == Autonomous` and the proposed discount satisfies the price floor constraint:
  `proposedPrice >= priceFloor`
- **Audit Traceability**: Every mutation is explicitly traceable back to a specific `AiPricingRecommendation` row linked via `RiskAssessmentId` and `CorrelationId`.
- **All other code paths (e.g. Assisted mode, price floor rejection, error states, and all steps in Phase 2 & 3) do not perform any price mutations.**

### B. DTO Risk Context Mapping Parity
We verified that the batch request maps the following fields:
- **Inventory Metrics**: `Quantity`, `OriginalPrice`, `CurrentPrice`, `PriceFloor` (re-calculated using the isolated `PriceFloorCalculator`).
- **Demand Context**: `SalesVelocity` (calculated last 7-day velocity), `HistoricalSales.AverageDailySales` (calculated last 30-day baseline).
- **Expiry Context**: `ExpiresAt` (Midnight UTC), `HoursRemaining` (calculated).
- **Risk Assessment**: `RiskLevel`, `Reason`, `Confidence` (pulled directly from the staged `AiRiskAssessment`).

### C. Assisted Mode Scope Declaration
- **Deferred Approval Endpoints**: The approval endpoints/UI hooks for Assisted-mode `Pending` recommendations are **out of scope** for Phase 4 and deferred to a future phase.

---

## 4. Test Execution Console Log

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  FoodLoop.Domain -> C:\ITI\server\src\FoodLoop.Domain\bin\Debug\net10.0\FoodLoop.Domain.dll
  FoodLoop.Application -> C:\ITI\server\src\FoodLoop.Application\bin\Debug\net10.0\FoodLoop.Application.dll
  FoodLoop.Domain.Tests -> C:\ITI\server\test\FoodLoop.Domain.Tests\bin\Debug\net10.0\FoodLoop.Domain.Tests.dll
Test run for C:\ITI\server\test\FoodLoop.Domain.Tests\bin\Debug\net10.0\FoodLoop.Domain.Tests.dll (.NETCoreApp,Version=v10.0)
  FoodLoop.Application.Tests -> C:\ITI\server\test\FoodLoop.Application.Tests\bin\Debug\net10.0\FoodLoop.Application.Tests.dll
Test run for C:\ITI\server\test\FoodLoop.Application.Tests\bin\Debug\net10.0\FoodLoop.Application.Tests.dll (.NETCoreApp,Version=v10.0)
  FoodLoop.Infrastructure -> C:\ITI\server\src\FoodLoop.Infrastructure\bin\Debug\net10.0\FoodLoop.Infrastructure.dll
  FoodLoop.Infrastructure.Tests -> C:\ITI\server\test\FoodLoop.Infrastructure.Tests\bin\Debug\net10.0\FoodLoop.Infrastructure.Tests.dll
Test run for C:\ITI\server\test\FoodLoop.Infrastructure.Tests\bin\Debug\net10.0\FoodLoop.Infrastructure.Tests.dll (.NETCoreApp,Version=v10.0)

Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 70 ms - FoodLoop.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 147 ms - FoodLoop.Application.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    82, Skipped:     0, Total:    82, Duration: 2 s - FoodLoop.Infrastructure.Tests.dll (net10.0)
```
