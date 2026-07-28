# AI Role

FoodLoop's AI sits between the physical product and the digital marketplace. Its core job is to read product labels, extract expiry dates, classify products, and recommend discounts — reducing the manual work merchants have to do while keeping expired or misclassified items off the platform.

---

## What the AI Does

### 1. OCR — Read Product Labels (Sprint 2)

When a merchant uploads a product image, the AI pipeline:

1. Runs OCR on the image to extract all visible text from the label.
2. Passes the text to GPT Vision for structured extraction:
   - Product name
   - Expiry date
   - Category hint
3. Stores the result in `AIRecognitionResults`:
   - `DetectedProduct` — identified product name
   - `ExtractedText` — raw OCR output (kept for auditing)
   - `ExtractedExpiryDate` — parsed expiry date
   - `ConfidenceScore` — 0.0–1.0 indicating how certain the AI is

### 2. Product Classification (Sprint 2)

Based on the extracted text and image, the AI suggests which `Category` the product belongs to (Dairy, Bakery, Produce, etc.). This pre-fills the category field in the product form, saving the merchant from picking manually.

### 3. Discount Calculation (Sprint 3)

The AI calculates a recommended `DiscountedPrice` for each listing using a rule-based pricing model that factors in:

- **Days until expiry** — the closer to the expiry date, the deeper the discount
- **Original price** — percentage-based calculation
- **Category norms** — some categories (e.g. fresh produce) depreciate faster than others
- **Historical sell-through rates** — if a product type consistently sells slowly, the AI recommends an earlier and deeper discount

The formula follows a decay curve: a product 7 days from expiry might get 20% off, while a product 1 day from expiry gets 60%+ off.

### 4. Scheduled Repricing (Sprint 4)

A background job runs daily to re-evaluate all active listings. As `ExpirationDate` approaches, prices are adjusted downward automatically according to the decay curve. This keeps pricing accurate without merchants having to update listings manually.

### 5. Performance Monitoring (Sprint 5)

The AI tracks its own accuracy:
- Compares `ExtractedExpiryDate` against the date the merchant confirmed
- Tracks how often its category suggestions are accepted vs. corrected
- Surfaces metrics in the admin analytics dashboard for team review and model improvement

---

## What the AI Can Update Autonomously

These actions happen without human intervention when `ConfidenceScore` is above a defined threshold:

| Action | Trigger | Condition |
|---|---|---|
| Set `ProductListing.ExpirationDate` | After OCR | `ConfidenceScore >= threshold` |
| Set `ProductListing.DiscountedPrice` | After expiry extraction | Expiry date is confirmed |
| Reprice an existing listing | Daily background job | Listing is still active |
| Pre-fill category suggestion | After classification | Always (suggestion, not forced) |

The confidence threshold is configurable. In early sprints it is set conservatively so more items go through human review while the model is being evaluated.

---

## What Requires Human Review

These actions are queued for review when confidence is below the threshold, or always by policy:

| Action | Who reviews | Screen |
|---|---|---|
| Low-confidence OCR result (`ConfidenceScore < threshold`) | Admin | Moderation queue (`moderation_queue_active_state`) |
| Expiry date could not be extracted | Merchant | Product creation form (manual entry) |
| Category suggestion rejected by merchant | Merchant | Product form |
| Product flagged by a user report | Admin | Moderation queue |
| Duplicate listing detection | Admin | Moderation queue |
| Missing or expired health permit | Admin | Moderation queue |
| Store document verification | Admin | `GET /admin/stores/pending` → `PATCH /admin/stores/{id}/verify` |

The moderation queue shows the AI's confidence score on each card so the admin can triage quickly: high-confidence items get approved in seconds, low-confidence items get careful review.

---

## The `AIRecognitionResult` Record

Every OCR job produces one `AIRecognitionResult` linked to a `ProductListing` (1-to-1):

```
AIRecognitionResult
  ListingId          → the product this result belongs to
  DetectedProduct    → "Labneh 500g" (from label)
  ExtractedText      → full raw OCR output
  ExtractedExpiryDate → 2026-08-14 (parsed from "Best Before 14/08/2026")
  ConfidenceScore    → 0.87
  Reviewed           → false (until an admin or merchant confirms it)
```

When `Reviewed = true` it means a human has confirmed the AI's output and it can be trusted for pricing calculations.

---

## Data Flow: Image → Listed Product

```
Merchant uploads photo
        ↓
AI Pipeline (OCR + GPT Vision)
        ↓
AIRecognitionResult stored
        ↓
ConfidenceScore >= threshold?
  ├─ YES → ExpirationDate auto-filled
  │         DiscountedPrice calculated
  │         Listing published
  └─ NO  → Queued in moderation
             Admin reviews
             Admin approves/rejects
                    ↓
             Listing published (or rejected)
```

---

## Pricing Formula (Rule-Based, Sprint 3)

The discount is a function of days remaining until expiry:

```
daysLeft = ExpirationDate - today

if daysLeft > 7:
    discount = 10%
elif daysLeft > 3:
    discount = 25%
elif daysLeft > 1:
    discount = 45%
else:
    discount = 60%+

DiscountedPrice = OriginalPrice * (1 - discount)
```

This is the initial rule-based version. Sprint 4 replaces it with a scheduled repricing job that re-evaluates every listing daily and applies the decay curve progressively rather than in fixed steps.

---

## What the AI Does NOT Do (boundaries)

- It does not directly place or cancel orders.
- It does not directly approve or reject stores — that is always admin-only.
- It does not set a price higher than `OriginalPrice`.
- It does not delete listings — it can flag them, but removal requires human action.
- It does not access user personal data for pricing decisions.
