# Step 7 Hardening: AI-Integration Coverage Gaps Test Report

## Executive Summary
This report presents the verification results of Step 7 of the FoodLoop AI-Integration roadmap. We successfully closed critical coverage gaps, enforced strict schema contract validations, introduced robust freshness snapshot checks across both autonomous and assisted approval modes, and verified batch chunking controls.

- **Baseline Test Count (Step 6)**: 147 tests
- **Total Test Count (Step 7)**: 177 tests
- **New Tests Added**: +30 tests
- **Overall Status**: **PASSED (100% Pass Rate)**

---

## Pass Rate Calculation
$$\text{Pass Rate} = \frac{\text{Passed Tests}}{\text{Total Tests}} \times 100 = \frac{177}{177} \times 100 = 100\%$$

- **Total Runs**: 177
- **Passed**: 177
- **Failed**: 0
- **Skipped**: 0

---

## Test Suite Breakdown

| Suite / Area | Type | Total Tests | Passed | Failed |
| :--- | :--- | :---: | :---: | :---: |
| **Domain Entities & Configuration** | Unit / Schema | 28 | 28 | 0 |
| **Application Logic** | Unit | 11 | 11 | 0 |
| **AI Client Contract Verification** | Integration / Mock | 45 | 45 | 0 |
| **Monitoring & Batch Pricing** | Logic / Integration | 48 | 48 | 0 |
| **Assisted Approval & Edge Cases** | Logic / Integration | 40 | 40 | 0 |
| **End-to-End Correlation Flow** | E2E Integration | 5 | 5 | 0 |

---

## Individual Test Case Status

### AI Service Client Contract (`AiServiceClientTests.cs`)
- [x] `RecommendPricingAsync_counts_match_but_returned_IDs_are_duplicates_should_throw_AiServiceContractException`
- [x] `RecommendPricingAsync_fewer_decisions_than_products_should_throw_AiServiceContractException`
- [x] `RecommendPricingAsync_more_decisions_than_products_should_throw_AiServiceContractException`
- [x] `RecommendPricingAsync_out_of_bounds_confidence_values_should_throw_AiServiceContractException`
- [x] `RecommendPricingAsync_boundary_values_0_and_15_discount_and_0_and_1_confidence_should_succeed` (Positive control verification)

### Batch Chunking (`AiPricingBatchTests.cs`)
- [x] `Handle_should_chunk_batches_larger_than_MaxPricingBatchSize_into_multiple_requests` (Verifies 75 candidate items chunked into batches of 50 and 25)

### Rejection & Freshness Safeguards (`AiIntegrationEdgeCaseTests.cs`)
- [x] `Scanner_Handle_should_persist_zero_risk_assessments_on_failure` (Verifies no-fabrication rule on scan endpoint failure)
- [x] `Batch_Handle_should_persist_zero_recommendations_for_failed_store` (Verifies no-fabrication rule on recommend endpoint failure)
- [x] `Approve_recommendation_should_be_rejected_if_product_quantity_changed_since_staging`
- [x] `Approve_recommendation_should_be_rejected_if_product_status_changed_since_staging`
- [x] `Autonomous_execution_should_be_rejected_if_product_state_changed_since_staging`
- [x] `Approve_recommendation_should_succeed_if_product_state_unchanged` (Positive control)
- [x] `Approve_recommendation_should_be_rejected_if_product_is_no_longer_Active`
- [x] `Autonomous_execution_should_be_rejected_if_product_is_no_longer_Active`

### E2E Flow & Correlation Continuity (`E2EIntegrationTests.cs`)
- [x] `Full_flow_should_preserve_single_CorrelationId_from_monitoring_through_PriceHistory` (Verifies single trace token flows intact to database log)

---

## Investigation Findings

1. **Freshness Snapshot Timing Decision**:
   We decided to capture the initial product state snapshot at monitoring/staging time (on `AiRiskAssessment`) when candidates are first flagged. This snapshot is then carried over to `AiPricingRecommendation` during the pricing batch execution. Checking freshness against staging-time values ensures that any change in product price, quantity, or status over the gap between staging and execution is caught, preventing the execution or approval of stale pricing recommendations.
2. **Authorization Headers and Security Redaction**:
   Investigation confirmed that no authorization headers or API keys are transmitted to the local Python AI service as the communication occurs inside a secure, private network boundary. Thus, log scrubbing/redaction for API keys is currently not applicable.
3. **Pre-execution Ready Probe `/ready`**:
   The circuit breaker and retry resilience pipeline on the HTTP client is configured per-call. Attempting a pre-execution `/ready` probe was decided against, as circuit breaking natively handles service unavailability at invocation time without introducing pre-call overhead.
4. **Corrected Re-ingestion Validation**:
   Plain confirmation: **No**, `RunHistoricalIngestionCommandHandler` does not currently support re-submitting a corrected `event_id` to update the existing Qdrant/vector store document. The sweeper skips any previously ingested `event_id` due to the `if (isAlreadyIngested) continue;` check.
5. **SQLite Query Translation**:
   The sales velocity calculation LINQ join failed to translate to SQLite in testing. We successfully resolved this by fetching orders and items separately, then executing in-memory joins using LINQ-to-Objects, which is fully compatible with both production SQL Server and test SQLite DBs.

---

## Open Items Requiring Product Decision
- **Ingestion Sweeper Re-submission Support**: A product decision is required on whether the historical ingestion sweeper should support updating and re-submitting corrected pricing episodes (matching `AI_Report.md` §7: "corrected snapshots replace existing knowledge instead of creating duplicates") rather than skipping previously ingested events.

---

## Regression Check
We explicitly confirm that all **147 legacy test cases** from Step 6 Hardening continue to run and pass with **0 regressions**. Missing snapshots (e.g. on pre-existing rows created before the migration) are treated as unverifiable and fail closed (rejected), preventing stale executions. Legacy unit tests have been updated to populate valid snapshots for positive controls.

---

## Invariant & Guardrail Verification

- **No fabricated rows on failure**: Verified that any failure on the AI client results in zero rows persisted for that store or candidate.
- **Freshness enforcement**: Verified that stale state modifications (price, quantity, status) reject executions in both autonomous and assisted modes.
- **MaxPricingBatchSize limit**: Checked that candidate lists exceeding `MaxPricingBatchSize` are split and sent in multiple calls.
- **Contract Boundary validation**: Checked that out-of-bounds parameters and duplicate product IDs are caught and detailed in exceptions.
