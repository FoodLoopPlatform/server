# FoodLoop Test Accounts & Database Seeding Reference

This document provides a comprehensive reference of all seeded accounts, credentials, and dataset entities in the FoodLoop database.

> **Web Application Login URL**: [https://web-nine-ivory-36.vercel.app/login](https://web-nine-ivory-36.vercel.app/login)

---

## 1. User Accounts & Credentials Summary

| Role | Count | Default Password | Notes |
| :--- | :---: | :--- | :--- |
| **System Admin** | 1 | `Admin@123` | Full access to moderation, analytics, disputes, user bans |
| **Merchants (Stores)** | 10 | `Password@123` | Verified organization owners with products and analytics |
| **Charities (NGOs)** | 5 | `Password@123` | Verified non-profit organizations receiving donations |
| **Customers** | 25 | `Password@123` | Active shoppers with orders, addresses, reviews, favorites |

---

## 2. System Administrator

| Role | Full Name | Email | Password | Phone |
| :--- | :--- | :--- | :--- | :--- |
| **Admin** | System Administrator | `admin@foodloop.com` | `Admin@123` | `+201011111111` |

---

## 3. Merchants & Supermarket Stores (10 Stores)

All merchant stores are verified, configured with locations, cover photos, opening hours, AI auto-discount settings, and catalog listings.

| Brand / Store Name | Manager Name | Login Email | Password | Category | Area / City | Phone |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Spinneys Supermarket** | Spinneys Egypt Manager | `merchant.spinneys@example.com` | `Password@123` | Supermarket | Zamalek, Cairo | `+201020000001` |
| **Carrefour Hypermarket** | Carrefour Store Lead | `merchant.carrefour@example.com` | `Password@123` | Supermarket | Maadi, Cairo | `+201020000002` |
| **Seoudi Supermarket** | Seoudi Operations Head | `merchant.seoudi@example.com` | `Password@123` | Supermarket | Dokki, Giza | `+201020000003` |
| **Metro Market** | Metro Market Officer | `merchant.metro@example.com` | `Password@123` | Supermarket | Heliopolis, Cairo | `+201020000004` |
| **Gourmet Egypt** | Gourmet Fresh Lead | `merchant.gourmet@example.com` | `Password@123` | Grocery Chain | New Cairo, Cairo | `+201020000005` |
| **The Bakery Shop (TBS)** | TBS Bakery Artisan | `merchant.tbs@example.com` | `Password@123` | Bakery | Zamalek, Cairo | `+201020000006` |
| **Fresh Food Market** | Fresh Food Market Mgr | `merchant.freshfood@example.com` | `Password@123` | Supermarket | Sheikh Zayed, Giza | `+201020000007` |
| **Alfa Market** | Alfa Market Supervisor | `merchant.alfa@example.com` | `Password@123` | Supermarket | Mohandessin, Giza | `+201020000008` |
| **Kazyon Market** | Kazyon Branch Lead | `merchant.kazyon@example.com` | `Password@123` | Convenience | Nasr City, Cairo | `+201020000009` |
| **Hyper One Zayed** | Hyper One Sales Lead | `merchant.hyperone@example.com` | `Password@123` | Supermarket | Sheikh Zayed, Giza | `+201020000010` |

---

## 4. Charities & Non-Profit Organizations (5 NGOs)

All charity accounts are verified non-profit organizations capable of receiving surplus food donations from merchant stores.

| Charity / NGO Name | Representative | Login Email | Password | Headquarters Area | Phone |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Egyptian Food Bank (بنك الطعام)** | Egyptian Food Bank Lead | `charity.foodbank@example.com` | `Password@123` | New Cairo, Cairo | `+201030000001` |
| **Resala Charity (جمعية رسالة)** | Resala NGO Director | `charity.resala@example.com` | `Password@123` | Faisal, Giza | `+201030000002` |
| **Orman Association (جمعية الأورمان)** | Orman Association Rep | `charity.orman@example.com` | `Password@123` | Haram, Giza | `+201030000003` |
| **Misr El Kheir (مصر الخير)** | Misr El Kheir Officer | `charity.misrelkheir@example.com` | `Password@123` | Mokattam, Cairo | `+201030000004` |
| **Baheya Foundation (مؤسسة بهية)** | Baheya Community Rep | `charity.baheya@example.com` | `Password@123` | Haram, Giza | `+201030000005` |

---

## 5. Customers / App Consumers (25 Customers)

| Customer Name | Login Email | Password | Governorate | Primary District | Phone |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Ahmed Hassan** | `ahmed.hassan@example.com` | `Password@123` | Cairo | Zamalek | `+201040000001` |
| **Sara Mahmoud** | `sara.mahmoud@example.com` | `Password@123` | Giza | Dokki | `+201040000002` |
| **Mohamed Aly** | `mohamed.aly@example.com` | `Password@123` | Cairo | Maadi | `+201040000003` |
| **Nour El-Din** | `nour.eldin@example.com` | `Password@123` | Cairo | Heliopolis | `+201040000004` |
| **Yasmine Tarek** | `yasmine.tarek@example.com` | `Password@123` | Cairo | New Cairo | `+201040000005` |
| **Omar Khaled** | `omar.khaled@example.com` | `Password@123` | Giza | Sheikh Zayed | `+201040000006` |
| **Mariam Ibrahim** | `mariam.ibrahim@example.com` | `Password@123` | Giza | Mohandessin | `+201040000007` |
| **Karim Mostafa** | `karim.mostafa@example.com` | `Password@123` | Cairo | Nasr City | `+201040000008` |
| **Laila Sherif** | `laila.sherif@example.com` | `Password@123` | Alexandria | Smouha | `+201040000009` |
| **Hassan Farouk** | `hassan.farouk@example.com` | `Password@123` | Alexandria | Gleem | `+201040000010` |
| **Dina Samir** | `dina.samir@example.com` | `Password@123` | Cairo | Shubra | `+201040000011` |
| **Tarek Nabil** | `tarek.nabil@example.com` | `Password@123` | Giza | Agouza | `+201040000012` |
| **Mona Adel** | `mona.adel@example.com` | `Password@123` | Cairo | Rehab | `+201040000013` |
| **Amr Essam** | `amr.essam@example.com` | `Password@123` | Cairo | Madinaty | `+201040000014` |
| **Salma Wael** | `salma.wael@example.com` | `Password@123` | Giza | 6th of October | `+201040000015` |
| **Khaled Yasser** | `khaled.yasser@example.com` | `Password@123` | Cairo | Abbassia | `+201040000016` |
| **Heba Gamal** | `heba.gamal@example.com` | `Password@123` | Alexandria | Roushdy | `+201040000017` |
| **Ziad Ashraf** | `ziad.ashraf@example.com` | `Password@123` | Cairo | Manial | `+201040000018` |
| **Rania Fouad** | `rania.fouad@example.com` | `Password@123` | Giza | Haram | `+201040000019` |
| **Sherif Hamdy** | `sherif.hamdy@example.com` | `Password@123` | Cairo | Garden City | `+201040000020` |
| **Fatma Zaki** | `fatma.zaki@example.com` | `Password@123` | Cairo | Katameya | `+201040000021` |
| **Mostafa Lotfy** | `mostafa.lotfy@example.com` | `Password@123` | Giza | Imbaba | `+201040000022` |
| **Aya Medhat** | `aya.medhat@example.com` | `Password@123` | Alexandria | San Stefano | `+201040000023` |
| **Youssef Nader** | `youssef.nader@example.com` | `Password@123` | Cairo | Sheraton | `+201040000024` |
| **Reem Fathy** | `reem.fathy@example.com` | `Password@123` | Giza | Hadayek El Ahram | `+201040000025` |

---

## 6. Seeded Product Categories (8 Categories)

| Category Name (EN) | Category Name (AR) | Sample Seeded Items |
| :--- | :--- | :--- |
| **Bakery** | مخبوزات | Sourdough Bread, Butter Croissants, Whole Wheat Toast, Cinnamon Rolls |
| **Dairy & Eggs** | ألبان وبيض | Organic Milk 1L, Greek Yogurt 500g, Eggs Carton (30), Feta Cheese 250g |
| **Fruits & Vegetables** | خضار وفواكه | Organic Bananas 1kg, Gala Apples 1kg, Baby Spinach 300g, Tomatoes 1.5kg |
| **Meat & Poultry** | لحوم ودواجن | Fresh Chicken Breast 1kg, Lean Minced Beef 500g |
| **Prepared Meals** | وجبات جاهزة | Roasted Chicken & Rice, Penne Arrabbiata, Beef Kofta & Tahini Platter |
| **Beverages** | مشروبات | Cold Pressed Orange Juice 1L, Unsweetened Almond Milk 1L |
| **Canned & Pantry** | معلبات ومؤن | Canned Chickpeas, Tuna Chunks, Tomato Paste, Olive Oil |
| **Desserts & Sweets** | حلويات | Belgian Chocolate Mousse Cup, Mixed Fresh Fruit Tartlet |

---

## 7. Seeded Database Entities Overview

When the database is populated via `scripts/seed_db.sh`, the following entities and relations are generated:

### 🛍️ Products & Inventory (70 Products)
- **7 products per store** across 10 merchant stores.
- Every product includes:
  - High-resolution product display image.
  - Price audit history record (`PriceHistories`) showing price progression.
  - AI OCR Recognition Log (`AIRecognitionResults`) with extracted text, expiration date, and high confidence score ($> 0.88$).
  - Realistic expiration dates (1 to 12 days ahead).
  - Discounted prices ($30\%$ to $60\%$ off original retail price).

### 🏠 Customer Saved Addresses (35 Addresses)
- Primary default **Home** addresses in Zamalek, Maadi, Dokki, Heliopolis, New Cairo, etc.
- Secondary **Company / Office** addresses for customers.
- Accurate GPS coordinates (`Latitude`, `Longitude`) and building/apartment numbers.

### 📦 Orders, Line Items & Payments (40 Orders)
- Multi-item customer orders across various stores.
- Status distribution: `Completed` (with credit card payments and transaction references), `Confirmed`, and `Pending`.
- Linked payments with transaction references (`TXN_xxxxxxx`).

### ⭐ Store Ratings & Customer Reviews (18 Reviews)
- Real customer reviews for completed orders with 4-star and 5-star ratings.
- Arabic and English customer feedback comments.

### ❤️ Customer Favorites (75 Favorites)
- Pre-populated favorite product bookmarks per customer account.

### 🤝 Surplus Donations (12 Donations)
- Direct surplus food donations from merchant stores (Spinneys, Carrefour, Seoudi, etc.) to verified charities (Egyptian Food Bank, Resala, Orman, Misr El Kheir, Baheya).
- Marked with status `Delivered`.

### 🔔 Notifications Feed (30 Notifications)
- Customer notification feed with special surplus deals and order confirmation updates.

### 💬 Support Tickets & Conversations (10 Tickets)
- Support tickets opened by customer accounts with two-way conversation history (Customer inquiry and Admin response).

### ⚖️ Product Reports & Disputes (4 Reports)
- Unresolved and resolved product issue dispute reports for admin review.

### 📜 System Audit Trail Logs (20 Records)
- Audit log records tracking store verifications, approvals, and profile updates.

---

## 8. Database Management Scripts

### Reset Database (Clean Wipe)
Wipes all tables safely with foreign key constraint handling:
```bash
bash scripts/reset_db.sh
```

### Seed Database (Full Large Dataset)
Wipes and seeds the entire database with all 25+ tables and 41 users:
```bash
bash scripts/seed_db.sh
```

### Run Full Test Suite
Runs the 260 integration test assertions across all 42 modules:
```bash
bash api_tests.sh
```
