# Step 3 Test Report: Monitoring Trigger Pipeline

This report documents the results of executing the unit, integration, and HTTP resilience test suite following the implementation of the Monitoring Trigger Pipeline in Step 3.

## Test Summary

| Layer | Existing (pre-Step 3) | New (Step 3) | Total | Passing |
|---|---|---|---|---|
| Domain | 28 | 0 | 28 | 28/28 |
| Application | 11 | 0 | 11 | 11/11 |
| Infrastructure | 58 | 14 | 72 | 72/72 |
| **Total** | **97** | **14** | **111** | **100%** |

- **Total Test Projects Executed:** 3
- **Total Tests Passed:** 111
- **Total Tests Failed:** 0
- **Total Tests Skipped:** 0
- **Pass Rate:** 100% (No Regressions)

---

## Test Executed Breakdown

### 1. Domain Entities & Validation Tests (`FoodLoop.Domain.Tests`)
- **Passed Tests:** 28 / 28 (Baseline)

### 2. Application Serialization & Mapping Tests (`FoodLoop.Application.Tests`)
- **Passed Tests:** 11 / 11 (Baseline)

### 3. Infrastructure HTTP Client, Background Scanner & Resilience Tests (`FoodLoop.Infrastructure.Tests`)
- **Passed Tests:** 72 / 72
- **New Coverage (14 tests):**

#### A. Background Scanner & MediatR Handler (`AiMonitoringScannerTests`)
- **`Handle_should_select_and_process_products_nearing_expiry`**: Verifies that active products expiring within the threshold window trigger a client call and save an `AiRiskAssessment`.
- **`Handle_should_select_and_process_products_with_low_velocity`**: Verifies that active products with velocity below the multiplier of their 30-day historical daily average trigger a client call.
- **`Handle_should_completely_skip_products_if_mode_is_manual`** (Manual-mode skip guard): Asserts that stores in `Manual` operating mode are bypassed early, executing zero client calls and writing no assessment rows.
- **`Handle_should_correctly_route_PRICING_vs_NO_ACTION`** (PRICING vs NO_ACTION routing): Asserts that route `PRICING` marks the assessment with `IsPricingStaged = true`, and route `NO_ACTION` leaves it as `false`.
- **`Handle_should_be_resilient_to_single_candidate_failures`**: Simulates client exception on candidate 1, and verifies candidate 2 completes successfully (scan does not crash).

#### B. Scanner Integration (`AiMonitoringScannerIntegrationTests`)
- **`Scan_should_persist_assessments_and_not_mutate_product_prices`**: Executes scanner command via MediatR and verifies database schema persistence and price field preservation.
- **`Scan_should_continue_when_client_throws_exception_on_one_candidate`** (Mid-scan failure resilience): Asserts overall scan success even when individual calls throw.

#### C. Isolated Price Floor Policy Mapping (`PriceFloorCalculatorTests`)
- **`Calculate_should_return_30_percent_when_policy_is_Fixed30Percent`**: Confirms that the `Fixed30Percent` policy maps to exactly `0.30 * OriginalPrice`.
- **`Calculate_should_return_50_percent_when_policy_is_Fixed50Percent`**: Confirms that the `Fixed50Percent` policy maps to exactly `0.50 * OriginalPrice`.
- **`Calculate_should_return_70_percent_when_policy_is_DynamicAi`**: Confirms that the `DynamicAi` policy maps to exactly `0.70 * OriginalPrice`.
- **`Calculate_should_fallback_to_70_percent_when_policy_is_null`**: Confirms fallback to 70% is applied when policy is null.
- **`Calculate_should_fallback_to_70_percent_when_policy_is_unrecognized`**: Assures fallback to 70% is applied when an unrecognized policy value is provided, confirming fallback is intentional and safe.

#### D. Gap-Closing Resilience (`AiServiceClientTests`)
- **`CircuitBreaker_should_trip_fail_fast_and_recover_after_cooldown_using_virtual_time`** (Circuit breaker recovery cycle): Trips the circuit breaker on 5 consecutive failures, verifies fast-fails with open circuit exceptions, advances fake time provider by 31s, and verifies recovery on a subsequent successful request.
- **`CorrelationIdAccessor_GetCorrelationId_should_return_fallback_uuid_when_httpContext_is_null`** (CorrelationIdAccessor fallback): Asserts that background workers invoking correlation accessor generate fresh, non-repeating fallback UUIDs.

---

## Invariant Verification & Price Floor Analysis

**SystemSettings Provenance Note**: 
> [!NOTE]
> The `SystemSettings` table and the `DefaultPriceFloorPolicy` configuration property are **pre-existing** database configurations established prior to Step 3 (introduced in migration `20260815205304_AddSystemSettings`). No undocumented database schema modifications or migrations were made for system settings in this step.

**Findings & Contract Inspection**:
- Inspected the Pydantic schema for `MonitoringRequest` in `AI_Report.md` (§4.1) and verified that the `price_floor` field is a required contract input within `InventoryMetrics`.
- **Mapping Resolution**: Rather than sending a static placeholder `0m`, `RunMonitoringScanCommandHandler` retrieves the active platform-wide `SystemSettings` from the database and maps the candidate product's `PriceFloor` dynamically using the configured `DefaultPriceFloorPolicy`:
  - `Fixed30Percent` -> `0.30 * OriginalPrice`
  - `Fixed50Percent` -> `0.50 * OriginalPrice`
  - `DynamicAi` (or fallback) -> `0.70 * OriginalPrice`
- **Explicit Invariant Justification**: The Monitoring Agent requires these price inputs (`original_price`, `current_price`, `price_floor`) strictly as passive evidence context to calculate inventory margin pressure and assess risk metrics (e.g. evaluating margin buffer safety). Under no circumstances does the Monitoring pipeline calculate, mutate, or adjust price values on product entities. The C# backend remains the sole authoritative source for financial execution and final price validation.

**Invariant Test Verification**:
- This invariant was explicitly verified in `Scan_should_persist_assessments_and_not_mutate_product_prices` by asserting that the pre-scan and post-scan values of `OriginalPrice` and `DiscountedPrice` on the product entity remained strictly unchanged (at 100.00m).
- In addition, all price calculations are completely absent from the `RunMonitoringScanCommandHandler` implementation code.

---

## Command Output Execution Log

```text
Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 52 ms - FoodLoop.Domain.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 121 ms - FoodLoop.Application.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    72, Skipped:     0, Total:    72, Duration: 2 s - FoodLoop.Infrastructure.Tests.dll (net10.0)
```
