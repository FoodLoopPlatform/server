# FoodLoop Notification Specification — Mobile App (FCM)

This document provides the concrete developer-ready contract and integration details for the FoodLoop iOS and Android Mobile teams implementing push notifications via Firebase Cloud Messaging (FCM).

---

## 1. Device Token Registration Endpoint

To receive push notifications, the mobile app must register its device token with the FoodLoop backend.

*   **Endpoint Route:** `POST /notifications/device-token`
*   **Authentication Requirement:** Bearer Token required (`[Authorize]`) in the standard authorization header:
    ```text
    Authorization: Bearer <JWT_BEARER_TOKEN>
    ```
*   **Request Headers:** `Content-Type: application/json`
*   **Request Body Schema:**
    ```json
    {
      "token": "string (Required. The FCM registration token obtained from Firebase SDK)",
      "platform": "string (Optional. e.g., 'iOS', 'Android', 'Mobile'. Defaults to 'Mobile' if null)"
    }
    ```
*   **Response Schema (200 OK):**
    Matches the standard backend ApiResponse envelope:
    ```json
    {
      "success": true,
      "data": {
        "success": true
      },
      "message": null,
      "errors": []
    }
    ```
*   **Error Responses:**
    *   `400 Bad Request`: If `token` is missing or whitespace.
    *   `401 Unauthorized`: If the bearer token is missing or expired.

---

## 2. FCM Payload Structure (Notification vs. Data)

To support correct background and foreground rendering behavior on iOS and Android:
*   FCM push notifications are dispatched as **hybrid payloads** containing both `notification` and `data` blocks.

```json
{
  "message": {
    "token": "fcm_registration_token",
    "notification": {
      "title": "Display Title",
      "body": "Display text body"
    },
    "data": {
      "type": "EventType",
      "userId": "user-guid-string",
      "title": "Display Title",
      "body": "Display text body"
    }
  }
}
```

### 2.1 OS vs. App Rendering Paths
1.  **App in Background or Terminated (Killed):** The OS interceptor automatically captures the `notification` block and displays a standard system-level banner. When the user taps the banner, the OS launches the app and passes the `data` block to the application context.
2.  **App in Foreground:** The OS does **not** show a system banner. Instead, the Firebase SDK raises an in-app message event, and the mobile application must extract the information from the `data` block to render a custom in-app banner or alert.

---

## 3. Deep-Linking Key Catalog

Currently, the server dispatches a flat key-value collection in the `data` block.

| Data Key | Type | Description |
| :--- | :--- | :--- |
| `type` | `string` | The event category. Used to route the app (e.g. `"SupportTicketReply"`, `"OrderPlaced"`). |
| `userId` | `string` | The unique recipient user ID (UUID format). |
| `title` | `string` | Mirrors the display title of the message. |
| `body` | `string` | Mirrors the display text body of the message. |

> [!WARNING]
> **No Structured Key Deep-Linking:**
> The current payload does **not** contain specific structured keys (like `orderId` or `ticketId`). If the mobile app needs to perform entity-specific deep-linking (e.g., opening a specific order), it must either:
> 1. Parse the ID directly from the `body` string (e.g., matching the prefix `#` in `"Your order #d7a123f1..."`).
> 2. request a backend update to add structured keys to the `data` block.

---

## 4. Real-World JSON Examples (Per Event Category)

### 4.1 Support Ticket Reply
*   *Note:* The `userId` in the `data` block is the distinct recipient customer's user ID.
```json
{
  "message": {
    "token": "fcm_device_token",
    "notification": {
      "title": "Support Ticket Reply",
      "body": "You have received a new reply on your support ticket regarding: Billing."
    },
    "data": {
      "type": "SupportTicketReply",
      "userId": "11111111-1111-1111-1111-111111111111",
      "title": "Support Ticket Reply",
      "body": "You have received a new reply on your support ticket regarding: Billing."
    }
  }
}
```

### 4.2 Account Warning (Official Admin Note)
```json
{
  "message": {
    "token": "fcm_device_token",
    "notification": {
      "title": "Account Warning",
      "body": "Your account has received a warning due to policy violation."
    },
    "data": {
      "type": "AdminWarning",
      "userId": "22222222-2222-2222-2222-222222222222",
      "title": "Account Warning",
      "body": "Your account has received a warning due to policy violation."
    }
  }
}
```

### 4.3 Order Placed (Consumer Side)
*   *Note:* The `userId` in the `data` block is the distinct placing customer's user ID. The order ID `#d7a123f1` is visible only in the message text.
```json
{
  "message": {
    "token": "fcm_device_token",
    "notification": {
      "title": "Order Placed Successfully",
      "body": "Your order #d7a123f1 has been placed successfully."
    },
    "data": {
      "type": "OrderPlaced",
      "userId": "33333333-3333-3333-3333-333333333333",
      "title": "Order Placed Successfully",
      "body": "Your order #d7a123f1 has been placed successfully."
    }
  }
}
```

### 4.4 Order Received (Merchant Side)
*   *Note:* The `userId` in the `data` block is the distinct merchant owner's user ID who receives the push notification.
```json
{
  "message": {
    "token": "fcm_device_token",
    "notification": {
      "title": "New Order Received",
      "body": "Store 'Bakery' received order #f8b234a2 for pickup."
    },
    "data": {
      "type": "OrderReceived",
      "userId": "44444444-4444-4444-4444-444444444444",
      "title": "New Order Received",
      "body": "Store 'Bakery' received order #f8b234a2 for pickup."
    }
  }
}
```

### 4.5 Order Status Updated (e.g. Confirmed)
```json
{
  "message": {
    "token": "fcm_device_token",
    "notification": {
      "title": "Order Confirmed",
      "body": "Your order has been confirmed by the merchant."
    },
    "data": {
      "type": "OrderConfirmed",
      "userId": "55555555-5555-5555-5555-555555555555",
      "title": "Order Confirmed",
      "body": "Your order has been confirmed by the merchant."
    }
  }
}
```

---

## 5. Token Lifecycle & Registration Best Practices

*   **Automatic Server Deactivation:**
    *   If the mobile app is uninstalled or the user revokes permissions, Google's FCM service returns `Unregistered` or `InvalidArgument` status.
    *   The FoodLoop backend catches this and immediately sets `IsActive = false` for that token.
*   **Cross-Tenant Device Sharing:**
    *   When User A logs out and User B logs in on the same phone, User B's token registration automatically flags User A's token as inactive to prevent cross-tenant message leakage.
*   **Mobile App Token Registration Rules:**
    1.  **Register on Auth State Change:** Register the FCM token via `POST /notifications/device-token` immediately upon successful user login.
    2.  **Register on Token Refresh:** Implement Firebase's token listener (`onNewToken` for Android / `messaging(_:didReceiveRegistrationToken:)` for iOS) to upload refreshed tokens to the server.
    3.  **App Launch Sync:** Checking and registering the token during app launch sequences ensures active token sync.

---

## 6. Out of Scope Events

The following events are explicitly **out of scope** for this release phase. Mobile teams must not expect or handle these types in the client application:
*   **AI Recommendations:** Staging, approvals, rejections, and auto-execution events.
*   **Donations:** Donation listings, matches, and pickup statuses.
