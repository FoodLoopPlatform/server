# FoodLoop Presentation & Showcase — Comprehensive Demo Data Seeding Blueprint

This specification contains the complete, deterministic demo dataset design for showcasing the FoodLoop Platform across all roles, business scenarios, AI pricing workflows, and order lifecycles.

When executed, this seeder will wipe existing database tables and populate rich, realistic, presentation-ready data with time-relative dates (`DateTimeOffset.UtcNow`).

---

## 1. 🔑 Presentation Credentials Cheatsheet

All demo accounts share the standard password: **`P@ssword123!`**

| Role / Persona | Email | Purpose / Showcase Flow |
| :--- | :--- | :--- |
| **System Admin** | `admin@foodloop.com` | Global settings, price floor policy toggles (Dynamic vs Fixed 50%), platform audit metrics. |
| **Merchant (Assisted)** | `merchant.assisted@foodloop.com` | **Bakery & Deli in Zamalek**. Shows pending AI recommendations, risk assessments, and live **Manual Approve / Reject** overrides. |
| **Merchant (Autonomous)** | `merchant.auto@foodloop.com` | **Supermarket in New Cairo**. Shows automated dynamic AI price cuts, price floor protection, and background execution logs. |
| **Merchant (Manual)** | `merchant.manual@foodloop.com` | **Gourmet Grocer in Maadi**. Shows traditional merchant operations opting out of AI automation. |
| **Customer (Active VIP)** | `customer.vip@foodloop.com` | **500 EGP Wallet Balance**. Demonstrates nearby store discovery, wallet checkout, active pickup orders, and price drop alerts. |
| **Customer (Explorer)** | `customer.explorer@foodloop.com` | **50 EGP Wallet Balance**. Demonstrates filtering by distance/category, favoriting stores, and writing reviews. |

---

## 2. 🏪 Store Organizations & Cairo Geospatial Coordinates

| Store Name | Owner | AI Mode | Category Focus | Cairo Location | Coordinates (Lat, Lon) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **"Le Pain Doré Bakery"** | `merchant.assisted` | **Assisted** | Bakery & Pastries | 26 July St, Zamalek | `30.0626, 31.2197` |
| **"FreshMarket Hypermarket"** | `merchant.auto` | **Autonomous** | Dairy, Meat & Produce | 90th St, New Cairo | `30.0168, 31.4340` |
| **"GreenValley Organics"** | `merchant.manual` | **Manual** | Organic & Pantry | Road 9, Maadi | `29.9592, 31.2595` |
| **"Sunrise Dairy & Grocers"** | `merchant.assisted` | **Assisted** | Dairy & Eggs | Abbas El Akkad, Nasr City | `30.0660, 31.3410` |

---

## 3. 📦 Comprehensive Product Catalog (By Scenario)

### Scenario 1: 🔴 Critical Near-Expiry (Assisted AI Approval Demo)
*Store: "Le Pain Doré Bakery" (Assisted Mode)*
*Showcase: Log in as `merchant.assisted@foodloop.com` $\rightarrow$ Open **Pending Recommendations** $\rightarrow$ Click **"Approve"** live to apply new price!*

| # | Product Title | Original Price | Expiry Date | Stock | AI Risk | AI Recommended Markdown | Recommended Price | AI Reason |
| :- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **French Butter Croissants (Pack of 4)** | 80.00 EGP | Tomorrow (`UtcNow + 1d`) | 12 | **CRITICAL** | **15.0%** | **68.00 EGP** | *"Expires in 24 hours with 12 units remaining. Urgent markdown to clear inventory."* |
| 2 | **Artisan Blueberry Cheesecake (Slice)** | 95.00 EGP | In 2 days (`UtcNow + 2d`) | 8 | **HIGH** | **12.5%** | **83.13 EGP** | *"High shelf-life sensitivity with moderate sales velocity."* |
| 3 | **Fresh Greek Feta Cheese 250g** | 75.00 EGP | Tomorrow (`UtcNow + 1d`) | 15 | **CRITICAL** | **15.0%** | **63.75 EGP** | *"1 day left before expiration. Price reduction maximizes recovery."* |
| 4 | **Organic Whole Milk 1L** | 45.00 EGP | In 2 days (`UtcNow + 2d`) | 20 | **HIGH** | **10.0%** | **40.50 EGP** | *"Short shelf life remaining on high volume staple."* |
| 5 | **Smoked Turkey Breast Sandwich** | 65.00 EGP | Today (`UtcNow + 12h`) | 6 | **CRITICAL** | **15.0%** | **55.25 EGP** | *"Prepared deli meal requiring same-day clearance."* |

---

### Scenario 2: 🟢 Autonomous Dynamic Pricing (Price Floor Shield Demo)
*Store: "FreshMarket Hypermarket" (Autonomous Mode)*
*Showcase: Demonstrates unsupervised background AI price adjustment without merchant intervention while honoring floor safety limits.*

| # | Product Title | Original Price | Discounted Price | Floor Policy | Calculated Floor | Status | AI Reason / Action |
| :- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Gourmet Beef Salami 200g** | 120.00 EGP | **102.00 EGP** (15% off) | Fixed 50% | 60.00 EGP | `AutoExecuted` | *"Automated pricing cycle applied 15% markdown safely above 60 EGP floor."* |
| 2 | **Farm Fresh Eggs (30 pack)** | 160.00 EGP | **140.00 EGP** (12.5% off) | Fixed 50% | 80.00 EGP | `AutoExecuted` | *"High velocity stock markdown applied autonomously."* |
| 3 | **Imported Cheddar Block 400g** | 210.00 EGP | **180.00 EGP** (14.3% off) | Fixed 50% | 105.00 EGP | `AutoExecuted` | *"Autonomous discount applied to avert expiry waste."* |
| 4 | **Fresh Salmon Fillet 300g** | 280.00 EGP | **280.00 EGP** | Dynamic AI | 252.00 EGP (90%) | `Rejected` | *"[Price Floor Shield] Proposed 230 EGP fell below 252 EGP floor. AI markdown blocked."* |

---

### Scenario 3: 🔵 Surplus Food Mystery Boxes & Bundles
*Showcase: High-discount food rescue bundles popular on consumer marketplace.*

| # | Bundle Title | Store | Original Value | Rescue Price | Quantity | Description |
| :- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **"Daily Surprise Pastry Box"** | Le Pain Doré | 150.00 EGP | **75.00 EGP** (50% off) | 5 | Assortment of daily baked Danish pastries, muffins, and croissants. |
| 2 | **"Evening Deli Rescue Bag"** | FreshMarket | 180.00 EGP | **90.00 EGP** (50% off) | 4 | Freshly cut cheeses, cold cuts, and artisan breads. |
| 3 | **"Organic Fruit & Veggie Basket"** | GreenValley | 120.00 EGP | **60.00 EGP** (50% off) | 6 | Seasonal organic produce nearing optimal ripeness. |

---

### Scenario 4: 🟡 Standard Fresh Inventory (Control Products)
*Showcase: Fresh, long shelf-life products that AI leaves untouched at full price.*

| # | Product Title | Store | Price | Expiry Date | Stock | Risk Level |
| :- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Extra Virgin Olive Oil 750ml** | GreenValley | 240.00 EGP | `UtcNow + 180d` | 30 | `LOW` |
| 2 | **Whole Wheat Flour 1kg** | Le Pain Doré | 35.00 EGP | `UtcNow + 90d` | 50 | `LOW` |
| 3 | **Basmati Rice Premium 2kg** | FreshMarket | 175.00 EGP | `UtcNow + 365d` | 40 | `LOW` |
| 4 | **Raw Organic Honey 500g** | GreenValley | 190.00 EGP | `UtcNow + 300d` | 25 | `LOW` |

---

### Scenario 5: 🟣 Historical Episodes & Sales Data (Dashboard Analytics & RAG Demo)
*Showcase: Past completed products with price histories and sales orders to populate dashboard charts and AI training metrics.*

| # | Product Title | Original Price | Sale History | Sold Units | Ingested Episode | Outcome |
| :- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Artisan Sourdough Loaf** | 60.00 EGP | 60.00 $\rightarrow$ 51.00 EGP (15% off) | 18 units sold | `ep-sourdough-01` | **100% Waste Averted** |
| 2 | **Vanilla Custard Danish** | 45.00 EGP | 45.00 $\rightarrow$ 38.25 EGP (15% off) | 12 units sold | `ep-danish-01` | **100% Waste Averted** |
| 3 | **Pasteurized Skim Milk 1L** | 40.00 EGP | 40.00 $\rightarrow$ 34.00 EGP (15% off) | 25 units sold | `ep-milk-01` | **100% Waste Averted** |
| 4 | **Greek Yogurt Strawberry 150g** | 50.00 EGP | 50.00 $\rightarrow$ 42.50 EGP (15% off) | 15 units sold | `ep-yogurt-01` | **100% Waste Averted** |

---

## 4. 🛒 Order Lifecycles & Customer Transactions

| Order # | Customer | Store | Items | Total Amount | Payment Method | Status | Verification / Action |
| :- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **ORD-101** | `customer.vip` | Le Pain Doré | French Croissants x2, Blueberry Cheesecake x1 | 219.13 EGP | **Wallet** (`Paid`) | `ReadyForPickup` | Shows **Pickup Verification PIN / QR** for the merchant to scan. |
| **ORD-102** | `customer.vip` | FreshMarket | Beef Salami x1, Fresh Eggs x1 | 242.00 EGP | **Paymob Card** (`Paid`) | `Completed` | Completed yesterday; displays customer 5-star review. |
| **ORD-103** | `customer.explorer` | GreenValley | Mystery Veggie Basket x1 | 60.00 EGP | **Wallet** (`Paid`) | `PendingPayment` | Demonstrates pending checkout cart flow. |

---

## 5. 🔔 In-App Notifications & Real-Time Inbox

| Recipient | Type | Title | Content | Timestamp |
| :--- | :--- | :--- | :--- | :--- |
| `merchant.assisted` | **AI Pricing Alert** | *"5 Pending AI Recommendations"* | *"AI has generated 5 price recommendations for near-expiry bakery items."* | `UtcNow - 10m` |
| `customer.vip` | **Order Ready** | *"Order #ORD-101 is Ready for Pickup!"* | *"Your order from Le Pain Doré Bakery is packed and ready at Zamalek branch."* | `UtcNow - 5m` |
| `customer.vip` | **Price Drop Alert** | *"Price Dropped on French Croissants!"* | *"French Croissants are now 15% off at Le Pain Doré near you."* | `UtcNow - 1h` |
| `admin@foodloop.com` | **System Notice** | *"AI Cycle Complete"* | *"Pricing batch cycle executed successfully for 4 stores."* | `UtcNow - 30m` |

---

## 6. 🛠️ How to Execute When Ready

When you are ready to generate this data, simply instruct:
> *"Seed the demo showcase data now"*

The automated seeder will wipe existing database tables and populate this exact dataset in seconds!
