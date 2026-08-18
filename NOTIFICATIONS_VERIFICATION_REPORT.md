# FoodLoop Notification System End-to-End Verification Report

This report presents the end-to-end investigation findings, architectural improvements, test coverage status, and resolved product decisions for the FoodLoop Notification System.

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

## 3. Resolved Product Decisions (Part 3)

The following design choices have been explicitly decided and recorded for the spec:

1.  **FCM Token Cleanup Strategy:**
    *   **Decision:** Flag-only (`IsActive = false`) is selected. This preserves historical associations of devices with users for audit trails and security/compliance analytics, while preventing future delivery attempts to expired or invalid tokens.
2.  **Offline Notification Handling:**
    *   **Decision:** Fire-and-forget is selected for real-time delivery channels (SignalR/Firebase). When a client is offline, the message is discarded on those channels. However, because the notification is always persisted in the database `Notifications` table first, the client receives all history by querying their database inbox upon reconnecting.
3.  **AI & Donation Triggers scope:**
    *   **Decision:** Explicitly out of scope for the current sprint and the upcoming `NOTIFICATIONS_SPEC.md`. These will be designed and implemented in a future phase.

---

## 4. Test Suite Breakdown (Part 4)

We have added comprehensive test coverage inside [`NotificationSystemTests.cs`](file:///c:/ITI/server/test/FoodLoop.Infrastructure.Tests/Features/Notifications/NotificationSystemTests.cs):

### Contract / Schema Verification
*   `Hub_connection_should_require_authentication` — **PASSED**
    Asserts that the `NotificationHub` is decorated with the `[Authorize]` attribute to reject unauthenticated WebSocket connections.
*   `Device_token_registration_endpoint_should_require_authentication` — **PASSED**
    Asserts that the `NotificationsController` is decorated with `[Authorize]`.

### Unit & Logic Verification
*   `Device_token_registration_should_deactivate_duplicates_for_other_users` — **PASSED**
    Ensures that registering a token for a new user automatically deactivates duplicate entries for previous users.
*   `SendToUser_should_deactivate_token_when_fcm_returns_unregistered` — **PASSED**
    Simulates a `FirebaseMessagingException` with the `Unregistered` error code and asserts that the corresponding `UserDeviceToken` is deactivated (`IsActive = false`) in the DB.
*   `SendToUser_should_deactivate_token_when_fcm_returns_invalid_argument` — **PASSED**
    Simulates a `FirebaseMessagingException` with the `InvalidArgument` error code and asserts that the corresponding `UserDeviceToken` is deactivated (`IsActive = false`) in the DB.
*   `SendToUser_should_not_deactivate_token_when_fcm_returns_internal_error` — **PASSED**
    Simulates a `FirebaseMessagingException` with a non-stale error (`Internal`) and asserts that the token is **not** deactivated.
*   `Notification_failures_in_signalr_or_firebase_should_not_block_parent_transaction` — **PASSED**
    Asserts that even if SignalR and Firebase throw exceptions, the notification is still successfully saved to the database and no error propagates to the caller.
*   `SendNotification_to_offline_user_should_not_throw_exception` — **PASSED**
    Verifies that sending to an offline SignalR client completes without errors.

### Integration / Trigger Verification
*   `ReplyToSupportTicket_should_dispatch_exactly_one_notification_on_success` — **PASSED**
    Verifies that support ticket replies trigger exactly one customer notification with the correct category.
*   `SendAdminNote_should_dispatch_exactly_one_notification_if_not_internal` — **PASSED**
    Verifies that public admin notes dispatch a single notification with the correct type.
*   `SendAdminNote_should_skip_notification_if_internal` — **PASSED**
    Verifies that internal notes do not dispatch any external notifications.
*   `CreateOrder_should_dispatch_exactly_one_customer_and_one_merchant_notification` — **PASSED**
    Verifies that placing an order dispatches exactly one customer notification (`OrderPlaced`) and exactly one merchant notification (`OrderReceived`).
*   `UpdateOrderStatus_should_dispatch_exactly_one_customer_notification` — **PASSED**
    Verifies that confirming an order dispatches exactly one customer notification (`OrderConfirmed`).

---

## 5. Full Solution Test Suite Execution Log (Regressions & Status Check)

All test suites compile and run without regressions. The complete log of `dotnet test` is captured below:

```text
Determining projects to restore...
C:\ITI\server\src\FoodLoop.Infrastructure\FoodLoop.Infrastructure.csproj : warning NU1510: PackageReference System.Security.Cryptography.Xml will not be pruned. Consider removing this package from your dependencies, as it is likely unnecessary. [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\test\FoodLoop.Infrastructure.Tests\FoodLoop.Infrastructure.Tests.csproj : warning NU1603: FoodLoop.Infrastructure.Tests depends on Microsoft.AspNetCore.Mvc.Testing (>= 10.0.0-rc.2.24474.3) but Microsoft.AspNetCore.Mvc.Testing 10.0.0-rc.2.24474.3 was not found. Microsoft.AspNetCore.Mvc.Testing 10.0.0-rc.2.25502.107 was resolved instead. [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\test\FoodLoop.Infrastructure.Tests\FoodLoop.Infrastructure.Tests.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-23rf-6693-g89p [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-37gx-xxp4-5rgx [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-6588-8gv4-xfgh [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-8q5v-6pqq-x66h [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-cvvh-rhrc-wg4q [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-g8r8-53c2-pm3f [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-mmjf-rqrv-855v [C:\ITI\server\FoodLoop.sln]
C:\ITI\server\src\FoodLoop.DbTool\FoodLoop.DbTool.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.2 has a known high severity vulnerability, https://github.com/advisories/GHSA-w3x6-4m5h-cxqf [C:\ITI\server\FoodLoop.sln]
  All projects are up-to-date for restore.
C:\ITI\server\test\FoodLoop.Infrastructure.Tests\FoodLoop.Infrastructure.Tests.csproj : warning NU1603: FoodLoop.Infrastructure.Tests depends on Microsoft.AspNetCore.Mvc.Testing (>= 10.0.0-rc.2.24474.3) but Microsoft.AspNetCore.Mvc.Testing 10.0.0-rc.2.24474.3 was not found. Microsoft.AspNetCore.Mvc.Testing 10.0.0-rc.2.25502.107 was resolved instead.
C:\ITI\server\test\FoodLoop.Infrastructure.Tests\FoodLoop.Infrastructure.Tests.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q
C:\ITI\server\src\FoodLoop.Infrastructure\FoodLoop.Infrastructure.csproj : warning NU1510: PackageReference System.Security.Cryptography.Xml will not be pruned. Consider removing this package from your dependencies, as it is likely unnecessary.
  FoodLoop.Domain -> C:\ITI\server\src\FoodLoop.Domain\bin\Debug\net10.0\FoodLoop.Domain.dll
  FoodLoop.Application -> C:\ITI\server\src\FoodLoop.Application\bin\Debug\net10.0\FoodLoop.Application.dll
  FoodLoop.Infrastructure -> C:\ITI\server\src\FoodLoop.Infrastructure\bin\Debug\net10.0\FoodLoop.Infrastructure.dll
  FoodLoop.API -> C:\ITI\server\src\FoodLoop.API\bin\Debug\net10.0\FoodLoop.API.dll
  FoodLoop.Domain.Tests -> C:\ITI\server\test\FoodLoop.Domain.Tests\bin\Debug\net10.0\FoodLoop.Domain.Tests.dll
  FoodLoop.Application.Tests -> C:\ITI\server\test\FoodLoop.Application.Tests\bin\Debug\net10.0\FoodLoop.Application.Tests.dll
Test run for C:\ITI\server\test\FoodLoop.Domain.Tests\bin\Debug\net10.0\FoodLoop.Domain.Tests.dll (.NETCoreApp,Version=v10.0)
Test run for C:\ITI\server\test\FoodLoop.Application.Tests\bin\Debug\net10.0\FoodLoop.Application.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    28, Skipped:     0, Total:    28, Duration: 127 ms - FoodLoop.Domain.Tests.dll (net10.0)

Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 310 ms - FoodLoop.Application.Tests.dll (net10.0)
  FoodLoop.Infrastructure.Tests -> C:\ITI\server\test\FoodLoop.Infrastructure.Tests\bin\Debug\net10.0\FoodLoop.Infrastructure.Tests.dll
Test run for C:\ITI\server\test\FoodLoop.Infrastructure.Tests\bin\Debug\net10.0\FoodLoop.Infrastructure.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   180, Skipped:     0, Total:   180, Duration: 18 s - FoodLoop.Infrastructure.Tests.dll (net10.0)
```
