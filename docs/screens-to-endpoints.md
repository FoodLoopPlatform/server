# FoodLoop — Screens to Backend Endpoints

Every screen in `/Screens` mapped to its implemented backend endpoint(s).
All endpoints are live at `https://foodloop.runasp.net`.

---

## Auth & Onboarding

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `welcome_to_foodloop` | — | — | — | Static splash screen, no API call |
| `create_account_account_type_selection` | POST | `/auth/register` | Public | role = Customer \| Merchant \| Charity |
| `login` | POST | `/auth/login` | Public | Returns accessToken + refreshToken |
| `email_verification` | POST | `/auth/resend-verification` | Public | Re-sends verification email |
| `forgot_password_request_link` | POST | `/auth/forgot-password` | Public | Always 200 to prevent enumeration |
| `reset_password_new_password` | POST | `/auth/reset-password` | Public | Requires token from forgot-password |
| `business_signup_step_1` | POST | `/auth/register` | Public | role = Merchant or Charity |
| `business_verification_location` | PATCH | `/stores/me/location` | Merchant | Sets city/neighborhood/lat/lng |
| `document_upload_step_2` | POST | `/stores/me/documents` | Public (email-identified) | type = CommercialRegistration \| TaxIdCertificate \| StoreFacilityPhoto |
| | POST | `/charities/me/documents` | Public (email-identified) | type = AssociationCertificate \| CharityBylaws \| BoardOfDirectorsList |
| `verification_pending_step_3` | GET | `/stores/me` | Merchant | Returns current verificationStatus |

---

## Marketplace & Customer

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `home_marketplace` | GET | `/marketplace/products` | Public | Supports lat/lng/maxDistance/category/price/search/sortBy |
| | GET | `/categories` | Public | Returns all product categories |
| `search_results` | GET | `/marketplace/products?search=...` | Public | Full-text search on title + description |
| `search_no_results` | GET | `/marketplace/products?search=...` | Public | Empty array response |
| `search_network_error` | GET | `/marketplace/products` | Public | Network failure state (client-side) |
| `product_details` | GET | `/marketplace/products/{id}` | Public | Single active product detail |
| `checkout_order_review` | POST | `/orders` | Customer | Body: `{ items: [{ productId, quantity }] }` |
| | POST | `/orders/{id}/paymob-checkout` | Customer | Generates Paymob Unified Checkout URL |
| | POST | `/orders/{id}/verify-payment` | Customer | Verifies/syncs Paymob payment status after WebView completion |
| | POST | `/orders/{id}/wallet-checkout` | Customer | Charges order total to customer wallet balance |
| | POST | `/payments/paymob-callback` | Public | Paymob transaction webhook listener |
| | GET | `/payments/paymob-callback` | Public | Paymob transaction redirect handler (with auto transaction API fallback) |
| | POST | `/payments/verify/{orderId}` | Public | Public payment verification & sync endpoint |
| `order_success` | GET | `/orders/{id}` | Customer | Returns placed order detail |
| `order_tracking` | GET | `/orders/{id}/tracking` | Customer | Pipeline steps + store info |
| `rate_your_experience` | POST | `/reviews` | Customer | Body: `{ orderId, rating, comment }` |
| `rating_bottom_sheet_overlay` | POST | `/reviews` | Customer | Same as above |
| `store_reputation` | GET | `/stores/{id}/reviews` | Public | Paginated store reviews |
| `report_an_issue` | POST | `/marketplace/products/{id}/report` | Customer | Mandatory `imageUrl`, reason = MisleadingInfo \| WrongExpiry \| Expired \| Spam \| Inappropriate \| Other |
| `report_issue_bottom_sheet_overlay` | POST | `/marketplace/products/{id}/report` | Customer | Same as above |

---

## User Profile & Wallet

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `profile_settings` | GET | `/users/me` | Any | Returns current user |
| | PATCH | `/users/me` | Any | Updates fullName / language / phone |
| | PATCH | `/users/me/preferences` | Any | orderUpdatesEnabled / marketingNotificationsEnabled |
| `user_wallet_overview` | GET | `/users/me/wallet` | Any | Returns wallet balance & transaction history |
| `add_address` | POST | `/users/me/addresses` | Any | Creates a delivery address |
| `location_delivery_settings` | GET | `/users/me/addresses` | Any | Lists all addresses |
| | PATCH | `/users/me/addresses/{id}` | Any | Updates address |
| | DELETE | `/users/me/addresses/{id}` | Any | Removes address |
| `help_center` | GET | `/support-tickets` | Any | Lists caller's tickets |
| `create_support_ticket` | POST | `/support-tickets` | Any | Opens a ticket |
| | POST | `/users/me/tickets` | Any | Alias via UsersController |
| `ticket_details_chat` | GET | `/support-tickets/{id}` | Any | Full ticket + message history |
| | POST | `/support-tickets/{id}/reply` | Any | Adds a reply to the conversation |

---

## Merchant — Store Management

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `store_profile_settings` | GET | `/stores/me` | Merchant | Full profile + documents + location |
| | PATCH | `/stores/me` | Merchant | Multipart form — name/description/logo/phone/openingHours |

---

## Merchant — Inventory

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `inventory_management_list_view` | GET | `/stores/me/products` | Merchant | Filters: status / categoryId / searchTerm / pageNumber / pageSize |
| `add_product_basic_info` | POST | `/stores/me/products` | Merchant | Creates a product |
| `add_product_manual_entry` | POST | `/stores/me/products` | Merchant | Same endpoint |
| `add_product_expiration_settings` | POST | `/stores/me/products` | Merchant | Same endpoint |
| `add_product_verification_summary` | GET | `/stores/me/products/{id}` | Merchant | Returns created product detail |
| `inventory_bulk_upload_utility` | POST | `/stores/me/products/bulk` | Merchant | Multipart CSV upload |
| `inventory_risk_analysis` | GET | `/stores/me/products/risk-analysis` | Merchant | Groups products by Critical/High/Medium/Low expiry risk |
| `inventory_risk_analysis_arabic_rtl` | GET | `/stores/me/products/risk-analysis` | Merchant | Same endpoint, RTL variant |

---

## Merchant — Pricing & Discounts

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `pricing_dashboard_overview` | GET | `/stores/me/products/pricing` | Merchant | Summary metrics + active product price list |
| `smart_discount_manager` | PATCH | `/stores/me/products/{id}/discount` | Merchant | Body: `{ discountedPrice, changeReason }` |
| `price_history_audit` | GET | `/stores/me/products/{id}/price-history` | Merchant | Ordered list of every price change |
| `price_history_empty_state` | GET | `/stores/me/products/{id}/price-history` | Merchant | Empty array response |

---

## Merchant — Orders & Logistics

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `orders_management_dashboard` | GET | `/stores/me/orders` | Merchant | All orders received by this store |
| `order_details_fulfillment_control` | PATCH | `/stores/me/orders/{id}/status` | Merchant | status = Confirmed \| Preparing \| ReadyForPickup \| Completed \| Cancelled |
| `logistics_hub_delivery_fleet_overview` | GET | `/stores/me/delivery/fleet` | Merchant | Active orders grouped by status |
| `real_time_logistics_map` | GET | `/stores/me/delivery/fleet` | Merchant | Same endpoint — client renders map from coordinates |

---

## Merchant — Analytics & AI

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `merchant_insights_dashboard` | GET | `/stores/me/analytics` | Merchant | Query param: `?period=today\|week\|month\|all` |
| `ai_automation_settings` | GET | `/stores/me/ai-settings` | Merchant | Reads AI auto-discount preferences |
| | PATCH | `/stores/me/ai-settings` | Merchant | Updates thresholds and enable flags |
| `ocr_verification_loading` | POST | `/stores/me/products/{id}/ocr` | Merchant | Submits product image for AI/OCR scan |
| | GET | `/stores/me/products/{id}/ocr-result` | Merchant | Polls latest OCR result |

---

## Merchant — Donations

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `donation_community_impact` | GET | `/charities` | Public | Lists all verified charities |
| | POST | `/stores/me/donations` | Merchant | Donates surplus inventory to a charity |

---

## Admin

| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| `admin_users_list` | GET | `/admin/users` | Admin | Filters: role / status / searchTerm |
| `admin_user_detail_view` | GET | `/admin/users/{id}` | Admin | Full user profile |
| | PATCH | `/admin/users/{id}/status` | Admin | status = Active \| Suspended \| Banned |
| `audit_log_activity_history` | GET | `/admin/users/{id}/activity-log` | Admin | Recent events for a user |
| | GET | `/admin/stores/{id}/activity-log` | Admin | Recent events for a store |
| | GET | `/admin/charities/{id}/activity-log` | Admin | Recent events for a charity |
| `audit_log_no_results_state` | GET | `/admin/users/{id}/activity-log` | Admin | Empty array response |
| `platform_analytics_dashboard` | GET | `/admin/analytics/summary` | Admin | Total users / stores / orders / savings |
| `ai_pricing_recommendation_review` | GET | `/stores/me/ai-recommendations` | Merchant | Lists pending AI pricing proposals for merchant store with full pricing numbers |
| | GET | `/stores/me/ai-recommendations/schedule` | Merchant | Returns next scheduled pricing cycle timestamp and store automation mode |
| | POST | `/stores/me/ai-recommendations/{id}/approve` | Merchant | Approves AI price cut recommendation |
| | POST | `/stores/me/ai-recommendations/{id}/reject` | Merchant | Rejects AI price cut with reason |
| | PATCH | `/admin/products/{id}/approve` | Admin | Approves a pending product |
| | PATCH | `/admin/products/{id}/reject` | Admin | Rejects with a note |
| | GET | `/admin/ai-status` | Admin | Returns real-time execution status and next run times for all AI cycles |
| `platform_settings_and_rules` | GET | `/admin/system-settings` | Admin | Fetches global platform rules & defaults |
| | POST | `/admin/system-settings` | Admin | Updates platform rules. `defaultPriceFloorPolicy`: `DynamicAi` \| `Fixed30Percent` \| `Fixed50Percent`; `newBusinessDefaultAutomationMode`: `Manual` \| `Assisted` \| `Autonomous` |
| `dispute_handling_resolution` | GET | `/admin/disputes` | Admin | Filters: isResolved / pageNumber / pageSize |
| | PATCH | `/admin/disputes/{id}/resolve` | Admin | Marks dispute resolved with admin note |

---

## Notifications
 
| Screen | Method | Endpoint | Auth | Notes |
|---|---|---|---|---|
| *(notification bell / feed)* | GET | `/notifications` | Any | Lists caller's notifications with pagination |
| *(notification details)* | GET | `/notifications/{id}` | Any | Detailed single notification record |
| *(unread badge counter)* | GET | `/notifications/unread-count` | Any | Returns unread notification count |
| | PATCH | `/notifications/{id}/read` | Any | Marks one notification as read |
| | PATCH | `/notifications/read-all` | Any | Marks all notifications as read |
| | POST | `/notifications/device-token` | Any | Registers/updates mobile FCM device token |

---

## Shared Utility Endpoints (no dedicated screen)

These are used by multiple screens or as part of flows above.

| Endpoint | Method | Auth | Used By |
|---|---|---|---|
| `GET /` | GET | Public | Root welcome check |
| `GET /health` | GET | Public | Health check |
| `POST /auth/refresh` | POST | Public | Token refresh (any authenticated screen) |
| `POST /auth/logout` | POST | Public | Session termination |
| `GET /categories` | GET | Public | Marketplace, product creation |
| `GET /stores/{id}/reviews` | GET | Public | `store_reputation` |
| `DELETE /stores/me/products/{id}` | DELETE | Merchant | Inventory management |
| `PATCH /stores/me/products/{id}` | PATCH | Merchant | Product editing |
| `POST /stores/me/products/{id}/images` | POST | Merchant | Product image upload |
| `DELETE /stores/me/products/{id}/images/{imageId}` | DELETE | Merchant | Product image removal |
| `GET /admin/stores/pending` | GET | Public | Admin verification queue |
| `GET /admin/stores/{id}` | GET | Public | Admin store detail view |
| `PATCH /admin/stores/{id}/verify` | PATCH | Admin | Approve/reject store |
| `PATCH /admin/charities/{id}/verify` | PATCH | Admin | Approve/reject charity |
| `GET /admin/stores` | GET | Admin | Admin store list with filters |
| `GET /admin/charities` | GET | Admin | Admin charity list with filters |
| `GET /admin/reviews` | GET | Admin | Admin review moderation |
| `DELETE /admin/reviews/{id}` | DELETE | Admin | Remove inappropriate review |
| `GET /admin/products` | GET | Admin | Admin product list with filters |
| `POST /admin/products/extend-expiration` | POST | Admin / Testing | Bulk extend product expiration dates and reactivate expired items |
| `DELETE /admin/products/{id}` | DELETE | Admin | Admin soft-delete product |
| `GET /admin/support-tickets` | GET | Admin | Admin ticket queue |
| `GET /admin/support-tickets/{id}` | GET | Admin | Admin ticket detail |
| `POST /admin/support-tickets/{id}/reply` | POST | Admin | Admin reply to ticket |
| `PATCH /admin/support-tickets/{id}/close` | PATCH | Admin | Close/resolve ticket |
| `POST /users` | POST | Admin | Direct user creation |
| `GET /users` | GET | Admin | Admin user list |
| `GET /users/{id}` | GET | Admin | Admin get user by ID |
| `PATCH /users/{id}` | PATCH | Admin | Admin update user |
| `DELETE /users/{id}` | DELETE | Admin | Admin delete user |
| `PATCH /admin/users/{id}/status` | PATCH | Admin | Suspend/ban/reactivate user |

---

## Summary

| Category | Screens | Endpoints |
|---|---|---|
| Auth & Onboarding | 10 | 9 |
| Marketplace & Customer | 14 | 8 |
| User Profile | 6 | 10 |
| Merchant — Store | 2 | 2 |
| Merchant — Inventory | 8 | 6 |
| Merchant — Pricing & Discounts | 4 | 3 |
| Merchant — Orders & Logistics | 4 | 3 |
| Merchant — Analytics & AI | 4 | 5 |
| Merchant — Donations | 1 | 2 |
| Admin | 10 | 13 |
| Notifications | — | 3 |
| **Total** | **59 screens** | **57 primary endpoints** |

> `welcome_to_foodloop` is the only screen with no backend endpoint — it is a static onboarding splash.
