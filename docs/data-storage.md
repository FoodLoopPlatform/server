# Data Storage

Everything is stored in a single SQL Server database via Entity Framework Core. All tables follow two conventions automatically applied by `ApplicationDbContext`:
- **Audit fields**: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` are stamped on every save.
- **Soft delete**: deletions set `IsDeleted = true` and `DeletedAt` rather than removing the row.

---

## Users

Table: `Users` (ASP.NET Core Identity `IdentityUser<Guid>` extended)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Email | string | Unique, used as username |
| PhoneNumber | string? | Unique across all users |
| FullName | string | Display name |
| Language | string | `"en"` or `"ar"` |
| ProfileImage | string? | URL |
| Status | enum | Active / Suspended / Banned / PendingVerification |
| OrderUpdatesEnabled | bool | Notification preference |
| MarketingNotificationsEnabled | bool | Notification preference |
| CreatedAt | DateTimeOffset | |
| UpdatedAt | DateTimeOffset? | |
| PasswordHash | string | Managed by Identity |
| SecurityStamp | string | Managed by Identity |

Supporting Identity tables: `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens`, `RoleClaims`.

Roles seeded at startup: `Customer`, `Merchant`, `Charity`, `Admin`.

---

## Refresh Tokens

Table: `RefreshTokens`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → Users |
| Token | string (500) | 64-byte random, Base64 |
| ExpiresAt | DateTimeOffset | Now + 30 days |
| RevokedAt | DateTimeOffset? | Set on logout, rotation, or reuse detection |
| ReplacedByToken | string? | The new token string that superseded this one |
| CreatedByIp | string? | Client IP at issuance |
| RevokedByIp | string? | Client IP at revocation |

A token is **active** when `RevokedAt == null AND ExpiresAt > now`. Each use rotates the token (old is revoked, new is issued).

---

## Stores

Table: `Stores`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| OwnerId | Guid | FK → Users |
| Name | string (150) | English name |
| NameAr | string? (150) | Arabic name |
| Description | string? | English description |
| DescriptionAr | string? | Arabic description |
| Logo | string? | URL |
| Phone | string? | Contact phone |
| Email | string? | Contact email |
| BusinessCategory | enum? | Restaurant / Bakery / Supermarket / etc. |
| Governorate | string? (100) | Set in onboarding step 2 |
| City | string? (100) | |
| Neighborhood | string? (100) | |
| Street | string? (200) | |
| BuildingNo | string? | Building / landmarks |
| Latitude | double? | |
| Longitude | double? | |
| OpeningHours | string? | JSON-encoded weekly schedule |
| VerificationStatus | enum | Unverified → Pending → Verified / Rejected |
| AverageRating | double | Computed from reviews |
| IsDeleted | bool | Soft delete |

A draft store is created at registration for Merchant and Charity accounts. The store moves through verification states as documents are uploaded and reviewed.

---

## Store Verification Documents

Table: `StoreVerifications`

Three document slots per store, matching the `document_upload_step_2` screen:

| Document type | Meaning |
|---|---|
| `CommercialRegistration` | Official business registration certificate |
| `TaxIdCertificate` | Tax identification document |
| `StoreFacilityPhoto` | Exterior photograph of the store |

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| StoreId | Guid | FK → Stores |
| VerificationType | string (100) | One of the three types above |
| DocumentUrl | string (500) | Path served as static file |
| Status | enum | Pending / Verified / Rejected |
| ReviewedBy | Guid? | FK → Users (admin who reviewed) |
| ReviewedAt | DateTimeOffset? | |

Once all three document types are uploaded, `Store.VerificationStatus` moves to `Pending` automatically.

---

## Product Listings

Table: `ProductListings`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| StoreId | Guid | FK → Stores |
| CategoryId | Guid | FK → Categories |
| Title | string (200) | English title |
| TitleAr | string? (200) | Arabic title |
| Description | string? | English description |
| DescriptionAr | string? | Arabic description |
| OriginalPrice | decimal (10,2) | Price before discount |
| DiscountedPrice | decimal (10,2) | AI-recommended or merchant-set price |
| QuantityAvailable | int | Current stock |
| **ExpirationDate** | DateOnly | **The core data point for the platform** — drives discount urgency |
| Status | enum | Active / Inactive / SoldOut / Expired |
| IsDeleted | bool | Soft delete |

The `ExpirationDate` is how the platform works — items close to expiry get deeper discounts. The AI reads this field to calculate discount recommendations.

### Product Images

Table: `ProductImages`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ListingId | Guid | FK → ProductListings |
| ImageUrl | string (500) | |
| DisplayOrder | int | Ordering for carousel |

---

## AI Recognition Results

Table: `AIRecognitionResults`

Stores the output of OCR / GPT Vision analysis per product listing.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ListingId | Guid | FK → ProductListings (1-to-1) |
| DetectedProduct | string? (200) | Product name extracted from image |
| ExtractedText | string? (4000) | All text found on the label |
| **ExtractedExpiryDate** | DateOnly? | **Expiry date read from the label** |
| ConfidenceScore | double | 0.0 – 1.0 |
| Reviewed | bool | Whether a human has confirmed this result |

This table is what bridges the physical product label to the digital listing. The `ConfidenceScore` drives whether the AI acts autonomously or queues the result for human review.

---

## Categories

Table: `Categories`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Name | string (100) | English, unique |
| NameAr | string? (100) | Arabic |
| Icon | string? | Icon identifier |

---

## Orders

Table: `Orders`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → Users (buyer) |
| TotalAmount | decimal (10,2) | |
| PaymentStatus | enum | Pending / Paid / Failed / Refunded |
| OrderStatus | enum | Pending / Confirmed / Ready / PickedUp / Cancelled |

Table: `OrderItems`

| Field | Type | Notes |
|---|---|---|
| OrderId | Guid | PK (composite) |
| ListingId | Guid | PK (composite) |
| Quantity | int | |
| UnitPrice | decimal (10,2) | Price at time of purchase |

---

## Payments

Table: `Payments` (1-to-1 with Orders)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| OrderId | Guid | FK → Orders (unique) |
| Amount | decimal (10,2) | |
| Method | string (50) | e.g. "Card", "Cash", "Wallet" |
| Status | enum | Pending / Completed / Failed / Refunded |
| TransactionReference | string? (200) | Payment gateway reference |

---

## Reviews

Table: `Reviews`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| StoreId | Guid | FK → Stores |
| UserId | Guid | FK → Users |
| OrderId | Guid | FK → Orders (unique — one review per order) |
| Rating | int | 1–5 |
| Comment | string? (1000) | |

---

## Addresses

Table: `Addresses`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → Users |
| AddressType | enum | Home / Company |
| City | string (100) | |
| District | string (100) | |
| Street | string (200) | |
| BuildingNo | string? (20) | |
| Floor | string? (20) | |
| ApartmentNo | string? (20) | |
| Notes | string? (300) | Landmarks / special instructions |
| Latitude | double | |
| Longitude | double | |
| IsDefault | bool | One default address per user |

---

## Notifications

Table: `Notifications`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → Users |
| Title | string (200) | |
| Body | string (1000) | |
| Type | string (50) | e.g. "OrderUpdate", "Promotion", "VerificationResult" |
| IsRead | bool | |

---

## Support Tickets

Table: `SupportTickets`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → Users |
| Category | string | e.g. "Delivery", "Payment", "Account" |
| Priority | enum | Low / Normal / High / Urgent |
| Status | enum | Open / InProgress / Resolved / Closed |

Table: `TicketMessages`

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| TicketId | Guid | FK → SupportTickets |
| Body | string | Message content |
| SentBy | Guid | User or admin ID |

---

## What the AI stores (summary)

The critical AI-stored data lives in `AIRecognitionResults`:

- **`ExtractedExpiryDate`** — read from the product label image by OCR/GPT Vision. This is the most important field — it's what the pricing algorithm uses to calculate how urgent the discount is.
- **`DetectedProduct`** — product name extracted from the label.
- **`ExtractedText`** — full raw text from the label, kept for auditing and re-processing.
- **`ConfidenceScore`** — determines whether the AI's output is trusted autonomously or sent to a human moderator.

The discount recommendation itself lives in `ProductListing.DiscountedPrice`. The AI writes this field when it has sufficient confidence; otherwise it proposes a value and waits for merchant confirmation.
