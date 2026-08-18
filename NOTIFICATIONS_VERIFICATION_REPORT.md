# FoodLoop Notification System End-to-End Verification Report

This report presents the end-to-end investigation findings, architectural improvements, and test coverage status for the FoodLoop Notification System (SignalR Real-Time Hub + Firebase Cloud Messaging Push Subsystem).

---

## 1. Investigation Findings (Part 1)

| No. | Feature / Checklist Item | Status | File References / Description |
| :--- | :--- | :--- | :--- |
| **1** | SignalR Authentication & Route Mapping | **Confirmed Working** | [`NotificationHub.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Hubs/NotificationHub.cs) is decorated with `[Authorize]`. Connection tokens are securely read from query strings on hub connection handshakes via JwtBearerEvents inside [`InfrastructureServiceRegistration.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs#L100-L113). |
| **2** | Complete Business Events Mapping | **Confirmed Working / Missing Triggers Logged** | Active events in code: Support Ticket Reply, Admin Note Sent, Order Placed (Consumer Side), Order Received (Merchant Side), Order Status Updated. Missing events in code (not implemented): AI Recommendations staged/executed, and Donation events. |
| **3** | Success-Path Reachability | **Confirmed Working** | Call sites are correctly positioned in the success branches of command handlers (e.g., [`CreateOrderCommandHandler.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Features/Orders/Commands/CreateOrderCommandHandler.cs#L127) and [`UpdateOrderStatusCommandHandler.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Features/Orders/Commands/UpdateOrderStatusCommandHandler.cs#L97)) following DB flushes. |
| **4** | Offline / Disconnected Client Handling | **Bug Found (Fixed)** | SignalR `Clients.User` proxy does not throw on offline clients, but any unexpected dispatch failures in the hybrid delivery chain bubbled up and crashed the underlying transaction. |
| **5** | Expired/Invalid FCM Token Handling | **Bug Found (Fixed)** | When Firebase returned `Unregistered` or `InvalidArgument` exception errors for dead device tokens, the backend did not clean them up, causing dead tokens to poll resources indefinitely. |
| **6** | Multi-Tenant Isolation / Leakage Risk | **Bug Found (Fixed)** | When a device token was registered by a new user, previous mappings in the `UserDeviceTokens` table remained active, leading to potential cross-tenant leakage. |
| **7** | Notification Failure Isolation | **Bug Found (Fixed)** | Prior to the fix, SignalR/Firebase dispatches were not wrapped in try-catch blocks in [`RealTimeNotificationService.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Services/RealTimeNotificationService.cs), violating the principle of failure isolation for non-critical dependencies. |
| **8** | Duplicate-Send Risk | **Confirmed Working** | HashSet collections are used to prevent duplicate merchant dispatches for multi-item orders. There are no duplicate pipeline retry triggers at the MediatR layer. |

---

## 2. Fixes Applied (Part 2)

### Bug 1: Non-Critical Notification Delivery Failure Cascading (Failure Isolation)
*   **Fix:** Wrapped the SignalR user group dispatch and the Firebase push notification dispatch in independent `try-catch` blocks inside [`RealTimeNotificationService.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Services/RealTimeNotificationService.cs#L51-L75). Added `ILogger` injection to log failures for alerting/debugging.
*   **Impact:** A broken SignalR connection or Firebase API timeout will never cause core business operations (like completing an order or saving replies) to fail or roll back.

### Bug 2: Expired/Invalid FCM Token DB Pollution
*   **Fix:** Updated the exception handler in [`FirebasePushNotificationService.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Services/FirebasePushNotificationService.cs#L89-L108) to catch `FirebaseMessagingException`. If the error corresponds to `Unregistered` or `InvalidArgument` (stale/dead token), the token is immediately marked as `IsActive = false` in the database.
*   **Impact:** Wasted resource allocation and FCM network calls are avoided.

### Bug 3: Cross-Tenant Token Leakage
*   **Fix:** Updated `UpsertAsync` in [`UserDeviceTokenService.cs`](file:///c:/ITI/server/src/FoodLoop.Infrastructure/Services/UserDeviceTokenService.cs#L28-L38). When a user registers a token, any other database records containing the same token but associated with a different user ID are flagged as `IsActive = false`.
*   **Impact:** Ensures that push notifications are strictly single-tenant targeted even when multiple users share the same physical phone over time.

---

## 3. Test Suite Breakdown (Part 3)

We have added comprehensive test coverage inside [`NotificationSystemTests.cs`](file:///c:/ITI/server/test/FoodLoop.Infrastructure.Tests/Features/Notifications/NotificationSystemTests.cs):

### Unit & Logic Verification Tests
*   `Hub_connection_should_require_authentication` — **PASSED**
    Asserts that the `NotificationHub` is decorated with the `[Authorize]` attribute to reject unauthenticated WebSocket connections.
*   `Device_token_registration_endpoint_should_require_authentication` — **PASSED**
    Asserts that the `NotificationsController` is decorated with `[Authorize]`.
*   `Device_token_registration_should_deactivate_duplicates_for_other_users` — **PASSED**
    Ensures that registering a token for a new user automatically deactivates duplicate entries for previous users.
*   `Notification_failures_in_signalr_or_firebase_should_not_block_parent_transaction` — **PASSED**
    Asserts that even if SignalR and Firebase throw exceptions, the notification is still successfully saved to the database and no error propagates to the caller.
*   `SendNotification_to_offline_user_should_not_throw_exception` — **PASSED**
    Verifies that sending to an offline SignalR client completes without errors.

### Integration/Trigger Verification Tests
*   `ReplyToSupportTicket_should_dispatch_exactly_one_notification_on_success` — **PASSED**
    Verifies that support ticket replies trigger exactly one customer notification with the correct category.
*   `SendAdminNote_should_dispatch_exactly_one_notification_if_not_internal` — **PASSED**
    Verifies that public admin notes dispatch a single notification with the correct type.
*   `SendAdminNote_should_skip_notification_if_internal` — **PASSED**
    Verifies that internal notes do not dispatch any external notifications.

---

## 4. Regression & Test Suite Status Check

All unit and integration test suites compile and run without regressions:
*   **`FoodLoop.Domain.Tests`**: 28 passed.
*   **`FoodLoop.Application.Tests`**: 11 passed.
*   **`FoodLoop.Infrastructure.Tests`**: 175 passed (including the 8 new verification tests).
*   **Total Suite Status**: **214 Passed, 0 Failed**.

---

## 5. Open Items (Product Decisions Required)

1.  **FCM Token Cleanup Strategy:** Should invalid/unregistered device tokens be completely deleted from the database rather than just flagged as `IsActive = false`?
2.  **Notification Queueing:** For offline users, is a simple "fire-and-forget" SignalR message acceptable, or should they be queued/retried once the user reconnects?
3.  **Missing AI and Donation Triggers:** Should we implement notification triggers for AI pricing recommendations and donation events, or should these be left for future sprints?
