# FoodLoop API — Sprint 1 (Foundation + Authentication)

.NET 10 Web API implementing the Sprint 1 backend goals from `foodLoop-sprints.md`:
Clean Architecture, database foundation, ASP.NET Core **Identity**, **JWT** access tokens
+ rotating refresh tokens, **RBAC**, and User CRUD (profile, addresses, preferences).

This revision reconciles the original spec-only build against the actual `UI_Screens_Stitch`
designs — see "Reconciling the spec docs with the UI screens" below for what changed and why.

## ⚠️ Important: this was not compiled in the sandbox

I built this in an environment with no internet access to NuGet (only `.NET 8 SDK` could
be installed via apt; NuGet.org is not reachable at all). So **I could not run `dotnet
restore` / `dotnet build` / `dotnet ef migrations add` here** — everything below is
written carefully by hand following standard ASP.NET Core 8/10 + EF Core + Identity
patterns, but you should do a `dotnet build` locally as your first step and expect to
fix the odd typo. Nothing here is exotic, but I want to be upfront rather than imply
it's been verified.

## Reconciling the spec docs with the UI screens

The `.md` specs and the real UI screens disagreed on a few things. Each was resolved as a
**superset** — nothing from the docs was dropped, the UI's exact fields were added on top.

1. **Account types.** The docs' RBAC roles (`Consumer`/`Merchant`/`Courier`/`Administrator`)
   are unchanged and still drive `[Authorize(Roles = ...)]`. The signup screens'
   `User`/`Store Owner`/`Charity` dropdown (`create_account_account_type_selection`,
   `business_signup_step_1`) is a separate `AccountType` enum on `RegisterRequest`:
   `User` → `Consumer` role, `StoreOwner`/`Charity` → `Merchant` role + a `Store.StoreType`
   of `Standard`/`Charity`. `Courier` stays admin-provisioned only, as before.
2. **Business signup is a 3-step wizard**, not one flat call: `POST /auth/register` (step 1 —
   store name, owner name, phone, email, password, business type) now also creates a *draft*
   `Store` when `AccountType` is business. New `StoresController` endpoints handle the rest:
   `PATCH /stores/me/location` (step 2 — governorate/city/neighborhood/street, matching
   `business_verification_location`) and `POST /stores/me/documents` (step 2 — one call per
   document type, matching `document_upload_step_2`'s three upload slots). `GET /stores/me`
   returns the current status for `verification_pending_step_3` and lets the wizard resume
   where the user left off.
3. **Address fields** now match `add_address` exactly: `AddressType` (Home/Company) instead
   of a free-text label, `City`/`District`/`Street`/`BuildingNo`/`Floor`/`ApartmentNo`/`Notes`
   instead of `Label`/`Country`/`Area`.
4. **Notification preferences** are the two toggles actually on `profile_settings`
   ("Order Updates" / "Latest Offers") — `ApplicationUser.OrderUpdatesEnabled` /
   `MarketingNotificationsEnabled`, renamed from the previous generic push/email split.
5. **Document uploads** needed *some* storage to work end-to-end, so `IFileStorageService`
   ships a local-disk implementation (`wwwroot/uploads/...`, served via `UseStaticFiles`).
   Swap it for a real Object Storage-backed implementation later — nothing else changes.

## Architecture

Clean Architecture, 4 projects:

```
src/
  FoodLoop.Domain          # Entities, enums — no dependencies
  FoodLoop.Application     # DTOs, service interfaces, IApplicationDbContext — depends on Domain only
  FoodLoop.Infrastructure  # EF Core, ASP.NET Identity, JWT, service implementations
  FoodLoop.API             # Controllers, Program.cs, middleware
```

- **Domain** has no package dependencies at all (pure POCOs + enums).
- **Application** defines *interfaces* (`IApplicationDbContext`, `IAuthService`, `IUserService`,
  `IStoreService`, `IJwtTokenService`, `ICurrentUserService`, `IEmailService`,
  `IFileStorageService`) — Infrastructure implements them. This is the Dependency Inversion
  piece of Clean Architecture: API and Application never reference EF Core or Identity types
  directly (file uploads go through the framework-agnostic `FileUploadRequest`, not `IFormFile`).
- **Infrastructure** contains `ApplicationDbContext` (extends `IdentityDbContext<ApplicationUser,
  ApplicationRole, Guid>`), all EF fluent configurations, `AuthService`, `UserService`,
  `StoreService`, `JwtTokenService`, `LocalFileStorageService`, and the DI wiring in
  `DependencyInjection/InfrastructureServiceRegistration.cs`.
- **API** is thin: controllers just call Application service interfaces and wrap results in
  the standard `{success, data}` / `{success, message, errors}` envelope from
  `FoodLoop API Documentation.md` §15.

## What's implemented (matches the Sprint 1 backend checklist)

- ✅ Clean Architecture (4-project layering above)
- ✅ Database — full EF Core model for **every** entity in `FoodLoop Database Design.md`
  (Users via Identity, Address, Store, StoreVerification, Category, ProductListing,
  ProductImage, Favorite, Order, OrderItem, Payment, Review, Notification, SupportTicket,
  TicketMessage, AIRecognitionResult), soft-delete convention, audit fields, and the
  suggested indexes from §5 of that doc — with `Address` and `Store` restructured to match
  the real UI screens (see reconciliation notes above).
- ✅ Authentication — register / login / refresh / logout / forgot-password / reset-password,
  all matching `FoodLoop API Documentation.md` §4 routes and response shapes. Register now
  also drives the business signup wizard's first step.
- ✅ JWT access tokens (15 min default) + rotating, DB-persisted refresh tokens (30 days
  default) with reuse detection (if a revoked refresh token is replayed, all of that
  user's sessions are revoked as a precaution).
- ✅ RBAC — `Consumer`, `Merchant`, `Courier`, `Administrator` roles seeded on startup via
  ASP.NET Core Identity roles; enforce with `[Authorize(Roles = AppRole.Merchant)]` etc.
  Administrator cannot be granted through public `/auth/register`.
- ✅ User CRUD — `GET/PATCH /users/me`, `PATCH /users/me/preferences`,
  `GET/POST/PATCH/DELETE /users/me/addresses/{id}` (API doc §5), with `Address` fields
  matching `add_address` and preferences matching `profile_settings`.
- ✅ Store onboarding — `Store` + `StoreVerification` entities, plus a minimal
  `StoresController` (`GET/PATCH /stores/me`, `POST /stores/me/documents`) covering just
  enough to finish the business signup wizard end-to-end. Full Store CRUD (browsing,
  editing a live/published store, etc.) is still Sprint 2 per the sprint plan.
- ✅ Standard response envelope + global exception-handling middleware (API doc §15).
- ✅ Swagger/OpenAPI with a JWT bearer auth button, so you can test everything from the browser.

Not in Sprint 1 (intentionally, per `foodLoop-sprints.md`): Product CRUD, Marketplace,
Orders, AI endpoints, Admin — those are Sprints 2–5.

## Getting started

### 1. Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (2019+), or run `docker compose up -d` to start SQL Server 2022 locally
  (see `docker-compose.yml`). Note: `FoodLoop System Architecture.md` §6 mentions PostgreSQL —
  this build was switched to SQL Server per explicit request; update that doc if you want the
  spec to reflect the actual stack.

### 2. Configure secrets

Don't leave real secrets in `appsettings.json`. For local dev, use user-secrets:

```bash
cd src/FoodLoop.API
dotnet user-secrets init
dotnet user-secrets set "Jwt:Secret" "a-long-random-32-plus-character-secret"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=foodloop_dev;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

### 3. Restore & build

```bash
cd /path/to/FoodLoop
dotnet restore
dotnet build
```

### 4. Create the initial migration & apply it

```bash
dotnet tool install --global dotnet-ef   # if you don't have it
cd src/FoodLoop.API
dotnet ef migrations add InitialCreate --project ../FoodLoop.Infrastructure --startup-project .
dotnet ef database update --project ../FoodLoop.Infrastructure --startup-project .
```

(`Program.cs` also auto-applies pending migrations on startup in the Development
environment, so `dotnet run` after the first migration is added will keep the DB in sync.)

### 5. Run

```bash
dotnet run --project src/FoodLoop.API
```

Swagger UI: `https://localhost:<port>/swagger`

### 6. Try it

```bash
# Register — plain consumer (AccountType defaults to "User")
curl -X POST https://localhost:<port>/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Sara Ahmed","email":"sara@example.com","password":"P@ssw0rd1"}'

# Register — business account (creates a draft Store automatically)
curl -X POST https://localhost:<port>/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Sara Ahmed","email":"sara.store@example.com","password":"P@ssw0rd1","phoneNumber":"+201001234567","accountType":"StoreOwner","businessName":"Green Valley Groceries","businessCategory":"Supermarket"}'

# Login
curl -X POST https://localhost:<port>/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"sara@example.com","password":"P@ssw0rd1"}'

# Authenticated call
curl https://localhost:<port>/users/me \
  -H "Authorization: Bearer <accessToken>"

# Business onboarding step 2 — location (business_verification_location)
curl -X PATCH https://localhost:<port>/stores/me/location \
  -H "Authorization: Bearer <accessToken>" -H "Content-Type: application/json" \
  -d '{"governorate":"Cairo","city":"Cairo","neighborhood":"Al-Rawda","street":"King Fahd Rd."}'

# Business onboarding step 2 — documents (document_upload_step_2), one call per slot
curl -X POST https://localhost:<port>/stores/me/documents \
  -H "Authorization: Bearer <accessToken>" \
  -F "type=CommercialRegistration" -F "file=@/path/to/commercial-registration.pdf"

# Business onboarding step 3 — status (verification_pending_step_3)
curl https://localhost:<port>/stores/me \
  -H "Authorization: Bearer <accessToken>"
```

## Notes & things you'll likely want to adjust

- **Package versions**: I pinned `10.0.0` for Microsoft.* packages, including `Microsoft.EntityFrameworkCore.SqlServer 10.0.0`.
  If those exact versions aren't published yet when you restore, bump to whatever's current
  for your installed SDK — nothing in the code is version-sensitive.
- **Email service** (`NullEmailService`) just logs instead of sending — swap in a real
  provider (SendGrid/SES/etc.) behind `IEmailService` when you get to it; nothing else needs
  to change.
- **CORS** origins are read from `Cors:AllowedOrigins` in `appsettings.json` — update for
  your frontend's dev URL.
- Table names for Identity are renamed to `Users`/`Roles`/`UserRoles`/etc. in
  `ApplicationDbContext.OnModelCreating` to match the Database Design doc's naming instead
  of the default `AspNetUsers` etc.
- `Store.OwnerId` and other cross-aggregate references (e.g. `Order.UserId`) are plain
  `Guid` foreign keys without navigation properties back to `ApplicationUser`, by design —
  it keeps the Domain project free of any Identity dependency. EF Core is fine with this;
  you just query by the FK instead of `.Include()`.
