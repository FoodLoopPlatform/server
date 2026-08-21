# FoodLoop Backend Architecture & API Meeting Guide

This guide is a complete technical and business reference for the **FoodLoop Backend Platform**. It is designed to prepare you for meetings, architecture walkthroughs, technical Q&A, and endpoint explanations.

---

## 📑 Table of Contents
1. [System Architecture & Tech Stack](#1-system-architecture--tech-stack)
2. [Security, Roles & Authentication Flow](#2-security-roles--authentication-flow)
3. [Core Business Workflows & AI Features](#3-core-business-workflows--ai-features)
4. [Comprehensive API Endpoint Reference](#4-comprehensive-api-endpoint-reference)
   - [4.1 Authentication & Security (`/auth`)](#41-authentication--security-auth)
   - [4.2 User Profiles & Delivery Addresses (`/users`)](#42-user-profiles--delivery-addresses-users)
   - [4.3 Stores & Merchants Management (`/stores`)](#43-stores--merchants-management-stores)
   - [4.4 Charities & NGOs (`/charities`)](#44-charities--ngos-charities)
   - [4.5 Product Categories (`/categories`)](#45-product-categories-categories)
   - [4.6 Merchant Product Inventory & CSV Upload (`/stores/me/products`)](#46-merchant-product-inventory--csv-upload-storesmeproducts)
   - [4.7 Public Marketplace & Discovery (`/marketplace`)](#47-public-marketplace--discovery-marketplace)
   - [4.8 Orders, Checkout & Status Workflow (`/orders`)](#48-orders-checkout--status-workflow-orders)
   - [4.9 Store Reviews & Customer Ratings (`/reviews`)](#49-store-reviews--customer-ratings-reviews)
   - [4.10 Notifications & SignalR (`/notifications`)](#410-notifications--signalr-notifications)
   - [4.11 Customer Support Tickets (`/support-tickets`)](#411-customer-support-tickets-support-tickets)
   - [4.12 Administrator Operations & Governance (`/admin`)](#412-administrator-operations--governance-admin)
5. [Meeting FAQ & Technical Cheat Sheet](#5-meeting-faq--technical-cheat-sheet)
6. [Current Features Available (What is Live & Working)](#6-current-features-available-what-is-live--working)
7. [Future Roadmap & Next Steps (What Will Be Done Next)](#7-future-roadmap--next-steps-what-will-be-done-next)

---

## 1. System Architecture & Tech Stack

FoodLoop is built on **.NET 10** following the **Clean Architecture** (Onion) pattern and **CQRS (Command Query Responsibility Segregation)** powered by **MediatR**.

```
┌────────────────────────────────────────────────────────┐
│                   FoodLoop.API (Presentation Layer)   │
│   • REST Controllers  • SignalR Hubs  • Swagger Filters│
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│              FoodLoop.Application Layer                │
│   • CQRS Commands & Queries  • DTOs  • Interfaces      │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                FoodLoop.Domain Layer                   │
│   • Core Entities  • Enums  • Value Objects            │
└───────────────────────────▲────────────────────────────┘
                            │
┌───────────────────────────┴────────────────────────────┐
│             FoodLoop.Infrastructure Layer              │
│   • EF Core + SQL Server  • Identity  • Cloudinary     │
│   • Realtime Notifications • Email Services (Brevo)    │
└────────────────────────────────────────────────────────┘
```

### Core Technologies:
* **Runtime / Framework**: .NET 10 (C# 13).
* **Database & ORM**: Microsoft SQL Server + Entity Framework Core 10 (Code-First Migrations, Soft Deletes, Global Query Filters).
* **Identity & Security**: ASP.NET Core Identity with JWT (JSON Web Tokens) and Refresh Token rotation.
* **Real-time Engine**: SignalR WebSockets for live in-app notifications and order alerts.
* **File & Media Storage**: Cloudinary integration for product images, organization logos, cover photos, and verification documents (PDF/images).
* **Email Service**: Brevo / SMTP with localized HTML email templates and web login redirection.
* **Logging & Observability**: Serilog structured logging (Console + rolling daily file logs).
* **Localization**: Bilingual support (English & Arabic) via `Accept-Language` headers and `ILocalizationService`.

---

## 2. Security, Roles & Authentication Flow

### User Roles (`AppRole`):
1. **`Admin`**: Platform governance, store verification, product moderation, dispute resolution, analytics, user management.
2. **`Merchant`**: Supermarkets, grocery chains, and bakeries listing surplus inventory, tracking orders, managing analytics, and auto-discounting.
3. **`Charity`**: Non-profit organizations (NGOs) receiving food surplus donations.
4. **`Customer`**: End-users browsing surplus deals, ordering food, writing reviews, and opening support tickets.

### Token Lifecycle:
1. **Access Token (JWT)**: Short-lived token containing claims (`sub`, `email`, `role`, `name`). Passed in HTTP header: `Authorization: Bearer <token>`.
2. **Refresh Token**: Long-lived secure string stored in database used via `POST /auth/refresh` to obtain new access tokens without requiring the user to re-enter credentials.
3. **Revocation**: Logging out via `POST /auth/logout` revokes the active refresh token.

### Organization Verification Flow:
```
[Merchant / Charity Registers]
         │ (Status: PendingReview)
         ▼
[Uploads Legal Documents (Commercial Registry / Tax ID / NGO Cert)]
         │
         ▼
[Admin Reviews Verification Queue in Admin Dashboard]
         ├── Approve ──► Status: Verified (Owner activated, can list products & receive orders)
         └── Reject  ──► Status: Rejected (Admin attaches review note for merchant to re-submit)
```

---

## 3. Core Business Workflows & AI Features

### A. Smart Expiry & Dynamic Discounting
* Merchants set dynamic discount triggers (`AiAutoDiscountEnabled`, `AiAutoDiscountPercent`, `AiAutoDiscountDaysBeforeExpiry`).
* When items approach expiry date (e.g. 2–3 days remaining), the system automatically applies price reductions to prevent food waste.
* Historical changes are recorded in `PriceHistories` for transparency.

### B. AI OCR Image Recognition
* Merchants upload photo packaging; the OCR engine extracts text, detected item name, and expiration date.
* If AI confidence is below threshold ($< 0.90$), it enters the Admin low-confidence review queue (`/admin/products/pending-ai`).

### C. Direct Charity Food Donations
* Merchants can donate unsold surplus stock directly to verified charities (`POST /stores/me/donations`).
* Tracks quantities, recipient NGO, and delivery statuses (`Delivered`).

### D. Real-Time WebSockets (SignalR)
* When order statuses change, donations are sent, or support replies are posted, SignalR pushes notifications directly to the user's active client app.

---

## 4. Comprehensive API Endpoint Reference

---

### 4.1 Authentication & Security (`/auth`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `POST` | `/auth/register` | Public | `RegisterRequest` (`name`, `email`, `password`, `role`, `phoneNumber`, `businessName`, etc.) | Registers new user account (Customer, Merchant, or Charity). Returns `201 Created` with user details. |
| `POST` | `/auth/login` | Public | `LoginRequest` (`email`, `password`) | Authenticates user credentials. Returns JWT Access Token, Refresh Token, expiration date, and user profile. |
| `POST` | `/auth/refresh` | Public | `RefreshTokenRequest` (`refreshToken`) | Validates refresh token and issues a new pair of access and refresh tokens. |
| `POST` | `/auth/logout` | Authorized | `LogoutRequest` (`refreshToken`) | Revokes the refresh token and invalidates active session. |
| `POST` | `/auth/forgot-password` | Public | `ForgotPasswordRequest` (`email`) | Generates secure password reset token and emails instructions to the user. |
| `POST` | `/auth/reset-password` | Public | `ResetPasswordRequest` (`email`, `token`, `newPassword`) | Validates reset token and sets new password. |
| `POST` | `/auth/resend-verification`| Public | `ResendVerificationRequest` (`email`) | Resends email account confirmation link. |

---

### 4.2 User Profiles & Delivery Addresses (`/users`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/users/me` | Customer/Any | None | Returns profile details of the logged-in user. |
| `PATCH`| `/users/me` | Customer/Any | `UpdateProfileRequest` (`fullName`, `phoneNumber`, `language`, `profileImage`) | Updates personal profile information and avatar photo. |
| `GET` | `/users/me/addresses` | Customer | None | Lists all saved delivery addresses for the customer. |
| `POST`| `/users/me/addresses` | Customer | `CreateAddressRequest` (`street`, `buildingNo`, `floor`, `city`, `district`, `latitude`, `longitude`, `isDefault`, `addressType`) | Creates a new delivery address. |
| `PATCH`| `/users/me/addresses/{id}`| Customer | `UpdateAddressRequest` | Updates an existing saved delivery address. |
| `DELETE`| `/users/me/addresses/{id}`| Customer | None | Deletes a saved address. |
| `POST`| `/users/me/tickets` | Customer | `CreateTicketRequest` (`category`, `message`, `priority`) | Creates a customer support ticket directly via user route. |
| `GET` | `/users/me/reports` | Customer | Query params (`page`, `pageSize`, `isResolved`) | Lists all product issue reports/disputes submitted by the customer with admin resolution notes. |
| `PATCH`| `/users/me/preferences` | Customer | `UpdatePreferencesRequest` (`orderUpdatesEnabled`, `marketingNotificationsEnabled`) | Updates push/email notification preferences. |
| `GET` | `/users` | Admin | Query params (`role`, `status`, `searchTerm`, `page`, `pageSize`) | Admin: Lists all platform users with filtering and pagination. |
| `GET` | `/users/{id}` | Admin | Route `id` (GUID) | Admin: Retrieves user account details by ID. |
| `POST`| `/users` | Admin | `CreateUserRequest` | Admin: Manually creates user account. |
| `PATCH`| `/users/{id}` | Admin | `UpdateUserRequest` | Admin: Updates user account details. |
| `DELETE`| `/users/{id}` | Admin | Route `id` (GUID) | Admin: Deactivates or removes a user. |

---

### 4.3 Stores & Merchants Management (`/stores`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/stores/me` | Merchant | None | Returns full profile of the merchant's store, operating hours, coordinates, and verification status. |
| `PATCH`| `/stores/me` | Merchant | `UpdateStoreProfileFormRequest` (`Name`, `BusinessCategory`, `CoverPhoto`, `Logo`, `OpeningHours`) | Updates store profile, cover banner, and operating hours via multipart form. |
| `PATCH`| `/stores/me/location` | Merchant | `UpdateStoreLocationRequest` (`latitude`, `longitude`, `governorate`, `city`, `neighborhood`, `street`, `buildingNo`) | Updates store geolocation and physical address. |
| `POST`| `/stores/me/documents` | Merchant | Multipart Form (`Email`, `Type`, `File`) | Uploads store verification documents (Commercial Registration, Tax ID, Store Photo). |
| `GET` | `/stores/me/orders` | Merchant | Query params (`status`, `page`, `pageSize`) | Retrieves orders received by this merchant store. |
| `PATCH`| `/stores/me/orders/{id}/status`| Merchant | `UpdateOrderStatusRequest` (`status`) | Updates order fulfillment status (`Confirmed`, `ReadyForPickup`, `Completed`, `Cancelled`). |
| `GET` | `/stores/me/analytics` | Merchant | Query param (`period`: `today`, `week`, `month`, `all`) | Returns merchant revenue metrics, surplus items saved, and total completed orders. |
| `GET` | `/stores/me/ai-settings` | Merchant | None | Gets current AI auto-discounting and auto-pricing settings. |
| `PATCH`| `/stores/me/ai-settings` | Merchant | `UpdateAiSettingsRequest` (`aiAutoDiscountEnabled`, `aiAutoDiscountPercent`, `aiAutoDiscountDaysBeforeExpiry`, `aiAutoPricingEnabled`) | Configures near-expiry auto discount rules. |
| `POST`| `/stores/me/donations` | Merchant | `CreateDonationRequest` (`recipientOrganizationId`, `productId`, `quantity`, `note`) | Creates a surplus food donation sent to a verified charity. |
| `GET` | `/stores/me/risk-analysis` | Merchant | None | Analyzes inventory risk for items expiring within 48-72 hours. |
| `GET` | `/stores/me/disputes` | Merchant | Query params (`page`, `pageSize`, `isResolved`) | Lists all customer reports and disputes filed on products in this store. |

---

### 4.4 Charities & NGOs (`/charities`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/charities` | Public | None | Lists all verified charities and non-profit organizations available for donations. |
| `GET` | `/charities/{id}` | Public | Route `id` (GUID) | Gets details of a specific charity. |
| `GET` | `/charities/me` | Charity | None | Returns profile and donation stats for the logged-in charity. |
| `PATCH`| `/charities/me` | Charity | Multipart Form (`Name`, `Description`, `Logo`, `CoverPhoto`, `OpeningHours`) | Updates charity profile details and cover photo. |
| `POST`| `/charities/me/documents` | Charity | Multipart Form (`Email`, `Type`, `File`) | Uploads charity verification documents (Association Certificate, Bylaws, Board List). |
| `GET` | `/charities/me/donations` | Charity | Query params (`page`, `pageSize`) | Lists surplus food donations received by this charity. |

---

### 4.5 Product Categories (`/categories`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/categories` | Public | None | Returns all active bilingual food categories (Bakery, Dairy, Meat, Fruits, etc.) with icons. |
| `GET` | `/categories/{id}` | Public | Route `id` (GUID) | Returns a specific category by ID. |

---

### 4.6 Merchant Product Inventory & CSV Upload (`/stores/me/products`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/stores/me/products` | Merchant | Query params (`categoryId`, `status`, `searchTerm`, `page`, `pageSize`) | Lists all products belonging to the merchant's store. |
| `GET` | `/stores/me/products/{id}` | Merchant | Route `id` (GUID) | Returns complete details of a specific product listing. |
| `POST`| `/stores/me/products` | Merchant | `CreateProductRequest` (`categoryId`, `title`, `description`, `originalPrice`, `discountedPrice`, `quantityAvailable`, `expirationDate`) | Adds a new surplus product item to the store inventory. |
| `PATCH`| `/stores/me/products/{id}` | Merchant | Multipart Form (`UpdateProductRequest`: price, stock, status, expiration) | Updates pricing, stock count, status, or details of an existing product. |
| `DELETE`| `/stores/me/products/{id}`| Merchant | Route `id` (GUID) | Soft-deletes a product listing. |
| `POST`| `/stores/me/products/{id}/images` | Merchant | Multipart Form (`File`) | Uploads a high-res display photo for the product. |
| `DELETE`| `/stores/me/products/{id}/images/{imageId}` | Merchant | Route GUIDs | Removes a product image. |
| `POST`| `/stores/me/products/bulk` | Merchant | Multipart Form (`File` - CSV) | **Bulk Import**: Imports multiple products at once from CSV file with column validation. |
| `PATCH`| `/stores/me/products/{id}/discount` | Merchant | `ApplyDiscountRequest` (`discountedPrice`, `reason`) | Manually applies a custom discount and logs into price history audit. |
| `GET` | `/stores/me/products/{id}/price-history` | Merchant | Route `id` (GUID) | Returns audit trail of all price adjustments for this product. |
| `POST`| `/stores/me/products/{id}/ocr` | Merchant | Multipart Form (`File`) | Submits product packaging photo for AI OCR expiration analysis. |
| `GET` | `/stores/me/products/{id}/ocr` | Merchant | Route `id` (GUID) | Polls AI OCR scan results, extracted expiry date, and confidence score. |

---

### 4.7 Public Marketplace & Discovery (`/marketplace`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/marketplace/products` | Public | Query params (`categoryId`, `search`, `sortBy`, `latitude`, `longitude`, `radiusKm`, `page`, `pageSize`) | Main customer discovery feed: returns surplus food deals with geospatial distance calculation, filtering, and sorting (`discount`, `expiry`, `price`). |
| `GET` | `/marketplace/products/{id}` | Public | Route `id` (GUID) | Retrieves full public product detail page including store info, savings, and expiry. |
| `POST`| `/marketplace/products/{id}/report`| Customer | `ReportProductRequest` (`reason`, `details`) | **Dispute / Report**: Customers flag problematic or misleading product listings for admin moderation. |
| `GET` | `/marketplace/favorites` | Customer | Query params (`page`, `pageSize`) | Returns customer's bookmarked favorite products. |
| `POST`| `/marketplace/favorites/{productId}`| Customer | Route `productId` | Adds a product to user favorites. |
| `DELETE`| `/marketplace/favorites/{productId}`| Customer | Route `productId` | Removes a product from favorites. |

---

### 4.8 Orders, Checkout & Status Workflow (`/orders`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `POST`| `/orders/checkout` | Customer | `CheckoutRequest` (`items`: `productId`, `quantity`, `notes`) | Validates stock, calculates discounts, reserves inventory, and creates order. Returns order ID and payment summary. |
| `GET` | `/orders` | Customer | Query params (`page`, `pageSize`) | Returns customer's order history and statuses. |
| `GET` | `/orders/{id}` | Customer/Merchant | Route `id` (GUID) | Returns full order details with item breakdown, store address, and payment status. |
| `GET` | `/orders/{id}/tracking` | Customer | Route `id` (GUID) | Returns live step-by-step progress tracking for the order (`Pending` $\rightarrow$ `Confirmed` $\rightarrow$ `ReadyForPickup` $\rightarrow$ `Completed`). |

---

### 4.9 Store Reviews & Customer Ratings (`/reviews`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `POST`| `/reviews` | Customer | `CreateReviewRequest` (`orderId`, `rating` [1-5], `comment`) | Submits store rating and feedback for a completed order. Updates store average rating automatically. |
| `GET` | `/stores/{storeId}/reviews` | Public | Route `storeId` + Query params | Lists customer reviews and ratings for a specific store. |

---

### 4.10 Notifications & SignalR (`/notifications`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/notifications` | Authorized | Query params (`page`, `pageSize`) | Retrieves user's notification feed (deals, order alerts, status updates). |
| `PATCH`| `/notifications/{id}/read` | Authorized | Route `id` (GUID) | Marks a single notification as read. |
| `PATCH`| `/notifications/read-all` | Authorized | None | Marks all notifications as read. |
| `WS`  | `/hubs/notifications` | Authorized | WebSocket connection | **SignalR Real-time Hub**: Pushes instant live alerts and messages to connected web and mobile clients. |

---

### 4.11 Customer Support Tickets (`/support-tickets`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `POST`| `/support-tickets` | Authorized | `CreateSupportTicketRequest` (`category`, `message`, `priority`) | Opens a new support ticket for helpdesk assistance. |
| `GET` | `/support-tickets` | Authorized | Query params (`page`, `pageSize`) | Lists user's submitted support tickets. |
| `GET` | `/support-tickets/{id}` | Authorized | Route `id` (GUID) | Retrieves ticket details and full conversation history. |
| `POST`| `/support-tickets/{id}/reply` | Authorized | `ReplySupportTicketRequest` (`message`) | Posts a follow-up reply message in the ticket thread. |

---

### 4.12 Administrator Operations & Governance (`/admin`)

| Method | Route | Access | Request / Body | Responsibility & Description |
| :--- | :--- | :---: | :--- | :--- |
| `GET` | `/admin/analytics/summary` | Admin | None | Platform-wide KPIs: total users, active stores, surplus food saved (kg/EGP), orders, and revenue. |
| `GET` | `/admin/stores/pending` | Admin (AllowAnon for review) | None | Lists onboarding stores waiting for admin verification review. |
| `GET` | `/admin/stores/{id}` | Admin (AllowAnon for review) | Route `id` (GUID) | Gets store profile and uploaded legal documents for verification. |
| `PATCH`| `/admin/stores/{id}/verify` | Admin | `VerifyOrganizationRequest` (`action`: `Approved`/`Rejected`, `note`) | Approves or rejects a merchant store onboarding. |
| `PATCH`| `/admin/charities/{id}/verify` | Admin | `VerifyOrganizationRequest` (`action`: `Approved`/`Rejected`, `note`) | Approves or rejects a charity organization onboarding. |
| `GET` | `/admin/stores` | Admin | Query params (`status`, `page`, `pageSize`) | Lists all platform stores with verification status filter. |
| `GET` | `/admin/charities` | Admin | None | Lists all charity organizations. |
| `GET` | `/admin/products` | Admin | Query params (`status`, `organizationId`, `page`, `pageSize`) | Lists platform inventory for content moderation. |
| `GET` | `/admin/products/pending-ai` | Admin | Query param (`confidenceThreshold`) | Lists low-confidence AI OCR products requiring human review. |
| `PATCH`| `/admin/products/{id}/approve`| Admin | Route `id` | Approves a moderated product listing. |
| `PATCH`| `/admin/products/{id}/reject` | Admin | `ProductModerationRequest` (`note`) | Rejects a product listing with a reason note. |
| `PATCH`| `/admin/products/{id}/request-changes`| Admin | `ProductModerationRequest` (`note`) | Flags product and requests merchant to adjust price/details. |
| `DELETE`| `/admin/products/{id}` | Admin | Route `id` | Soft-deletes a product listing directly. |
| `GET` | `/admin/reviews` | Admin | Query params (`organizationId`, `rating`, `page`, `pageSize`) | Lists store reviews with moderation controls. |
| `DELETE`| `/admin/reviews/{id}` | Admin | Route `id` | Removes abusive or fake customer reviews. |
| `GET` | `/admin/disputes` | Admin | Query params (`isResolved`, `page`, `pageSize`) | **Disputes Queue**: Lists user-submitted product reports. |
| `GET` | `/admin/disputes/{id}` | Admin | Route `id` (GUID) | Gets detailed dispute report by ID. |
| `PATCH`| `/admin/disputes/{id}/resolve`| Admin | `ResolveDisputeRequest` (`adminNote`) | Resolves a product dispute. |
| `GET` | `/admin/support-tickets` | Admin | Query params (`status`, `priority`, `page`, `pageSize`) | Lists all user support tickets across the platform. |
| `GET` | `/admin/support-tickets/{id}`| Admin | Route `id` | Retrieves full support ticket conversation thread. |
| `POST`| `/admin/support-tickets/{id}/reply`| Admin | String / JSON body (`message`) | Admin sends official reply message to the user. |
| `PATCH`| `/admin/support-tickets/{id}/close`| Admin | Route `id` | Closes and resolves a support ticket. |
| `PATCH`| `/admin/users/{id}/status` | Admin | `UpdateUserStatusRequest` (`status`: `Active`, `Suspended`, `Banned`) | Bans, suspends, or reactivates a user account. |
| `GET` | `/admin/users/{id}/activity-log`| Admin | Route `id` | Retrieves complete audit trail log for a user. |
| `GET` | `/admin/stores/{id}/activity-log`| Admin | Route `id` | Retrieves complete audit trail log for a store. |
| `GET` | `/admin/charities/{id}/activity-log`| Admin | Route `id` | Retrieves audit trail log for a charity. |

---

## 5. Meeting FAQ & Technical Cheat Sheet

### Q1: What is the architectural difference between Disputes and Support Tickets?
* **Dispute (`ProductReport`)**: Product-level issue submitted by a customer against a specific marketplace product (misleading info, wrong expiry date, damaged item). It is reviewed by Admin to take action on the product.
* **Support Ticket (`SupportTicket`)**: General helpdesk issue submitted by a user for assistance (order problems, refunds, account issues). It features a **two-way messaging thread** (`TicketMessage`) between the user and support.

### Q2: How does the Bulk CSV Import work?
* Merchants call `POST /stores/me/products/bulk` with `multipart/form-data`.
* The server validates the required headers (`title`, `originalPrice`, `discountedPrice`, `quantityAvailable`, `expirationDate`, `categoryName`) and checks that prices and dates are valid before inserting the products in a single database transaction.

### Q3: How is data consistency and soft deletion handled?
* Entities implement `BaseEntity` with `IsDeleted`, `DeletedAt`, and `DeletedBy`.
* EF Core global query filters automatically ignore soft-deleted items across all queries while preserving historical data for orders and price audits.

### Q4: How is real-time notification implemented?
* Implemented via **SignalR** (`/hubs/notifications`). When an event occurs (e.g. order placed, donation delivered, or ticket reply posted), `IRealTimeNotificationService` publishes message payloads to the recipient user's WebSocket channel.

### Q5: How do we test and verify all backend endpoints?
* Integration test suite:
  ```bash
  bash api_tests.sh
  ```
  Runs **260 automated assertions** across all 42 modules with 100% pass rate.
* Database management:
  ```bash
  bash scripts/seed_db.sh    # Resets and populates 41 users and 70 products
  bash scripts/reset_db.sh   # Clean table wipe
  dotnet run --project src/FoodLoop.DbTool -- --verify  # Verifies live DB data
  ```

---

## 6. Current Features Available (What is Live & Working)

The backend platform is fully developed, tested, and live with the following capabilities:

### 1. 🔐 Security & Identity Management
* **Multi-Role JWT Authentication**: Role-based authorization (`Admin`, `Merchant`, `Charity`, `Customer`).
* **Refresh Token Rotation & Revocation**: Seamless re-authentication without user interruptions.
* **Password Management**: Localized forgot/reset password email workflows.
* **Account Status & Governance**: Admin ability to ban, suspend, or reactivate users.

### 2. 🏪 Merchant & Store Operations
* **Store Onboarding & Verification**: Document uploads (`CommercialRegistration`, `TaxIdCertificate`, `StoreFacilityPhoto`) with Admin review/rejection/approval cycle.
* **Store Profile & Visuals**: Geolocation coordinates, operating hours JSON, store logos, and cover photo banners.
* **Analytics Engine**: Real-time sales, order counts, and surplus food saved over customizable periods (`today`, `week`, `month`, `all`).
* **Inventory Risk Analysis**: Automatic risk assessment report for products expiring in 48–72 hours.

### 3. 📦 Smart Inventory & Catalog
* **Product Catalog**: Multi-image product listings with original and discounted pricing, categories, and expiry dates.
* **Bulk CSV Import**: High-performance batch product upload (`POST /stores/me/products/bulk`) with header validation (EN/AR).
* **AI OCR Packaging Scanner**: Photo scanning for expiration date extraction with confidence scoring.
* **Smart Dynamic Discounting**: Automated price reductions when expiration thresholds are reached.
* **Price History Audit Trail**: Automated logging of every price change with reasons and timestamps.
* **Soft Delete Protection**: `ISoftDelete` interceptors and EF Core global query filters protecting order history.

### 4. 🛒 Consumer Marketplace & Discovery
* **Geospatial Proximity Search**: Nearby surplus product discovery with Haversine distance calculations and radius filtering.
* **Search & Sorting**: Filtering by category, keyword search, and multi-criteria sorting (highest discount, nearest expiry, price).
* **Favorites & Bookmarks**: Saving preferred products for quick access.
* **Product Disputes / Flagging**: In-app reporting of misleading or damaged products (`POST /marketplace/products/{id}/report`).

### 5. 💳 Orders & Fulfillment Lifecycle
* **Checkout & Stock Reservation**: Multi-item cart checkout with immediate stock validation.
* **Multi-Stage Order Tracking**: Step-by-step progress (`Pending` $\rightarrow$ `Confirmed` $\rightarrow$ `ReadyForPickup` $\rightarrow$ `Completed`).
* **Payment Record Tracking**: Integration-ready payment tracking with transaction references (`TXN_xxxxxxx`).

### 6. 🤝 Charity & Community Impact
* **Charity Onboarding**: NGO legal registration (`AssociationCertificate`, `Bylaws`, `BoardList`) and verification.
* **Surplus Donations**: Direct merchant-to-charity donation management with quantity and delivery status tracking.

### 7. 💬 Customer Support & Review System
* **Two-Way Support Tickets**: Helpdesk messaging threads with priority levels and statuses (`Open`, `Pending`, `Resolved`, `Closed`).
* **Store Ratings & Reviews**: Order-verified customer reviews with automatic rolling store rating calculations.
* **System Audit Logs**: Platform-wide security audit logging.

### 8. ⚡ Real-Time & Communications
* **SignalR WebSockets**: Instant live push notifications for order updates, donations, and support replies.
* **Firebase Cloud Messaging (FCM)**: Mobile background push notifications (`POST /notifications/device-token`).
* **Localized Email Service**: HTML email notifications (Welcome, Approval, Rejection, Password Reset) with web login links.
* **100% Automated Test Suite**: 490 automated tests passing across Domain, Application, and Infrastructure layers.

### 9. 🤖 AI Microservice & Intelligent Pricing
* **Dual LangGraph Agents**: Monitoring Agent (inventory risk & holiday/weather tool routing) + Pricing Agent (0–15% markdown optimization).
* **RAG Vector Knowledge**: Qdrant vector store indexing historical pricing episodes using BGE-M3 1024-d multilingual embeddings.
* **Safety Margin Shield**: Price floor validation protecting merchant costs.
* **Payment Processing**: Paymob unified checkout (Card & Wallets) with callbacks and customer wallet management.

---

## 7. Future Roadmap & Next Steps

The following enhancements are planned for subsequent phases of the platform:

### 1. 🚚 Dedicated Delivery Fleet & Driver App API
* **Driver Role & Dispatch System**: Dedicated endpoints for third-party or in-house delivery couriers.
* **Live GPS Route Tracking**: Real-time driver location streaming to customer apps during active order delivery.
* **Proof of Delivery**: Digital signatures or OTP confirmation codes upon package handover.

### 2. 🏢 Enterprise ERP & POS Synchronization
* **Retail POS Connectors**: Direct sync connectors and webhooks for supermarket ERP systems (Odoo, SAP, Microsoft Dynamics, Symphony) to sync expiring inventory automatically without manual CSV uploads.

### 3. 🏆 Consumer Eco-Impact Gamification
* **Carbon / Food Waste Badges**: Tracking customer total kilograms of food saved and estimated CO₂ footprint reductions ("Eco Hero", "Zero Waste Champion").
* **Loyalty Points & Rewards**: Earning points on surplus purchases redeemable for bonus discounts.

### 4. 🥗 Charity Beneficiary & Distribution Logs
* **Charity Distribution Records**: Inner-NGO management tools to record which families/charity kitchens received specific donated surplus food batches.


