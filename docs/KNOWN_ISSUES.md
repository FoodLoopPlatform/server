# Known Issues & Technical Debts

## 1. AI Service Integration Status (Resolved & Fully Integrated)
* **Status**: **RESOLVED & OPERATIONAL**
* **Deployment**: Live Python FastAPI microservice deployed on AWS EC2 (`http://3.94.7.125:8000`).
* **Integration**: Fully integrated with ASP.NET Core backend via `AiServiceClient`, Polly v8 resilience pipelines, and three automated background hosted services (`MonitoringScannerHostedService`, `PricingBatchHostedService`, `HistoricalIngestionHostedService`).
* **Verification**: All 490 automated tests across Domain, Application, and Infrastructure pass 100% green.

## 2. Fail-Closed Bulk CSV Upload Mitigation (Resolved)
* **Status**: **RESOLVED**
* The bulk-upload handler (`BulkUploadProductsCommandHandler`) creates imported products in `ProductStatus.PendingModeration` and dispatches `ProductUploaded` admin notifications per item to ensure full platform governance.

## 3. Production CORS Allowed Origins (Resolved)
* **Status**: **RESOLVED**
* Added deployed production domain `https://foodloop.runasp.net` to `Cors:AllowedOrigins` in `appsettings.json` and `appsettings.Development.json` to ensure clean Swagger UI and SPA API execution.
