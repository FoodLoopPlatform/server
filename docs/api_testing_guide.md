# FoodLoop Complete API Testing Guide (All Endpoints)

This document provides the complete API specification and testing manual for every controller in the FoodLoop system.

---

## 🛠️ Configuration & Setup (Options Pattern)

The backend uses the strongly-typed **Options Pattern** for configuring external services. It supports two configuration approaches:

### 1. Configuration Methods
*   **Standard Configuration (`appsettings.json`)**:
    ```json
    {
      "Smtp": {
        "Host": "smtp-relay.brevo.com",
        "Port": 587,
        "Username": "your_email@example.com",
        "Password": "your_api_key",
        "FromEmail": "noreply@foodloop.com"
      },
      "Cloudinary": {
        "Url": "cloudinary://..."
      }
    }
    ```
*   **Environment File (`.env` in repository root)**:
    At startup, a custom parser reads the `.env` file and binds these flat keys as fallbacks:
    ```ini
    CLOUDINARY_URL=cloudinary://...
    SMTP_HOST=smtp-relay.brevo.com
    SMTP_PORT=587
    SMTP_USERNAME=your_email@example.com
    SMTP_PASSWORD=your_api_key
    SMTP_FROM_EMAIL=noreply@foodloop.com
    ```

### 2. Service Fallbacks (No Credentials Required)
*   **Email**: If `SMTP_HOST` (or `Smtp:Host`) is empty, the application uses `NullEmailService` which logs the emails to console instead of throwing errors.
*   **File Storage**: If `CLOUDINARY_URL` (or `Cloudinary:Url`) is empty, the application uses `LocalFileStorageService` which saves uploaded files to local disk folders instead of Cloudinary.

---

## 🔑 Authentication Module (`/auth`)

### 1. Register Account
*   **Endpoint**: `POST /auth/register`
*   **Request Body**:
    ```json
    {
      "name": "Jane Doe",
      "email": "jane.merchant@example.com",
      "password": "Password@123",
      "role": "Merchant",
      "businessName": "Jane Fresh Organics"
    }
    ```
*   **Response (201 Created)**:
    ```json
    {
      "success": true,
      "message": "User registered successfully.",
      "data": {
        "id": "e3fa0739-b96b-4aea-9c07-45bb63de2051",
        "name": "Jane Doe",
        "email": "jane.merchant@example.com",
        "role": "Merchant",
        "status": "PendingVerification"
      }
    }
    ```

### 2. User Login
*   **Endpoint**: `POST /auth/login`
*   **Request Body**:
    ```json
    {
      "email": "jane.merchant@example.com",
      "password": "Password@123"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Login successful.",
      "data": {
        "userId": "e3fa0739-b96b-4aea-9c07-45bb63de2051",
        "email": "jane.merchant@example.com",
        "role": "Merchant",
        "accessToken": "",
        "refreshToken": "rt_8f0a12e3-b96b-4aea-9c07-45bb63de2051"
      }
    }
    ```
    *Note: `accessToken` is empty `""` because the merchant store is not yet verified.*

### 3. Refresh Token Session
*   **Endpoint**: `POST /auth/refresh`
*   **Request Body**:
    ```json
    {
      "refreshToken": "rt_8f0a12e3-b96b-4aea-9c07-45bb63de2051"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "accessToken": "eyJhbGciOiJIUzI1NiIsIn...",
        "refreshToken": "rt_new_token_string"
      }
    }
    ```

### 4. User Logout
*   **Endpoint**: `POST /auth/logout`
*   **Request Body**:
    ```json
    {
      "refreshToken": "rt_new_token_string"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Logout successful."
    }
    ```

### 5. Forgot Password
*   **Endpoint**: `POST /auth/forgot-password`
*   **Request Body**:
    ```json
    {
      "email": "customer.test@example.com"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Reset token generated successfully.",
      "data": {
        "resetToken": "rst_9f0a12e3-b96b"
      }
    }
    ```

### 6. Reset Password
*   **Endpoint**: `POST /auth/reset-password`
*   **Request Body**:
    ```json
    {
      "email": "customer.test@example.com",
      "token": "rst_9f0a12e3-b96b",
      "newPassword": "NewSecurePassword@123"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Password reset successfully."
    }
    ```

### 7. Resend Verification Email
*   **Endpoint**: `POST /auth/resend-verification`
*   **Request Body**:
    ```json
    {
      "email": "jane.merchant@example.com"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Verification email sent."
    }
    ```

---

## 🧑‍💼 User & Profile Module (`/users`)

*   **Headers**: `Authorization: Bearer <Customer_JWT>`

### 1. Get Current User Profile
*   **Endpoint**: `GET /users/me`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "e3fa0739-b96b-4aea-9c07-45bb63de2051",
        "name": "Jane Doe",
        "email": "jane.merchant@example.com",
        "profilePicture": "https://res.cloudinary.com/profiles/jane.jpg",
        "language": "en"
      }
    }
    ```

### 2. Update Profile Info
*   **Endpoint**: `PATCH /users/me`
*   **Request Body**:
    ```json
    {
      "name": "Jane S. Doe",
      "profilePicture": "https://res.cloudinary.com/profiles/jane_updated.jpg",
      "language": "ar"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "e3fa0739-b96b-4aea-9c07-45bb63de2051",
        "name": "Jane S. Doe",
        "profilePicture": "https://res.cloudinary.com/profiles/jane_updated.jpg",
        "language": "ar"
      }
    }
    ```

### 3. Update User Preferences
*   **Endpoint**: `PATCH /users/me/preferences`
*   **Request Body**:
    ```json
    {
      "enableEmailNotifications": true,
      "enablePushNotifications": false,
      "receiveMarketingEmails": true
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Preferences updated successfully."
    }
    ```

### 4. Get My Addresses
*   **Endpoint**: `GET /users/me/addresses`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "add_a792cd1c-541d-437b-a0f1",
          "label": "Home",
          "city": "Cairo",
          "district": "Maadi",
          "street": "Street 9",
          "buildingNo": "12B",
          "floor": 3,
          "apartmentNo": "14",
          "latitude": 30.0444,
          "longitude": 31.2357,
          "isDefault": true
        }
      ]
    }
    ```

### 5. Add Address
*   **Endpoint**: `POST /users/me/addresses`
*   **Request Body**:
    ```json
    {
      "label": "Work",
      "city": "Cairo",
      "district": "New Cairo",
      "street": "90 St",
      "buildingNo": "20",
      "floor": 1,
      "apartmentNo": "3",
      "latitude": 30.0125,
      "longitude": 31.4285,
      "isDefault": false
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "add_b57b350-541d-437b-a0f1",
        "label": "Work",
        "city": "Cairo",
        "district": "New Cairo",
        "street": "90 St",
        "buildingNo": "20"
      }
    }
    ```

### 6. Update Address
*   **Endpoint**: `PATCH /users/me/addresses/add_b57b350-541d-437b-a0f1`
*   **Request Body**:
    ```json
    {
      "floor": 2,
      "apartmentNo": "6"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "add_b57b350-541d-437b-a0f1",
        "label": "Work",
        "floor": 2,
        "apartmentNo": "6"
      }
    }
    ```

### 7. Delete Address
*   **Endpoint**: `DELETE /users/me/addresses/add_b57b350-541d-437b-a0f1`
*   **Response (204 No Content)**: Returns empty body.

---

## 🏪 Stores & Onboarding Module (`/stores`)

### 1. Get My Store Profile
*   **Endpoint**: `GET /stores/me`
*   **Headers**: `Authorization: Bearer <Merchant_JWT>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "4a259c99-52e0-47de-824f-37db1b2f0a12",
        "name": "Spinneys Supermarket",
        "businessCategory": "Grocery",
        "verificationStatus": "Verified",
        "location": {
          "latitude": 30.0444,
          "longitude": 31.2357,
          "city": "Cairo"
        }
      }
    }
    ```

### 2. Update Store Profile (Multipart Form-Data)
*   **Endpoint**: `PATCH /stores/me`
*   **Headers**: `Authorization: Bearer <Merchant_JWT>`
*   **Form Data Fields**:
    *   `Name`: `Organic Food Market`
    *   `BusinessCategory`: `Grocery`
    *   `Logo`: *(Attach Image File)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "4a259c99-52e0-47de-824f-37db1b2f0a12",
        "name": "Organic Food Market",
        "logoUrl": "https://res.cloudinary.com/store-logos/organic.jpg"
      }
    }
    ```

### 3. Update Store Location
*   **Endpoint**: `PATCH /stores/me/location`
*   **Headers**: `Authorization: Bearer <Merchant_JWT>`
*   **Request Body**:
    ```json
    {
      "latitude": 30.0512,
      "longitude": 31.2185,
      "city": "Giza",
      "neighborhood": "Dokki",
      "street": "Tahrir St",
      "buildingNo": "5"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Store location updated successfully."
    }
    ```

### 4. Upload Merchant Store Document (Anonymous/Onboarding)
*   **Endpoint**: `POST /stores/me/documents`
*   **Form Data Fields**:
    *   `Email`: `jane.merchant@example.com`
    *   `Type`: `CommercialRegistration`
    *   `File`: *(Attach CR.pdf)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Document uploaded successfully."
    }
    ```

### 5. Get Merchant Orders List
*   **Endpoint**: `GET /stores/me/orders`
*   **Headers**: `Authorization: Bearer <Merchant_JWT>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "ord_8844-52e0-47de-824f-37db1b2f0a12",
          "totalAmount": 10.00,
          "orderStatus": "Pending",
          "paymentStatus": "Paid"
        }
      ]
    }
    ```

### 6. Update Received Order Status
*   **Endpoint**: `PATCH /stores/me/orders/ord_8844-52e0-47de-824f-37db1b2f0a12/status`
*   **Headers**: `Authorization: Bearer <Merchant_JWT>`
*   **Request Body**:
    ```json
    {
      "status": "ReadyForPickup"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Order status updated successfully."
    }
    ```

---

## 🎗️ Charities Module (`/charities`)

### 1. Get My Charity Profile
*   **Endpoint**: `GET /charities/me`
*   **Headers**: `Authorization: Bearer <Charity_JWT>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "ch_7733-52e0-47de-824f-37db1b2f0a12",
        "name": "Help Hand Charity",
        "verificationStatus": "Verified"
      }
    }
    ```

### 2. Update Charity Profile (Multipart Form-Data)
*   **Endpoint**: `PATCH /charities/me`
*   **Headers**: `Authorization: Bearer <Charity_JWT>`
*   **Form Data Fields**:
    *   `Name`: `Help Hand Charity Org`
    *   `Logo`: *(Attach Image File)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "ch_7733-52e0-47de-824f-37db1b2f0a12",
        "name": "Help Hand Charity Org",
        "logoUrl": "https://res.cloudinary.com/store-logos/charity.jpg"
      }
    }
    ```

### 3. Update Charity Location
*   **Endpoint**: `PATCH /charities/me/location`
*   **Headers**: `Authorization: Bearer <Charity_JWT>`
*   **Request Body**:
    ```json
    {
      "latitude": 30.0444,
      "longitude": 31.2357,
      "city": "Cairo",
      "neighborhood": "Maadi"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Charity location updated successfully."
    }
    ```

### 4. Upload Charity Document (Anonymous/Onboarding)
*   **Endpoint**: `POST /charities/me/documents`
*   **Form Data Fields**:
    *   `Email`: `charity.helphand@example.com`
    *   `Type`: `AssociationCertificate`
    *   `File`: *(Attach CR.pdf)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Document uploaded successfully."
    }
    ```

---

## 📦 Products Inventory Module (`/stores/me/products`)

*   **Headers**: `Authorization: Bearer <Merchant_JWT>`

### 1. Create Product
*   **Endpoint**: `POST /stores/me/products`
*   **Request Body**:
    ```json
    {
      "categoryId": "e4fa0739-b96b-4aea-9c07-45bb63de2058",
      "title": "Fresh Lettuce Head",
      "titleAr": "خس طازج",
      "description": "Crisp green organic lettuce",
      "descriptionAr": "خس طازج مغسول ومغلف",
      "originalPrice": 8.00,
      "discountedPrice": 4.00,
      "quantityAvailable": 20,
      "expirationDate": "2026-08-15"
    }
    ```
*   **Response (201 Created)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
        "title": "Fresh Lettuce Head",
        "discountedPrice": 4.00,
        "quantityAvailable": 20
      }
    }
    ```

### 2. Get Product Inventory List
*   **Endpoint**: `GET /stores/me/products?pageNumber=1&pageSize=10`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
          "title": "Fresh Lettuce Head",
          "quantityAvailable": 20
        }
      ]
    }
    ```

### 3. Get Product Details
*   **Endpoint**: `GET /stores/me/products/prd_9900-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
        "title": "Fresh Lettuce Head",
        "originalPrice": 8.00,
        "discountedPrice": 4.00,
        "quantityAvailable": 20
      }
    }
    ```

### 4. Update Product
*   **Endpoint**: `PATCH /stores/me/products/prd_9900-52e0-47de-824f-37db1b2f0a12`
*   **Request Body**:
    ```json
    {
      "quantityAvailable": 15,
      "discountedPrice": 3.00
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
        "quantityAvailable": 15,
        "discountedPrice": 3.00
      }
    }
    ```

### 5. Soft-Delete Product
*   **Endpoint**: `DELETE /stores/me/products/prd_9900-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Product soft-deleted successfully."
    }
    ```

### 6. Upload Product Image (Multipart Form-Data)
*   **Endpoint**: `POST /stores/me/products/prd_9900-52e0-47de-824f-37db1b2f0a12/images`
*   **Form Data Fields**:
    *   `file`: *(image.jpg)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
        "imageUrl": "https://res.cloudinary.com/product-images/lettuce.jpg"
      }
    }
    ```

### 7. Delete Product Image
*   **Endpoint**: `DELETE /stores/me/products/prd_9900-52e0-47de-824f-37db1b2f0a12/images/img_uuid_here`
*   **Response (200 OK)**: Returns updated Product DTO.

### 8. Bulk Upload Products (CSV)
*   **Endpoint**: `POST /stores/me/products/bulk`
*   **Form Data Fields**:
    *   `file`: *(inventory.csv)*
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Products uploaded successfully.",
      "data": [
        {
          "id": "prd_7766-52e0-47de-824f",
          "title": "French Baguette"
        }
      ]
    }
    ```

---

## 🏷️ Categories Module (`/categories`)

### 1. Get Categories List
*   **Endpoint**: `GET /categories`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "e4fa0739-b96b-4aea-9c07-45bb63de2058",
          "name": "Bakery",
          "nameAr": "المخبوزات"
        }
      ]
    }
    ```

---

## 🗺️ Marketplace Module (`/marketplace`)

### 1. Search Marketplace Products (Haversine Sorted)
*   **Endpoint**: `GET /marketplace/products?latitude=30.0444&longitude=31.2357&maxDistance=10&sortBy=distance`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "prd_9900-52e0-47de-824f-37db1b2f0a12",
          "title": "Fresh Lettuce Head",
          "originalPrice": 8.00,
          "discountedPrice": 4.00,
          "quantityAvailable": 15,
          "imageUrl": "https://res.cloudinary.com/product-images/lettuce.jpg",
          "storeId": "4a259c99-52e0-47de-824f-37db1b2f0a12",
          "storeName": "Spinneys Supermarket",
          "distanceKm": 0.05
        }
      ]
    }
    ```

---

## 🛒 Orders Module (`/orders`)

*   **Headers**: `Authorization: Bearer <Customer_JWT>`

### 1. Checkout Cart
*   **Endpoint**: `POST /orders`
*   **Request Body**:
    ```json
    {
      "items": [
        {
          "productId": "prd_9900-52e0-47de-824f-37db1b2f0a12",
          "quantity": 2
        }
      ]
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "ord_8844-52e0-47de-824f-37db1b2f0a12",
        "totalAmount": 8.00,
        "orderStatus": "Pending",
        "paymentStatus": "Paid"
      }
    }
    ```

### 2. Get Order History List
*   **Endpoint**: `GET /orders`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "ord_8844-52e0-47de-824f-37db1b2f0a12",
          "totalAmount": 8.00,
          "orderStatus": "Pending"
        }
      ]
    }
    ```

### 3. Get Order Details Conversation
*   **Endpoint**: `GET /orders/ord_8844-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "ord_8844-52e0-47de-824f-37db1b2f0a12",
        "totalAmount": 8.00,
        "orderStatus": "Pending",
        "items": [
          {
            "productTitle": "Fresh Lettuce Head",
            "quantity": 2
          }
        ]
      }
    }
    ```

---

## ⭐ Reviews Module (`/reviews` & `/stores`)

### 1. Submit Order Review
*   **Endpoint**: `POST /reviews`
*   **Headers**: `Authorization: Bearer <Customer_JWT>`
*   **Request Body**:
    ```json
    {
      "orderId": "ord_8844-52e0-47de-824f-37db1b2f0a12",
      "rating": 5,
      "comment": "Super fresh!"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "rev_7733-52e0-47de-824f-37db1b2f0a12",
        "rating": 5,
        "comment": "Super fresh!"
      }
    }
    ```

### 2. Get Public Store Reviews List
*   **Endpoint**: `GET /stores/4a259c99-52e0-47de-824f-37db1b2f0a12/reviews?pageNumber=1&pageSize=10`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "rev_7733-52e0-47de-824f-37db1b2f0a12",
          "rating": 5,
          "comment": "Super fresh!",
          "userFullName": "Customer Test"
        }
      ]
    }
    ```

---

## 🔔 Notifications Module (`/notifications`)

*   **Headers**: `Authorization: Bearer <Customer_JWT>`

### 1. Get Notifications Feed List
*   **Endpoint**: `GET /notifications`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "nt_3300-52e0-47de-824f-37db1b2f0a12",
          "title": "Order Ready",
          "body": "Your order is ready for pickup!",
          "isRead": false
        }
      ]
    }
    ```

### 2. Mark Single Notification Read
*   **Endpoint**: `PATCH /notifications/nt_3300-52e0-47de-824f-37db1b2f0a12/read`
*   **Response (204 No Content)**: Empty body.

### 3. Mark All Notifications Read
*   **Endpoint**: `PATCH /notifications/read-all`
*   **Response (204 No Content)**: Empty body.

---

## 🎫 Customer Support Tickets Module (`/support-tickets`)

*   **Headers**: `Authorization: Bearer <Customer_JWT>`

### 1. Open Support Ticket
*   **Endpoint**: `POST /support-tickets`
*   **Request Body**:
    ```json
    {
      "category": "Refund",
      "message": "Refund confirmation missing.",
      "priority": "High"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "tk_1100-52e0-47de-824f-37db1b2f0a12",
        "category": "Refund",
        "status": "Open"
      }
    }
    ```

### 2. List My Support Tickets
*   **Endpoint**: `GET /support-tickets`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "tk_1100-52e0-47de-824f-37db1b2f0a12",
          "category": "Refund",
          "status": "Open"
        }
      ]
    }
    ```

### 3. Get Support Ticket Conversation Details
*   **Endpoint**: `GET /support-tickets/tk_1100-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "tk_1100-52e0-47de-824f-37db1b2f0a12",
        "category": "Refund",
        "status": "Open",
        "messages": [
          {
            "senderName": "Customer Test",
            "message": "Refund confirmation missing."
          }
        ]
      }
    }
    ```

### 4. Send Customer Reply Message
*   **Endpoint**: `POST /support-tickets/tk_1100-52e0-47de-824f-37db1b2f0a12/reply`
*   **Request Body**:
    ```json
    {
      "message": "Checking back on this."
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "msg_8877-52e0-47de-824f",
        "senderName": "Customer Test",
        "message": "Checking back on this."
      }
    }
    ```

---

## 🛡️ Admin Dashboard Module (`/admin`)

*   **Headers**: `Authorization: Bearer <Admin_JWT>`

### 1. List Pending Merchant Stores
*   **Endpoint**: `GET /admin/stores/pending`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "4a259c99-52e0-47de-824f-37db1b2f0a12",
          "name": "Jane Fresh Organics"
        }
      ]
    }
    ```

### 2. Get Pending Store Verification Details
*   **Endpoint**: `GET /admin/stores/4a259c99-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**: Returns detailed store, location, and document items.

### 3. Approve or Reject Merchant Store Verification
*   **Endpoint**: `PATCH /admin/stores/4a259c99-52e0-47de-824f-37db1b2f0a12/verify`
*   **Request Body**:
    ```json
    {
      "action": "Approved",
      "rejectionReason": null
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Store verification status updated successfully."
    }
    ```

### 4. List Pending Charity Registrations
*   **Endpoint**: `GET /admin/charities/pending`
*   **Response (200 OK)**: Lists pending charity entity profiles.

### 5. Get Pending Charity Details
*   **Endpoint**: `GET /admin/charities/ch_7733-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**: Detailed documents list.

### 6. Verify Charity Organization
*   **Endpoint**: `PATCH /admin/charities/ch_7733-52e0-47de-824f-37db1b2f0a12/verify`
*   **Request Body**:
    ```json
    {
      "action": "Approved"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Charity verification status updated successfully."
    }
    ```

### 7. Modify User Status (Suspend, Ban, Activate)
*   **Endpoint**: `PATCH /admin/users/e3fa0739-b96b-4aea-9c07-45bb63de2051/status`
*   **Request Body**:
    ```json
    {
      "status": "Suspended"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "User status updated successfully."
    }
    ```

### 8. Get User Activity Audit Log
*   **Endpoint**: `GET /admin/users/e3fa0739-b96b-4aea-9c07-45bb63de2051/activity-log`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "action": "OrderPlaced",
          "ipAddress": "127.0.0.1",
          "details": "Placed order #ord_8844"
        }
      ]
    }
    ```

### 9. Get Analytics Dashboard Summary metrics
*   **Endpoint**: `GET /admin/analytics/summary`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "totalStores": 12,
        "totalCharities": 8,
        "totalOrders": 142,
        "totalSavings": 2840.50
      }
    }
    ```

### 10. List All Stores (Filtered & Paginated)
*   **Endpoint**: `GET /admin/stores?pageNumber=1&pageSize=10&status=Verified`
*   **Response (200 OK)**: Lists all matching store records.

### 11. List All Charities
*   **Endpoint**: `GET /admin/charities?pageNumber=1&pageSize=10`
*   **Response (200 OK)**: Lists matching charity records.

### 12. List All Reviews Feed
*   **Endpoint**: `GET /admin/reviews?pageNumber=1&pageSize=10`
*   **Response (200 OK)**: Lists all store reviews.

### 13. Delete/Moderate Review
*   **Endpoint**: `DELETE /admin/reviews/rev_7733-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Review deleted successfully."
    }
    ```

### 14. List System Products Catalog
*   **Endpoint**: `GET /admin/products?pageNumber=1&pageSize=10`
*   **Response (200 OK)**: Lists products across all stores.

### 15. Delete/Moderate Product
*   **Endpoint**: `DELETE /admin/products/prd_9900-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Product suspended/deleted successfully."
    }
    ```

### 16. List Support Tickets Queue
*   **Endpoint**: `GET /admin/support-tickets?pageNumber=1&pageSize=10&status=Open`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "tk_1100-52e0-47de-824f-37db1b2f0a12",
          "category": "Refund",
          "status": "Open"
        }
      ]
    }
    ```

### 17. Get Support Ticket Conversation Details
*   **Endpoint**: `GET /admin/support-tickets/tk_1100-52e0-47de-824f-37db1b2f0a12`
*   **Response (200 OK)**: Returns full messages log.

### 18. Send Admin Support Reply
*   **Endpoint**: `POST /admin/support-tickets/tk_1100-52e0-47de-824f-37db1b2f0a12/reply`
*   **Request Body (Plain Text)**:
    ```text
    Hello, our team is investigating.
    ```
*   **Response (200 OK)**:
    ```json
    {
      "id": "msg_2200-52e0-47de-824f",
      "senderName": "System/Support",
      "message": "Hello, our team is investigating."
    }
    ```

### 19. Close/Resolve Support Ticket
*   **Endpoint**: `PATCH /admin/support-tickets/tk_1100-52e0-47de-824f-37db1b2f0a12/close`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "message": "Support ticket closed successfully."
    }
    ```

---

## 🔔 Notifications Module (`/notifications`)

### 1. List Notifications Feed (Paginated)
*   **Endpoint**: `GET /notifications?pageNumber=1&pageSize=20&isRead=false`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": [
        {
          "id": "8fa1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
          "userId": "11111111-1111-1111-1111-111111111111",
          "title": "New Order Received",
          "body": "Store 'Spinneys' received order #ord_1234 for pickup.",
          "type": "OrderReceived",
          "isRead": false,
          "readAt": null,
          "entityType": "Order",
          "entityId": "22222222-2222-2222-2222-222222222222",
          "createdAt": "2026-08-19T18:00:00Z"
        }
      ]
    }
    ```

### 2. Get Single Notification Detail
*   **Endpoint**: `GET /notifications/8fa1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**: Returns full `NotificationDto`.

### 3. Get Unread Count Badge
*   **Endpoint**: `GET /notifications/unread-count`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": 3
    }
    ```

### 4. Mark Single Notification Read
*   **Endpoint**: `PATCH /notifications/8fa1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c/read`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "id": "8fa1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
        "isRead": true,
        "readAt": "2026-08-19T18:05:00Z"
      }
    }
    ```

### 5. Mark All Notifications Read
*   **Endpoint**: `PATCH /notifications/read-all`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (204 No Content)**

### 6. Register Mobile FCM Push Token
*   **Endpoint**: `POST /notifications/device-token`
*   **Headers**: `Authorization: Bearer <token>`
*   **Request Body**:
    ```json
    {
      "token": "fcm_device_registration_token_xyz",
      "platform": "Android"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": { "success": true }
    }
    ```

---

## 💳 Payments & Wallet Module (`/orders`, `/payments`, `/users/me/wallet`)

### 1. Paymob Online Checkout Initiation
*   **Endpoint**: `POST /orders/22222222-2222-2222-2222-222222222222/paymob-checkout`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "orderId": "22222222-2222-2222-2222-222222222222",
        "paymentToken": "paymob_session_client_secret_token",
        "checkoutUrl": "https://accept.paymob.com/unifiedcheckout/?publicKey=pk_test_...&clientSecret=paymob_session_client_secret_token"
      }
    }
    ```

### 2. Pay With Wallet Balance
*   **Endpoint**: `POST /orders/22222222-2222-2222-2222-222222222222/wallet-checkout`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "orderId": "22222222-2222-2222-2222-222222222222",
        "paymentStatus": "Paid",
        "orderStatus": "Confirmed",
        "amountCharged": 60.00,
        "remainingWalletBalance": 40.00
      }
    }
    ```

### 3. Get User Wallet Balance & Transactions
*   **Endpoint**: `GET /users/me/wallet`
*   **Headers**: `Authorization: Bearer <token>`
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "walletBalance": 125.00,
        "transactions": [
          {
            "id": "33333333-3333-3333-3333-333333333333",
            "amount": 50.00,
            "type": "Refund",
            "description": "Refund for order #ord_1234",
            "createdAt": "2026-08-19T17:30:00Z"
          }
        ]
      }
    }
    ```

### 4. Refund Order (Merchant / Admin)
*   **Endpoint**: `POST /orders/22222222-2222-2222-2222-222222222222/refund`
*   **Headers**: `Authorization: Bearer <merchant_or_admin_token>`
*   **Request Body**:
    ```json
    {
      "amount": 60.00,
      "reason": "Customer cancelled before preparation"
    }
    ```
*   **Response (200 OK)**:
    ```json
    {
      "success": true,
      "data": {
        "orderId": "22222222-2222-2222-2222-222222222222",
        "refundedAmount": 60.00,
        "paymentStatus": "Refunded",
        "orderStatus": "Cancelled"
      }
    }
    ```

