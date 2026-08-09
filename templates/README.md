# FoodLoop Import & Bulk Upload Templates

This directory contains standard sample and import template files for bulk operations in the FoodLoop platform.

---

## 📂 Available Templates

### 1. CSV Bulk Product Upload
Located in [`templates/csv/`](./csv/):

* **[`products_bulk_template_en.csv`](./csv/products_bulk_template_en.csv)**: Standard English headers and English category names.
* **[`products_bulk_template_ar.csv`](./csv/products_bulk_template_ar.csv)**: Arabic product titles, descriptions, and Arabic category names with standard CSV headers.

---

## 📋 CSV Format Specifications

### Header Columns (Case-Insensitive)

| Column Header | Required? | Type | Allowed Values / Format | Description |
| :--- | :---: | :---: | :--- | :--- |
| **`title`** | **Yes** | String | Any text | Product title/name. |
| **`description`** | No | String | Any text | Optional description of the item. |
| **`originalPrice`** | **Yes** | Decimal | $\ge 0$ | Original retail price (e.g. `45.00`). |
| **`discountedPrice`** | **Yes** | Decimal | $\ge 0$, $\le \text{originalPrice}$ | Surplus discounted selling price (e.g. `22.50`). |
| **`quantityAvailable`** | **Yes** | Integer | $\ge 0$ | Number of available units (e.g. `10`). |
| **`expirationDate`** | **Yes** | Date | `YYYY-MM-DD` | Expiration date of the item (e.g. `2026-08-20`). |
| **`categoryName`** | **Yes** | String | Valid Category Name (EN or AR) | Must match an existing category (e.g. `Bakery`, `Dairy`, `مخبوزات`, etc.). |

---

## 🚀 API Usage

### Upload Endpoint
* **Route**: `POST /stores/me/products/bulk`
* **Authorization**: `Bearer <MERCHANT_TOKEN>` (Must belong to a verified merchant)
* **Content-Type**: `multipart/form-data`
* **Form Field**: `File`

### Example cURL Command
```bash
curl -X POST "https://web-nine-ivory-36.vercel.app/api/stores/me/products/bulk" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -F "File=@templates/csv/products_bulk_template_en.csv"
```
