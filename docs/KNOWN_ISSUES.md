# Known Issues & Technical Debts

## 1. Fail-Closed ExpiryVerificationState Mitigation (Single-Upload)
While the AI service integration is paused, the `ExpiryVerificationState` field is client-supplied (via `CreateProductRequest.ExpiryVerificationState`) but is **ignored** for product status determination. All newly created products are forced to `ProductStatus.PendingModeration` to prevent client-side bypass of the moderation queue and ensure the admin `ProductUploaded` notification always fires. This mitigation should be revisited and updated to re-enable AI-confidence-driven `Active` status once the AI microservice is restored.

## 2. Fail-Closed Bulk CSV Upload Mitigation (Resolved)
Previously, the bulk-upload command handler (`BulkUploadProductsCommandHandler`) created products with `ProductStatus.Active` unconditionally, representing a bypass of the moderation controls. Following the precedent of single-upload fail-closed enforcement (commit `7551215`), the bulk-upload handler now forces all imported products to `ProductStatus.PendingModeration` and dispatches `ProductUploaded` admin notifications per item. Any client-supplied `expiryverificationstate` header in the CSV is optionally parsed and retained on the entity for later AI reconciliation.
