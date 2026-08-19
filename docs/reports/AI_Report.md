# **FoodLoop AI Service** 

## Full Technical Architecture, Implementation & Production Readiness Report 

_Final consolidated handoff | Version 1.0.0 | 16 August 2026_ 

|**Document status**|**Scope**|**Verifcation basis**|
|---|---|---|
|Final consolidated report|Python AI microservice: Monitoring +<br>Pricing + RAG + providers + deployment<br>readiness|Compiled from all implementation<br>summaries and verifed decisions shared<br>in this conversation; latest reported test<br>results are recorded explicitly.|



**Important verification note:** This report consolidates the implementation evidence and test summaries provided during the project conversation. It is not a fresh filesystem/code audit of the repository; therefore, statements about implementation status are based on the reported results rather than an independent re-run of the repository. 

## **1. Executive Summary** 

FoodLoop AI Service evolved from a minimal FastAPI service into a provider-agnostic dual-agent AI microservice with a Monitoring Agent for inventory-risk routing and a Pricing Agent for discount recommendations. The service is designed as a recommendation layer, while the .NET Backend remains the final business and financial authority. 

- Monitoring Agent: evaluates context sufficiency, can retrieve controlled external context, computes deterministic risk signals, performs LLM risk assessment, and routes LOW to NO_ACTION or MEDIUM/HIGH/CRITICAL to PRICING. 

- Pricing Agent: processes batches per store, retrieves store/product-isolated historical knowledge, computes deterministic pricing signals, and returns a discount recommendation from 0% to 15% with an auditable reason and confidence. 

- RAG stack: HistoricalPricingEvent → factual knowledge document → local BAAI/bge-m3 multilingual embeddings (1024-d) → Qdrant → isolated retrieval. 

- External context for the MVP: Open-Meteo for weather and Nager.Date v4 for public/national holidays in the store country, with mock providers retained for offline tests. 

- Operating modes: ASSISTED and AUTONOMOUS. Manual pricing is outside the AI service. Assisted requires owner approval; autonomous produces eligibility for backend automatic execution subject to .NET safety checks. 

- Production hardening: environment-driven configuration, no silent provider fallback, health/readiness/version endpoints, request IDs, timeout configuration, Docker hardening, secret handling, and smoke-check tooling. 

## **2. Final Architecture** 

.NET Backend / Store Configuration 

│ 

trusted internal REST calls 

▼ 

┌─────────────────────────────┐ FoodLoop AI Service    │ │ │ │ FastAPI                    │ │ │ │ │ 

FoodLoop AI Service — Full Technical Report 

│ ▼ │ Monitoring Agent           │ │ │ │ │ Risk + Route               │ │ │ │ │ │ ▼ │ Pricing Agent              │ │ │ │ │ │ ├── Pricing Signals     │ │ ├── Historical RAG      │ │ └── LLM Recommendation │ │ │ Infrastructure:            │ │ BGE-M3 / Qdrant           │ │ Open-Meteo / Nager.Date   │ │ └──────────────┬──────────────┘ 

│ 

▼ recommendation + policy interpretation only │ 

▼ .NET execution layer 

### **2.1 Responsibility Boundaries** 

|**Component**|**Owns**|**Does not own**|
|---|---|---|
|Monitoring Agent|Context analysis, risk assessment,<br>routing|Discount calculation, execution|
|Pricing Agent|Historical retrieval, pricing signals,<br>discount recommendation, reason,<br>confdence|Final price, execution, approval|
|Policy layer in Python|Maps assisted/autonomous mode to<br>action requirement<br>|Actual approval or transaction execution|
|.NET Backend|Store confguration, authorization, price<br>calculation, price foor, approvals, DB<br>updates,execution|LLM reasoning|
|Vector Store|Vector persistence/search, hard<br>metadata flters|Pricing decisions|
|EmbeddingProvider|Text → vectorgeneration|Business recommendations|



## **3. Implementation Evolution / Milestones** 

|**Milestone**|**Key result**|**Reported test count**|
|---|---|---|
|Foundation|FastAPI, settings, /health, tests,<br>pyproject,README|Initial baseline|
|Monitoring contracts|Pydantic request/response schemas +<br>/api/v1/monitoring/analyze|9|
|LLM layer|Shared ChatOpenAI-compatible factory,<br>temperature=0|13|
|LangGraph monitoringskeleton|State,nodes, graph,API wiring|15|
|Context analysis|Structured LLM analysis of missing<br>weather/holidaycontext|22|
|External context tools|Mock weather/events + conditional<br>routing|34|



FoodLoop AI Service — Full Technical Report 

|Risk assessment|Deterministic risk signals + LLM<br>interpretation|45|
|---|---|---|
|Routing|LOW→NO_ACTION,others→PRICING|52|
|Monitoringhardening|Validation,state errors,E2E scenarios|74|
|Pricing Agent|Single/batch recommendation-only<br>agent + retriever abstraction|94|
|Batch pricing|Store-scoped batch requests and strict<br>product mapping|97|
|Knowledge retrieval layer|Typed retrieval items,isolation, grouping|104|
|Historical knowledge model|HistoricalPricingEvent + deterministic<br>document builder|125|
|Embeddingabstraction|OpenAI/fakeprovider interfaces<br>|139|
|Vector store abstraction|In-memorycosine similarity+ fltering|162|
|Qdrant infrastructure|Production adapter, collection<br>management,hard flters|181|
|Retrieval pipeline|Batch query embedding + product-<br>isolated vector search|194|
|Storepolicy|Assisted/autonomouspolicymapping|201/202|
|Pricing signals|Inventory coverage, demand ratio, expiry<br>pressure|210|
|Pricingscenario tests|19 recommendation/contract scenarios|229|
|Open-Meteo|Real weatherprovider + live opt-in test|244|
|Nager.Date|Holiday context replacing generic local<br>events|258|
|Historical ingestion|Idempotent batch ingestion →<br>embeddings →Qdrant|280|
|BGE-M3 hardening|Arabic/English local embeddings, 1024-<br>d,cross-lingual retrieval<br>|293 reported (289 pass, 4 skip)|
|Production completion|Qdrant/confg validation, Docker,<br>readiness,smoke tooling|291 pass, 7 skip, 0 fail|



## **4. Monitoring Agent — Complete Detail** 

The Monitoring Agent is the router and risk evaluator. It intentionally does not make pricing decisions. 

### **4.1 Input Contract** 

#### MonitoringRequest 

├── product: ProductMetadata 

   - ├── inventory: InventoryMetrics 

   - ├── demand: DemandContext 

   - ├── expiry: ExpiryContext 

   - ├── location: LocationContext 

   - ├── store_policy: StorePolicy | optional during compatibility stages 

   - └── timestamp 

- Product metadata: ID, name, category/type information used for context. 

- Inventory: quantity, original price, current price, price floor. 

- Demand: sales velocity and historical baseline metrics. 

- Expiry: expiration timestamp and remaining hours. 

- Location: latitude, longitude, store_id. 

- Store policy: backend-owned operating mode (assisted/autonomous) when included. 

- Request validation rejects missing required business data at the API boundary (HTTP 422). 

FoodLoop AI Service — Full Technical Report 

### **4.2 Context Analysis** 

Context Analysis is a dedicated structured-output LLM step whose job is to determine whether the supplied request is sufficient and, if not, whether weather or public-holiday context should be fetched. 

AllowedContext = weather | local_events ContextAnalysisResult: is_sufficient: bool missing_context: list[AllowedContext] reason: str confidence: float [0,1] 

- The LLM can request only weather or local_holiday/public-holiday context; unsupported values such as traffic, quantity, price, or expiry are rejected. 

- The step never suggests discounts, prices, donation actions, or execution decisions. 

- Structured Pydantic output is used; there is no manual JSON/regex parsing. 

- LLM errors propagate instead of being converted to NO_ACTION or fake data. 

### **4.3 Weather Context** 

The MVP weather provider is Open-Meteo. The Monitoring Agent calls it only when context analysis requests weather. 

- Provider abstraction: WeatherProvider with MockWeatherProvider and OpenMeteoWeatherProvider. 

- Open-Meteo endpoint: /v1/forecast; no API key is required for the demo/free development endpoint. 

- Inputs: latitude, longitude, UTC start/end hours. 

- Mapped fields: temperature_2m → temperature; precipitation_probability / 100 → normalized probability; weather_code → human-readable WMO condition. 

- Naive datetimes and invalid coordinates fail explicitly via WeatherToolError. 

- Live integration test is opt-in; normal pytest remains offline. 

- Commercial production use of the hosted free endpoint is not assumed; the architecture is ready for commercial endpoint or self-hosting later. 

### **4.4 Holiday Context** 

The earlier generic local-events concept was intentionally simplified for the MVP into public/national holiday context. 

- Provider: Nager.Date v4. 

- Current MVP country scope: Egypt (EG); country should ultimately come from trusted store/backend configuration. 

- Contract: Holiday(date, name, country_code, national_holiday, holiday_types) and HolidayContext(holidays). 

- The provider is year-based; the tool fetches only the calendar years covered by the request window and filters locally to the exact date range. 

- No fabricated distance, venue, or expected attendance fields remain. 

- Provider failure raises LocalEventsToolError; an empty holiday list is a valid no-match result. 

- Live integration is opt-in; the normal suite is offline. 

### **4.5 Deterministic Risk Signals** 

|**Signal**|**Formula / rule**|**Categories**|
|---|---|---|
|Expiry pressure|hours_remaining|<24h CRITICAL; 24–<48 HIGH; 48–72<br>MODERATE;>72 LOW|
|Inventory coverage days|quantity / sales_velocity; zero velocity →<br>None/infnite semantic state|≤1 LOW; >1–≤3 MODERATE; >3–≤7 HIGH;<br>>7 VERY_HIGH|
||sales_velocity /|≥1.2 STRONG; 0.8–<1.2 NORMAL; 0.5–|
|Demand ratio|historical_average_daily_sales; zero<br>baseline handled explicitly|<0.8 WEAK; <0.5 VERY_WEAK; special<br>zero cases|



FoodLoop AI Service — Full Technical Report 

- Zero sales velocity with positive historical baseline → inventory coverage None, VERY_HIGH inventory pressure, demand ratio 0.0, NO_CURRENT_SALES. 

- Zero sales velocity with zero historical baseline → inventory coverage None, VERY_HIGH inventory pressure, demand ratio None, NO_DEMAND_BASELINE. 

- No division-by-zero is tolerated; the implementation uses explicit semantic states rather than fabricated ratios. 

### **4.6 Risk Assessment** 

Risk Assessment combines deterministic signals with request context and optional weather/holiday context. The LLM is instructed to treat deterministic signals as authoritative evidence while interpreting external context. 

#### RiskAssessmentResult 

risk_level: LOW | MEDIUM | HIGH | CRITICAL reason: concise, auditable rationale confidence: [0,1] 

### **4.7 Deterministic Routing** 

|**Risk level**|**Route**|
|---|---|
|LOW|NO_ACTION|
|MEDIUM|PRICING|
|HIGH|PRICING|
|CRITICAL|PRICING|



Routing makes zero LLM calls. Missing risk_level raises an explicit RiskAssessmentMissingError. No pricing or donation logic exists inside the Monitoring Agent. 

## **5. Pricing Agent — Complete Detail** 

The Pricing Agent is a structured recommendation-only optimizer. It receives only products selected for pricing and returns one auditable recommendation per product. 

### **5.1 Batch Contract** 

PricingBatchRequest 

- ├── store_id 

- ├── store_policy: StorePolicy 

- └── products[] 

#### PricingBatchResponse 

   - ├── store_id 

   - └── decisions[] 

- Multiple products can be priced in one request to reduce LLM/embedding overhead. 

- Every decision must preserve product_id exactly. 

- Duplicate, missing, or unknown product decisions are rejected; no silent product dropping or fallback discount is allowed. 

- Store identity is validated between request.store_id and store_policy.store_id. 

### **5.2 Pricing Decision Output** 

PricingDecision (LLM-owned) product_id discount_percentage: 0..15 

reason: mandatory, concise, auditable 

FoodLoop AI Service — Full Technical Report 

confidence: 0..1 

#### Python policy interpretation (deterministic) 

action_requirement 

action_reason 

- Forbidden LLM fields: final_price, recommended_price, price_after_discount, price_floor_adjustment, approval/execution/automation decisions. 

- The LLM never calculates or returns a final monetary price. 

- The AI-side reason explains why the discount is recommended; action_reason explains why assisted/autonomous handling applies. 

- Operating mode does not change the pricing recommendation itself; it only changes downstream execution handling. 

### **5.3 Pricing Signals** 

- Inventory quantity is never treated as an absolute proxy for pressure. The model uses demand-relative inventory coverage. 

- Example: 200 units at 200/day means 1 day coverage; 200 units at 5/day means 40 days coverage. 

- Demand ratio compares current velocity with historical average sales. 

- Expiry pressure is based on remaining hours. 

- These are evidence signals, not direct discount-percentage mappings. For example, HIGH risk does not hardcode to 10% and CRITICAL does not hardcode to 15%. 

### **5.4 Historical RAG** 

HistoricalPricingEvent 

↓ 

build_pricing_knowledge_document() 

- ↓ 

EmbeddingProvider 

- ↓ 

QdrantVectorStore 

- ↓ 

store_id + product_id filters 

- ↓ 

PricingKnowledgeItem[] 

- ↓ 

Pricing Agent prompt 

- Historical documents are facts only; they never contain a hidden recommendation such as “the optimal discount is 10%.” 

- Historical evidence is supporting context, not a hard rule. The LLM must not blindly copy a historical discount. 

- Products without historical evidence receive an explicit empty knowledge section rather than fabricated data. 

- Store and product isolation are enforced at the vector-store query level. 

## **6. Embeddings & Vector Search** 

### **6.1 Embedding Strategy** 

- The platform is bilingual (Arabic + English), so the MVP embedding model was changed from an English-only BGEsmall option to BAAI/bge-m3. 

- BGE-M3 is run locally through sentence-transformers; no per-call embedding API cost is required for the MVP. 

FoodLoop AI Service — Full Technical Report 

- Vector dimension: 1024. 

- Model loading is lazy/controlled and the model is reused within a process. 

- FakeEmbeddingProvider remains for deterministic unit tests; it is not the production provider. 

### **6.2 Provider Abstraction** 

|**Provider**|**Purpose**|
|---|---|
|LocalBGEEmbeddingProvider|MVP/demo and intendedproduction embedding provider|
|OpenAIEmbeddingProvider|Optional future embedding provider;not required for MVP|
|FakeEmbeddingProvider|Ofline deterministic tests only|



### **6.3 Cross-lingual Retrieval** 

The implementation includes tests for Arabic, English, and mixed-language text, including cross-lingual retrieval (e.g., Arabic historical text retrieved by an English query). 

### **6.4 Vector Store** 

- Provider-agnostic VectorStore interface supports upsert, search, and delete. 

- InMemoryVectorStore uses deterministic cosine similarity for tests. 

- QdrantVectorStore is the production implementation. 

- Qdrant collection uses COSINE distance and 1024 dimensions for BGE-M3. 

- Payload indexes: store_id, product_id, category. 

- Upsert is idempotent by document_id; deletion is safe/idempotent. 

- Production configuration explicitly disallows silent fallback from Qdrant to memory. 

## **7. Historical Pricing Ingestion** 

The ingestion pipeline decouples the AI service from the .NET database. .NET is the authoritative business-data source and calls an internal ingestion API. 

POST /api/v1/pricing/knowledge/ingest 

.NET historical data 

- ↓ 

HistoricalPricingEvent validation 

- ↓ 

Knowledge Builder 

- ↓ 

Batch BGE-M3 embedding 

- ↓ 

Qdrant upsert 

- Batch size default: 100, configurable via HISTORICAL_INGESTION_MAX_BATCH_SIZE. 

- event_id is deterministically mapped to document_id (reported as doc-{event_id}). 

- Re-ingesting the same event is idempotent; corrected snapshots replace existing knowledge instead of creating duplicates. 

- The ingestion service uses dependency injection for EmbeddingProvider and VectorStore. 

- Infrastructure failures propagate; there is no fake fallback embedding or fabricated knowledge. 

- A coherent historical pricing episode is required; metrics such as sales_velocity and sell_through_rate are supplied by the domain source rather than silently calculated by the AI service. 

FoodLoop AI Service — Full Technical Report 

### **7.1 Historical Data Contract with .NET** 

|**Field**|**Meaning / owner**|
|---|---|
|event_id|Stable identityof the historicalpricingepisode|
|store_id|Authoritative store identity|
|product_id|Authoritativeproduct identity|
|category|Product categorysnapshot|
|recorded_at|Time of historicalpricingepisode|
|quantity|Inventorysnapshot|
|current_price|Price at the event|
|original_price|Original/baseprice snapshot|
|price_foor|Backend business foor at the event|
|sales_velocity|<br>Backend/domain-defned sales velocity|
|historical_average_daily_sales|<br>Backend/domain-defned baseline|
|hours_remaining|<br>Remainingshelf-life/expirytime|
|discount_percentage|Discount actuallyapplied,bounded 0–15 for historical schema|
|units_sold_after_discount|Observed outcome|
|sell_through_rate|Observed outcome metric 0..1|
|outcome|SOLD_OUT / PARTIALLY_SOLD / UNSOLD / EXPIRED|



## **8. Store Operating Modes** 

The AI service now supports only two AI operating modes. Manual pricing is entirely outside the AI workflow. 

|**Mode**|**AI behavior**|**Backend behavior**|
|---|---|---|
|ASSISTED|Recommend discount + reason +<br>confdence|Wait for explicit owner approval, then<br>revalidate and execute|
|AUTONOMOUS|Recommend discount + reason +<br>confdence;<br>action_requirement=AUTOMATIC_EXEC<br>UTION_ELIGIBLE|Run deterministic backend safety<br>checks; execute automatically only if<br>eligible|
|MANUAL (outside AI)|AI not involved|Owner enters discount manually; normal<br>backend business rules apply|



- Operating mode is backend-owned configuration and is never inferred by the LLM. 

- For identical product context, assisted and autonomous modes should not alter the AI discount recommendation. 

- Python never performs actual financial execution. 

- The Python policy layer maps ASSISTED → APPROVAL_REQUIRED and AUTONOMOUS → AUTOMATIC_EXECUTION_ELIGIBLE. 

- The final price, price floor, authorization, approval persistence, and transaction execution remain in .NET. 

## **9. Final API Contracts** 

|**Endpoint**|**Purpose**|**Key result**|
|---|---|---|
|GET /health|Liveness|Process is alive; no provider dependency<br>required<br>|
|GET /ready|Readiness|Confguration/dependency readiness<br>accordingto environment|
|GET /version|Service metadata|Name/version/environment;no secrets<br>|
|POST /api/v1/monitoring/analyze|Risk/context analysis|route,risk_level,reason,confdence|
|POST /api/v1/pricing/recommend|Batch pricing recommendations|store_id + decisions[] with discount,<br>reason, confdence, action<br>requirement/reason|
|POST /api/v1/pricing/knowledge/ingest|Historical RAG ingestion|accepted/upserted/failed counts +<br>document IDs|



FoodLoop AI Service — Full Technical Report 

### **9.1 Monitoring response example** 

{ "route": "PRICING", "risk_level": "HIGH", "reason": "High inventory coverage with only 18 hours remaining.", "confidence": 0.93 } 

### **9.2 Pricing response example** 

{ "store_id": "store-cairo-01", "decisions": [ { "product_id": "p-100", "discount_percentage": 10.0, "reason": "High inventory coverage and short remaining shelf life support a moderate markdown.", "confidence": 0.92, "action_requirement": "APPROVAL_REQUIRED", "action_reason": "Store operates in assisted mode; explicit owner approval is required before execution." } ] } 

### **9.3 Pricing input structure** 

{ "store_id": "store-cairo-01", "store_policy": { "store_id": "store-cairo-01", "operating_mode": "assisted" }, "products": [ { "product_id": "p-100", "product_name": "Organic Milk 1L", "category": "Dairy", "inventory": { "quantity": 10, "original_price": 40.0, "current_price": 40.0, "price_floor": 28.0 }, "demand": { "sales_velocity": 0.5, "historical_sales": {"average_daily_sales": 5.0} }, "expiry": {"expires_at": "2026-08-16T12:00:00Z", "hours_remaining": 18.0}, "risk_assessment": {"risk_level": "HIGH", "reason": "Short remaining shelf life.", "confidence": 0.93} } 

FoodLoop AI Service — Full Technical Report 

] } 

## **10. Configuration & Environment** 

# LLM - SambaNova / OpenAI-compatible OPENAI_API_KEY=<secret> OPENAI_BASE_URL=https://api.sambanova.ai/v1 OPENAI_MODEL=gemma-2-27b-it OPENAI_TIMEOUT_SECONDS=30 

# Embeddings - local bilingual MVP EMBEDDING_PROVIDER=local_bge_m3 EMBEDDING_MODEL=BAAI/bge-m3 EMBEDDING_VECTOR_SIZE=1024 EMBEDDING_DEVICE=cpu 

# Vector store VECTOR_STORE_PROVIDER=qdrant QDRANT_URL=<production-endpoint> QDRANT_API_KEY=<secret> QDRANT_COLLECTION_NAME=foodloop_pricing_knowledge_bge_m3 QDRANT_VECTOR_SIZE=1024 QDRANT_TIMEOUT_SECONDS=10 

# Pricing PRICING_RETRIEVAL_TOP_K=5 MAX_PRICING_BATCH_SIZE=50 HISTORICAL_INGESTION_MAX_BATCH_SIZE=100 

# Weather 

WEATHER_PROVIDER=open_meteo WEATHER_API_BASE_URL=https://api.open-meteo.com/v1/forecast WEATHER_API_TIMEOUT_SECONDS=5 

# Holidays EVENTS_PROVIDER=nager_date HOLIDAY_API_BASE_URL=https://date.nager.at/api/v4 HOLIDAY_API_TIMEOUT_SECONDS=5 DEFAULT_COUNTRY_CODE=EG 

- Production secrets are injected through environment/secret management and never committed. 

- Development can use memory vector store and mock providers; production must reject fake providers and must not silently fall back. 

- Qdrant vector dimension must match BGE-M3 1024-d vectors. 

- HF_HOME/model cache is configurable for BGE-M3; no developer-specific filesystem path should be hardcoded into the production image. 

### **10.1 Deployment hardening** 

- Multi-stage production Docker build, non-root appuser, no --reload, configurable PORT/WORKERS, built-in /health healthcheck. 

FoodLoop AI Service — Full Technical Report 

- Conservative worker strategy recommended because each process may load its own BGE-M3 model copy; one worker is a safe default until memory profiling justifies more. 

- Production readiness verifies configuration, embedding model availability, Qdrant compatibility/connectivity, and other required providers without executing pricing. 

- Production smoke-check command validates dependencies and exits non-zero on failure; it must not alter business data. 

## **11. Testing & Verification Matrix** 

|**Area**|**Latest reported result / evidence**|
|---|---|
|Monitoring schema/API/agent|Covered by extensive unit/integration/E2E tests; routing and<br>errorpropagation verifed<br>|
|Pricing schema/API/agent|Batch mapping, discount/confdence bounds, forbidden felds,<br>errors verifed|
|Pricing scenario tests|19 scenario tests added; total reported 229 passing at that<br>milestone<br>|
|Open-Meteo|Ofline tests + opt-in live test;live run reported 10/10passing<br>|
|Nager.Date|Ofline tests + opt-in live test;live run reported 17/17passing|
|Historical ingestion|22 ingestion tests; reported cumulative 280 passed / 3 skipped<br>at that milestone|
|BGE-M3 hardening|Arabic/English/cross-lingual tests and 1024-d enforcement;<br>reported 289passed / 4 skipped out of 293|
|Finalproduction-readiness suite|Reported 291passed,7 skipped,0 failures|
|External calls in normalpytest|Reported zero external network calls|
|Secret handling|No tracked hardcoded credentials reported; API keys redacted<br>from logs/errors|



### **11.1 Testing philosophy** 

- Normal pytest is deterministic and offline. 

- Live provider tests are opt-in using RUN_EXTERNAL_INTEGRATION_TESTS=true. 

- FakeEmbeddingProvider and InMemoryVectorStore are test infrastructure only. 

- Live smoke tests should use isolated collections/test data and must never modify the production knowledge corpus. 

- The system fails explicitly on provider/LLM failures instead of inventing NO_ACTION, 0% discounts, or fake historical data. 

## **12. External Providers & MVP Decisions** 

|**Capability**|**MVP choice**|**Rationale / status**|
|---|---|---|
|LLM|SambaNova OpenAI-compatible API /<br>Gemma 2 27B IT|Live connectivity and structured-output<br>smoke test reported successful|
|Embeddings|Local BAAI/bge-m3|Arabic + English, no per-call embedding<br>cost,1024-d|
|Weather|Open-Meteo|Real development/demo integration; no<br>API keyfor free endpoint|
|Holidays|Nager.Date v4|Simple factual public/national-holiday<br>context; no fabricated attendance/venue<br>data|
|Vector DB|Qdrant|Production-oriented vector search with<br>hard metadata fltering|



FoodLoop AI Service — Full Technical Report 

For the MVP/demo, Open-Meteo is used as a real development provider. A commercial provider endpoint or selfhosted deployment can be used later for commercial production as required by provider terms. Nager.Date is intentionally limited to public/national holiday context rather than generic live events. 

## **13. End-to-End Business Flows** 

### **13.1 Assisted** 

1. .NET loads authoritative store policy and product data 

2. .NET calls Monitoring 

3. Monitoring analyzes context / optional weather / holidays 

4. Monitoring assesses risk 

5. LOW → NO_ACTION; otherwise → PRICING 

6. .NET sends only pricing candidates as a batch 

7. Pricing retrieves historical knowledge with store/product isolation 

8. Pricing calculates deterministic signals 

9. Pricing LLM recommends 0–15% + reason + confidence 

10. Python maps policy → APPROVAL_REQUIRED 

11. .NET persists pending recommendation and waits for owner approval 

12. On approval, .NET revalidates current state / version 

13. .NET calculates final price and enforces price floor / business rules 

14. .NET executes the transaction 

### **13.2 Autonomous** 

1. Same Monitoring + Pricing flow 

2. Python maps policy → AUTOMATIC_EXECUTION_ELIGIBLE 

3. .NET rechecks store mode, product state, configured auto limits, price floor, and freshness 

4. If all checks pass → .NET executes 

5. If any check fails → reject/escalate according to backend policy 

6. Python never executes the transaction 

### **13.3 Manual** 

Manual pricing is outside the AI workflow. The owner enters the discount directly in the backend/UI; normal .NET business validation and execution rules apply. No AI recommendation is required. 

## **14. Security, Safety & Failure Model** 

- No API key, authorization header, or secret should be logged or returned in API responses. 

- Production Qdrant must not silently fall back to memory. This prevents the AI service from operating with an empty/false historical context while pretending success. 

- Provider failures propagate explicitly and are observable. 

- Store and product isolation are hard invariants at the vector-store filter level and in prompt construction. 

- The AI output is never treated as final financial authority. .NET independently validates price floor, limits, authorization, current state, and execution eligibility. 

- No chain-of-thought is exposed. Reasons are concise business-facing rationales only. 

- Approval in Assisted mode must be handled as a backend workflow; AI should not “wait” inside an LLM call. 

- Autonomous mode is bounded autonomy: the AI recommends, deterministic backend policy authorizes eligibility, and .NET performs the side effect. 

FoodLoop AI Service — Full Technical Report 

## **15. Final Remaining Work / Handoff Boundary** 

|**Item**|**Status / owner**|
|---|---|
|Real Qdrant production endpoint + secret injection|Environment/deployment dependency; implementation<br>supports it, actual endpoint/secret must be supplied by<br>deployment/infra owner|
|Production deployment platform|Deployment decision/infra task; Python service is Docker-<br>ready|
|.NET integration<br>|Out of Python scope;separate .NET owner|
|Approval workfow<br>|Out of Python scope;.NET owner|
|Finalprice calculation /price foor / DB transaction|Out of Python scope;.NET owner|
|Full E2E smoke test|Joint task after both sides are deployed and connected|



Definition of Done for the Python AI Service: AI application logic, retrieval, providers, ingestion, policy interpretation, test coverage, and deployment hardening are complete. The remaining work is environment provisioning and integration with the separate .NET execution layer. 

## **Appendix A. Important Design Decisions** 

- Dual-agent separation: Monitoring routes/risk; Pricing optimizes discount recommendation. 

- Deterministic formulas where arithmetic/business signals are stable; LLM used for contextual interpretation and recommendation, not arithmetic authority. 

- Batch pricing by store to reduce embedding/LLM overhead while preserving product isolation. 

- Store policy is backend-owned; operating mode is not an LLM signal. 

- Public-holiday context replaced generic local-event attendance modeling for MVP safety and factuality. 

- Local BGE-M3 was selected because the product is Arabic + English and the MVP benefits from zero per-call embedding API cost. 

- Qdrant hard filters are used for store/product isolation; similarity alone is never trusted for tenancy boundaries. 

- Historical data is ingested as factual snapshots; the AI must not invent missing business history or convert historical discounts into hard rules. 

## **Appendix B. Final High-Level Directory Shape** 

- app/ ├── api/ │└── routes/ │ ├── health.py │ ├── monitoring.py │ └── pricing.py ├── agents/ │├── monitoring/ ││├── state.py 

- ││├── nodes.py ││├── graph.py ││├── prompts.py ││└── risk_signals.py │└── pricing/ │ ├── state.py │ ├── nodes.py │ ├── graph.py │ ├── prompts.py 

FoodLoop AI Service — Full Technical Report 

│ ├── retriever.py │ ├── signals.py │ ├── config.py │ └── knowledge_builder.py ├── embeddings/ │├── base.py │├── bge_m3.py │├── fake.py │├── openai.py │└── factory.py ├── schemas/ │├── monitoring.py │├── context_analysis.py │├── risk_assessment.py │├── pricing.py │├── pricing_signals.py │├── pricing_knowledge.py │├── pricing_knowledge_document.py │├── historical_pricing.py │└── store_policy.py ├── tools/ │├── weather.py │└── events.py ├── vector_store/ │├── base.py │├── in_memory.py │├── qdrant.py │├── qdrant_client.py │└── factory.py ├── policies/ │└── store_policy.py ├── config/ │├── settings.py │└── validation.py └── scripts/ └── production_smoke_check.py 

## **Appendix C. Core File / Module Inventory** 

The following inventory consolidates the concrete module names reported during implementation. It is a representative implementation inventory based on the project summaries shared during the conversation, not a fresh repository filesystem listing. 

- app/main.py — FastAPI application entrypoint and router registration. 

- app/api/routes/health.py — health/liveness endpoint.  app/api/routes/monitoring.py — POST /api/v1/monitoring/analyze. 

- app/api/routes/pricing.py — POST /api/v1/pricing/recommend and pricing batch contract. 

- app/config/settings.py — environment-driven configuration, model/provider settings, batch/timeouts. 

- app/config/validation.py — production configuration validation and provider restrictions. 

- app/llm/model.py / app/llm/factory.py / app/llm/__init__.py — shared LLM abstraction and SambaNova/OpenAIcompatible client factory. 

FoodLoop AI Service — Full Technical Report 

- app/embeddings/base.py — provider abstraction and vector validation. 

- app/embeddings/fake.py — deterministic test embeddings. 

- app/embeddings/bge_m3.py — local BAAI/bge-m3 multilingual embedding provider. 

- app/embeddings/openai.py — optional OpenAI embedding provider. 

- app/embeddings/factory.py — embedding provider selection. 

- app/tools/weather.py — WeatherProvider abstraction, mock provider, Open-Meteo provider. 

- app/tools/events.py — HolidayProvider abstraction, mock provider, Nager.Date provider. 

- app/vector_store/base.py — VectorStore contract and validation. 

- app/vector_store/in_memory.py — deterministic in-memory vector store for tests. 

- app/vector_store/qdrant.py / qdrant_client.py — production Qdrant adapter and client factory. 

- app/vector_store/factory.py — memory/qdrant selection. 

- app/schemas/monitoring.py — MonitoringRequest/MonitoringResponse and domain context schemas. 

- app/schemas/context_analysis.py — structured context-analysis result. 

- app/schemas/risk_assessment.py — structured risk-assessment result. 

- app/schemas/pricing.py — PricingBatchRequest/PricingDecision/PricingBatchResponse and batch LLM result. 

- app/schemas/pricing_signals.py — deterministic signal schema/enums. 

- app/schemas/historical_pricing.py — canonical historical pricing event and outcome enum. 

- app/schemas/pricing_knowledge.py — PricingKnowledgeItem. 

- app/schemas/pricing_knowledge_document.py — factual retrieval document model. 

- app/schemas/store_policy.py — OperatingMode and StorePolicy. 

- app/policies/store_policy.py — deterministic ActionRequirement mapping. 

- app/agents/monitoring/state.py / nodes.py / graph.py / prompts.py — Monitoring Agent state, nodes, routing and prompts. 

- app/agents/monitoring/risk_signals.py — deterministic monitoring risk formulas. 

- app/agents/pricing/state.py / nodes.py / graph.py / prompts.py — Pricing Agent workflow/state/prompting. 

- app/agents/pricing/retriever.py — product-isolated retrieval and VectorPricingKnowledgeRetriever. 

- app/agents/pricing/signals.py — deterministic pricing-signal calculation. 

- app/agents/pricing/config.py — pricing thresholds and limits. 

- app/agents/pricing/knowledge_builder.py — deterministic historical document construction. 

- app/scripts/production_smoke_check.py — production dependency/readiness smoke checks. 

## **Appendix D. Test Suite Inventory** 

- Health and core API tests. 

- Monitoring schema validation and API integration tests. 

- Context analysis schema/node tests and unsupported-context validation. 

- Weather tool/provider tests and Open-Meteo integration tests. 

- Holiday/Nager.Date provider tests and opt-in live integration. 

- Risk signal threshold tests and risk-assessment LLM tests. 

- Routing tests covering LOW/PRICING paths and missing risk errors. 

- Pricing schema, agent, API, retrieval, and batch mapping tests. 

- Pricing signal boundary and zero-demand tests. 

- Pricing recommendation scenario suite (19 scenarios). 

- HistoricalPricingEvent and knowledge-builder tests. 

- Embedding provider tests including BGE-M3 Arabic/English/cross-lingual behavior. 

- Vector store tests for cosine ranking, filtering, isolation, idempotency and deletion. 

- Qdrant adapter tests and opt-in live Qdrant smoke tests. 

- Store policy and operating-mode independence tests. 

FoodLoop AI Service — Full Technical Report 

- Historical ingestion tests including batch embedding, idempotency, failure propagation and in-memory end-to-end retrieval. 

- Live SambaNova/Gemma structured-output smoke test. 

- Production configuration, readiness, Docker/deployment sanity, and secret-redaction tests. 

## **Appendix E. Latest Reported Verification Snapshot** 

Latest supplied production-readiness report: 291 PASSED, 7 SKIPPED, 0 FAILURES in 77.12 seconds. The seven skipped tests are described as opt-in live/external integration checks. Earlier summaries reported 293 tests at the BGEM3 hardening stage and 280 tests at the historical-ingestion stage; these are milestone snapshots, not contradictory final totals. 

FoodLoop AI Service — Full Technical Report 

