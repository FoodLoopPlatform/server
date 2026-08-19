# FoodLoop Notification Specification — Web & Frontend (SignalR)

This document provides the concrete developer-ready contract and integration details for the FoodLoop Web and Frontend UI teams implementing real-time notifications via SignalR.

---

## 1. Connection Details & Handshake

The real-time notification hub is implemented using ASP.NET Core SignalR.

*   **Full Route:** `/hubs/notifications` (e.g., `wss://<host>/hubs/notifications` or `https://<host>/hubs/notifications`)
*   **Transports:** WebSockets (recommended and preferred), Server-Sent Events (SSE), or Long Polling.
*   **Authentication Handshake:**
    *   WebSockets do not permit custom headers during the initial HTTP handshake. Therefore, authentication is performed by appending the JWT bearer token as a query string parameter named `access_token`.
    *   **Handshake URL Pattern:**
        ```text
        wss://<host>/hubs/notifications?access_token=<JWT_BEARER_TOKEN>
        ```
*   **Reconnection Behavior:**
    *   The server maintains connection lifecycles but does not enforce automatic retries.
    *   Frontend developers must enable automatic client-side reconnection using the SignalR Client SDK (`withAutomaticReconnect()`):
        ```javascript
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/notifications", {
                accessTokenFactory: () => this.getAccessToken() // Must refresh expired tokens
            })
            .withAutomaticReconnect()
            .build();
        ```

---

## 2. Client-Side Methods (Event Invocation)

The backend interacts with connected clients through a single generic client-side event.

*   **Method Name:** `ReceiveNotification`
*   **Parameter:** `NotificationDto` (JSON object)
*   **Discrimination Logic:** Since all notifications flow through `ReceiveNotification`, the frontend must inspect the `type` field on the payload to route the message to the appropriate UI component or trigger specific audio/visual alerts.

---

## 3. TypeScript Interfaces (Data Contract)

This is the exact contract matching the C# [`NotificationDto`](file:///c:/ITI/server/src/FoodLoop.Application/DTOs/Notifications/NotificationDto.cs):

```typescript
export interface NotificationDto {
  /** Unique notification identifier */
  id: string; // UUID v4 format
  
  /** Display title for the banner or notification center */
  title: string;
  
  /** Body message content */
  body: string;
  
  /** 
   * Business event type. Valid values:
   * - "SupportTicketReply"
   * - "AdminWarning"
   * - "AdminUrgent"
   * - "AdminNotice"
   * - "OrderPlaced"
   * - "OrderReceived"
   * - "OrderConfirmed"
   * - "OrderPreparing"
   * - "OrderReadyForPickup"
   * - "OrderCompleted"
   * - "OrderCancelled"
   */
  type: string;
  
  /** Indicates whether the notification has been read by the user */
  isRead: boolean;
  
  /** Creation timestamp in ISO 8601 UTC format */
  createdAt: string; // e.g. "2026-08-18T14:00:34.123Z"
}
```

---

## 4. Real-World JSON Examples (Per Event Category)

### 4.1 Support Ticket Reply
```json
{
  "id": "e93fca41-5839-4d6d-9781-807d8dcd8da9",
  "title": "Support Ticket Reply",
  "body": "You have received a new reply on your support ticket regarding: Billing.",
  "type": "SupportTicketReply",
  "isRead": false,
  "createdAt": "2026-08-18T14:00:34.123Z"
}
```

### 4.2 Official Admin Note
```json
{
  "id": "a5c7f8a9-1234-456d-89ef-b07d8dc12da1",
  "title": "Account Warning",
  "body": "Your account has received a warning due to policy violation.",
  "type": "AdminWarning",
  "isRead": false,
  "createdAt": "2026-08-18T14:01:10.456Z"
}
```

### 4.3 Order Placed (Consumer Side)
```json
{
  "id": "d7a123f1-4321-4def-9abc-c0812e123456",
  "title": "Order Placed Successfully",
  "body": "Your order #d7a123f1 has been placed successfully.",
  "type": "OrderPlaced",
  "isRead": false,
  "createdAt": "2026-08-18T14:02:15.789Z"
}
```

### 4.4 Order Received (Merchant Side)
```json
{
  "id": "f8b234a2-8765-4bcd-8def-d0923f234567",
  "title": "New Order Received",
  "body": "Store 'Bakery' received order #f8b234a2 for pickup.",
  "type": "OrderReceived",
  "isRead": false,
  "createdAt": "2026-08-18T14:02:16.111Z"
}
```

### 4.5 Order Status Updated (e.g., Confirmed)
```json
{
  "id": "b1c345d3-9876-4cba-9fed-e0a34f345678",
  "title": "Order Confirmed",
  "body": "Your order has been confirmed by the merchant.",
  "type": "OrderConfirmed",
  "isRead": false,
  "createdAt": "2026-08-18T14:05:00.222Z"
}
```

---

## 5. Handshake Errors & Disconnection UX

*   **Auth Failure (401 Unauthorized):**
    *   If the JWT token is missing, invalid, or expired, the SignalR handshake fails with HTTP Status `401 Unauthorized`.
    *   **Frontend Action:** Catch this error on start. Do not attempt a retry loop without refreshing the credentials first to prevent spamming the server.
*   **Token Expiry While Connected:**
    *   The WebSocket connection is not closed immediately upon JWT token expiration; it remains open as long as the socket is active.
    *   If the connection drops due to network changes, reconnection will fail with `401 Unauthorized` until the client fetches a new token.

---

## 6. Out of Scope Events

The following events are explicitly **out of scope** for this release phase. Frontend teams must not expect or handle these types in the client application:
*   **AI Recommendations:** Staging, approvals, rejections, and auto-execution events.
*   **Donations:** Donation listings, matches, and pickup statuses.
