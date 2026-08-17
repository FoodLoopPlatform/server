# Production Cutover & Deployment Hardening Checklist

This document details the checklist for deploying Phase 7 of the FoodLoop AI-Integration roadmap to production (mapping to **AI_Report.md** §10, §14, and §15).

---

## 1. Credentials Rotation & Secret Injection (§10, §14)
- [ ] **Rotate SQL Server Password**: The database password currently exposed in history (`Kq2@6?eBC7!o`) must be rotated on the database server immediately.
- [ ] **Scrub Git History**: Use `git filter-repo` or `BFG Repo-Cleaner` to remove any committed instances of `Password=Kq2@6?eBC7!o` from the repository history.
- [ ] **Rotate SambaNova API Key**: Provision and inject the real SambaNova API key via the production secrets manager. Do **not** commit it or reuse the development key.
- [ ] **Store Secrets in Environment Variables**: Confirm that production connection strings and API keys are injected via environment variables (e.g. `ConnectionStrings__DefaultConnection` and `OPENAI_API_KEY`) and never committed to `appsettings.json` files.

---

## 2. Vector DB Provisioning & Settings (§10, §14)
- [ ] **Provision Qdrant Production Endpoint**: Setup a dedicated production Qdrant cluster.
- [ ] **Configure Qdrant Collection**: Ensure the Qdrant collection is configured with **1024-dimensional** vectors, matching the BGE-M3 model size.
- [ ] **Enforce No Fallback**: Ensure the AI Service env variables set `VECTOR_STORE_PROVIDER=qdrant`. Production must **never** run with `VECTOR_STORE_PROVIDER=memory` to prevent silent operating empty RAG context.

---

## 3. AI Service Deployment & CLI Validation (§10.1, §15)
- [ ] **Deploy AI Service with Production Environment**: Set `APP_ENV=production` on the deployed AI service instance.
- [ ] **Configure PORT and Workers**: Set `PORT` and a conservative `WORKERS` worker count (default to 1 worker per container for model memory footprint safety).
- [ ] **Run AI Production Dependency Verification**: Inside the AI Service container/deployment environment, execute the dependency check CLI:
  ```bash
  python -m app.cli.smoke_check
  ```
  Ensure it exits with code `0`. This validates connection to Qdrant, OpenAI-compatible SambaNova endpoints, Open-Meteo, and Nager.Date without modifying business data.

---

## 4. .NET Integration Config & Cutover Checks (§9, §10, §15)
- [ ] **Configure AI BaseUrl**: Configure the production endpoint URL for .NET's `AiService:BaseUrl` via environment variables (e.g. `AiService__BaseUrl` in Azure/K8s). Do not commit it to `appsettings.json`.
- [ ] **Verify Cutover Startup Protection**: Confirm that the API fails fast at startup if it is run under `ASPNETCORE_ENVIRONMENT=Production` while `AiService:BaseUrl` points to localhost.
- [ ] **Verify AI Service Metadata**: Query the GET `/version` endpoint of the AI Service via the client (or curl) to confirm the service metadata returns the expected name, version (e.g. `1.0.0`), and environment (e.g. `production`), ensuring the .NET client is communicating with the correct deployment build.
- [ ] **Apply Database Migrations**: Run `dotnet ef database update` (or execute the startup migration runner) to apply the model updates that configure `ProductPricingEpisodes` columns `IngestedAt` and `IngestionCorrelationId` as nullable, enabling idempotent correction sweeps.

---

## 5. Live E2E Smoke Verification & Rollout Strategy (§9, §11.1, §15)
- [ ] **Run .NET Opt-In Live Smoke Test**: From the deployment pipeline/runner (with network access to the deployed AI Service), execute the live integration test:
  ```bash
  dotnet test --filter "FullyQualifiedName=FoodLoop.Infrastructure.Tests.Integrations.LiveAiServiceSmokeTests.Run_Live_AI_Service_Smoke_Check"
  ```
  Ensure `RUN_LIVE_AI_SERVICE_TESTS=true` and `AI_SERVICE_LIVE_BASE_URL=<deployed-ai-url>` environment variables are set. This verifies the client handles actual Gemma 2 model outputs and structured response boundaries safely.
- [ ] **Confirm Health Check Routing**: Call the .NET `/health` endpoint and verify the AI service reachability is checked. Verify that taking the AI service offline flags the system as `Degraded` but returns `200 OK` (so that load balancers do not strip the application).
- [ ] **Rollout Phasing**:
  - Initially, configure all organizations with `AiOperatingMode=Assisted` so that pricing changes require human-in-the-loop merchant approval.
  - Monitor logs for a standard period (e.g. 7-14 days).
  - Only after confirming steady-state and zero pricing anomalies, enable `AiOperatingMode=Autonomous` for designated automated stores.
