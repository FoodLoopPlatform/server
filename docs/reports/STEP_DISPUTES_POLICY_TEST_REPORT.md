# Step Disputes Policy Test Report

This report summarizes the implementation and verification of the Dispute Image Attachment & Store Expiry Deactivation Policy.

---

## 1. Summary of Changes by File

### Domain Layer (`FoodLoop.Domain`)
*   **[`ProductReport.cs`](file:///c:/ITI/server/src/FoodLoop.Domain/Entities/ProductReport.cs)**: Added optional `ImageUrl` property to record proof/images attached to product reports.
*   **[`SystemSettings.cs`](file:///c:/ITI/server/src/FoodLoop.Domain/Entities/SystemSettings.cs)**: Added `MaxExpiredReportsBeforeDeactivation` configuration property (defaulting to 3).

### Application Layer (`FoodLoop.Application`)
*   **[`DisputeDto.cs`](file:///c:/ITI/server/src/FoodLoop.Application/DTOs/Admin/DisputeDto.cs)**: Added nullable `ImageUrl` property.
*   **[`ReportProductCommand.cs`](file:///c:/ITI/server/src/FoodLoop.Application/Features/Products/Commands/ReportProductCommand.cs)**: Added optional `ImageUrl` parameter to the constructor record.

### API Layer (`FoodLoop.API`)
*   **[`MarketplaceController.cs`](file:///c:/ITI/server/src/FoodLoop.API/Controllers/MarketplaceController.cs)**: Added `ImageUrl` field to `ReportProductRequest` and bound it to `ReportProductCommand`.

### Infrastructure & Persistence Layer (`FoodLoop.Infrastructure`)
*   **[`ProductReportConfiguration.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Persistence/Configurations/ProductReportConfiguration.cs)**: Created to configure the max length limit of 500 characters for `ImageUrl` on `ProductReport`.
*   **[`SystemSettingsConfiguration.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs)**: Configured the default value of 3 for `MaxExpiredReportsBeforeDeactivation` and updated the seeded singleton entity.
*   **[`ReportProductCommandHandler.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Features/Products/Commands/ReportProductCommandHandler.cs)**:
    *   Added validation constraints for `ImageUrl` (must be 500 characters or fewer).
    *   Added tracking and checking for expired product report counts against stores.
    *   Implemented the automated deactivation policy when counts cross the configured threshold.
*   **Query Handlers**: Updated `GetDisputesQueryHandler`, `GetDisputeByIdQueryHandler`, `GetStoreDisputesQueryHandler`, and `GetMyReportsQueryHandler` to return `ImageUrl` in `DisputeDto`.
*   **EF Core Migrations**: Generated the new migration script to update the database schema.

---

## 2. Test Execution & Layer Breakdown

A comprehensive suite of tests was written in `DisputeAndPolicyTests.cs` to test the new logic. The test suite execution results are as follows:

| Test Assembly | Status | Passed | Failed |
| --- | --- | --- | --- |
| `FoodLoop.Domain.Tests.dll` | **Passed** | 28 | 0 |
| `FoodLoop.Application.Tests.dll` | **Passed** | 11 | 0 |
| `FoodLoop.Infrastructure.Tests.dll` | **Passed** | 204 | 0 |
| **Total** | **Passed** | **243** | **0** |

### Verified Test Scenarios

1.  **Dispute Image Round-Trip**: Report filed with `ImageUrl` persists correctly and returns `ImageUrl` in dispute queries.
2.  **Report Without Image**: Report filed with `null` image succeeds with no null-reference issues.
3.  **Threshold Trigger**: Filing the Nth expired report (where $N = \text{threshold}$) successfully deactivates the store (`VerificationStatus.Rejected`), suspends the merchant (`UserStatus.Suspended`), and appends the notice to `AdminNote`.
4.  **Non-Expired Report**: Reports for reasons other than expired products increment report counts without triggering the auto-deactivation policy.
5.  **Existing Notes Preservation**: Auto-deactivation appends to existing `AdminNote` content rather than overwriting it.

All 243 tests passed successfully. 100% of the baseline tests are preserved with zero regressions.
