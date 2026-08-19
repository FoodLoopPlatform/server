# Known Issues & Technical Debts

## 1. Fail-Closed ExpiryVerificationState Mitigation
While the AI service integration is paused, the `ExpiryVerificationState` field is client-supplied (via `CreateProductRequest.ExpiryVerificationState`) but is **ignored** for product status determination. All newly created products are forced to `ProductStatus.PendingModeration` to prevent client-side bypass of the moderation queue and ensure the admin `ProductUploaded` notification always fires. This mitigation should be revisited and updated to re-enable AI-confidence-driven `Active` status once the AI microservice is restored.
