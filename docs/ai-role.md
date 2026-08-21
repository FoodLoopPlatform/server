# FoodLoop AI Architecture & System Role

FoodLoop integrates a high-performance, deterministic AI system combining **Large Language Models (LLM)**, **Autonomous Multi-Agent Workflows (LangGraph)**, and **Multilingual Retrieval-Augmented Generation (RAG)**.

The AI service runs as a dedicated Python FastAPI microservice deployed on AWS EC2 (`http://3.94.7.125:8000`), seamlessly integrated with the ASP.NET Core backend through typed HTTP clients, Polly v8 resilience pipelines, and background hosted services.

---

## 🏛️ AI Architecture Overview

```
                               ┌────────────────────────────────────────────────────────┐
                               │                    FoodLoop AI Stack                   │
                               └────────────────────────────────────────────────────────┘
                                                         │
         ┌───────────────────────────────────────────────┼───────────────────────────────────────────────┐
         ▼                                               ▼                                               ▼
   ┌───────────┐                                   ┌───────────┐                                   ┌───────────┐
   │    LLM    │                                   │  AGENTS   │                                   │    RAG    │
   └───────────┘                                   └───────────┘                                   └───────────┘
   • Gemma 2 27B IT (SambaNova)                     • Monitoring Agent (Risk & Routing)             • BGE-M3 (1024-d Multilingual)
   • Gemini 1.5 Flash (Vision OCR)                 • Pricing Agent (Markdown Optimization)         • Qdrant Vector Database
   • Temperature = 0 (Deterministic)               • LangGraph Directed Workflows                  • Hard Store & Product Isolation
```

---

## 1. 🧠 LLM (Large Language Models)

* **Reasoning Model**: **`Gemma 2 27B IT`** (served via SambaNova OpenAI-compatible endpoint).
  * **Temperature**: `0.0` for structured, deterministic, auditable decisions without hallucination.
  * **Role**: Analyzes inventory velocity, demand ratios, and store policy context to recommend optimal discount percentages and generate human-readable business justifications.
* **Multimodal / Vision Model**: **`Gemini 1.5 Flash`** (in .NET Backend).
  * **Role**: Parses uploaded product label photos statelessly or on product creation to extract expiration dates, product titles, and English/Arabic category suggestions (`StatelessOcrScanCommandHandler`).

---

## 2. 🤖 Autonomous Dual-Agent System (LangGraph)

The AI Microservice implements two coordinated LangGraph agents:

### A. Monitoring Agent (Inventory Risk & Routing)
* **Endpoint**: `POST /api/v1/monitoring/analyze`
* **Inputs**: Product metadata, stock quantities, daily sales velocity, remaining hours to expiry, store location coordinates, and store operating policy (`assisted` / `autonomous`).
* **Tool Augmentations**:
  * **Open-Meteo**: Dynamic weather forecast queries (e.g. extreme heat accelerating perishable degradation).
  * **Nager.Date**: Egyptian national holiday awareness influencing foot traffic demand.
* **Decision Output**:
  * Calculates `RiskLevel` (`LOW`, `MEDIUM`, `HIGH`, `CRITICAL`).
  * Determines `Route`: `LOW` $\rightarrow$ `NO_ACTION`, while `MEDIUM/HIGH/CRITICAL` $\rightarrow$ `PRICING`.

### B. Pricing Agent (Dynamic Markdown Optimizer)
* **Endpoint**: `POST /api/v1/pricing/recommend`
* **Inputs**: Store-scoped candidate batches, inventory costs, historical sales velocity, and RAG contextual knowledge retrieved from Qdrant.
* **Decision Output**:
  * Proposes bounded markdown percentages between **0.0% and 15.0%**.
  * Determines `ActionRequirement`:
    * If Store is in `assisted` mode $\rightarrow$ `APPROVAL_REQUIRED` (queued in Merchant Dashboard for approval).
    * If Store is in `autonomous` mode $\rightarrow$ `AUTOMATIC_EXECUTION_ELIGIBLE` (auto-applied by background workers).
  * Produces transparent, auditable business explanation strings.

---

## 3. 📚 RAG (Retrieval-Augmented Generation & Vector Store)

* **Embedding Model**: **`BAAI/bge-m3`**
  * Dense 1024-dimensional multilingual vector representation supporting Arabic, English, and cross-lingual semantics.
* **Vector Store**: **`Qdrant`** (with in-memory fallback for local unit test suites).
* **Ingestion Endpoint**: `POST /api/v1/pricing/knowledge/ingest`
* **Multi-Tenant Isolation**: Hard metadata filtering by `store_id` and `product_id` guarantees that pricing models only retrieve historical pricing episodes (`ProductPricingEpisode`) belonging strictly to the requesting store.
* **Data Stored**: Past markdown campaigns, sell-through rates, initial/final prices, and outcomes (`SOLD_OUT`, `PARTIALLY_SOLD`, `EXPIRED`).

---

## 4. 🛡️ Financial Safety Shield (.NET Authority)

The backend never trusts raw LLM outputs for financial execution:

1. **Price Floor Calculator ([`PriceFloorCalculator.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Features/AiIntegration/PriceFloorCalculator.cs))**:
   * Evaluates the merchant's configured floor policy (`MarginBased`, `CostPlus`, `AbsoluteMinimum`).
   * Guarantees `DiscountedPrice >= PriceFloor` before writing any price reduction to the database.
2. **Polly v8 Resilience Pipelines**:
   * Automated retry policies with exponential backoff for transient network issues.
   * Circuit breaker protecting the server if the AI service experiences persistent downtime.
   * Request timeouts (default 60 seconds for batch pricing).
3. **Audit Logging**:
   * Every price change, AI recommendation, and merchant approval/rejection is stored in `PriceHistories` and `AiPricingRecommendations`.

---

## 5. ⏰ Scheduled Hosted Services

Three background services run continuously in the ASP.NET Core backend:

| Background Service | Options Section | Default Interval | Description |
| :--- | :--- | :---: | :--- |
| **`MonitoringScannerHostedService`** | `MonitoringScanner` | **60 min** | Scans store inventory for expiring items and submits risk assessments. |
| **`PricingBatchHostedService`** | `AiPricingBatch` | **60 min** | Executes pricing recommendations and auto-applies autonomous discounts. |
| **`HistoricalIngestionHostedService`** | `HistoricalIngestion` | **60 min** | Ingests closed sales episodes into Qdrant vector database. |

All intervals are hot-reloadable at runtime using `IOptionsMonitor<T>` via `appsettings.json` or `.env`.
