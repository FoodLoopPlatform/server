using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("payments")]
[AllowAnonymous]
public class PaymentsController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IApplicationDbContext db, IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _db = db;
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// GET /payments/paymob-callback — handles client redirection from Paymob checkout.
    /// </summary>
    [HttpGet("paymob-callback")]
    public async Task<IActionResult> PaymobRedirectCallback(
        [FromQuery] string? success = null,
        [FromQuery] string? id = null,
        [FromQuery(Name = "amount_cents")] string? amountCents = null,
        [FromQuery(Name = "merchant_order_id")] string? merchantOrderId = null,
        [FromQuery(Name = "special_reference")] string? specialReference = null,
        [FromQuery(Name = "order")] string? orderParam = null,
        [FromQuery(Name = "orderId")] string? orderIdQuery = null,
        [FromQuery] string? hmac = null,
        CancellationToken cancellationToken = default)
    {
        var isSuccess = string.Equals(success, "true", StringComparison.OrdinalIgnoreCase);
        var orderIdStr = !string.IsNullOrWhiteSpace(orderIdQuery) ? orderIdQuery
                       : !string.IsNullOrWhiteSpace(merchantOrderId) ? merchantOrderId
                       : !string.IsNullOrWhiteSpace(specialReference) ? specialReference
                       : orderParam;

        Guid? targetOrderId = null;
        if (Guid.TryParse(orderIdStr, out var parsedGuid))
        {
            targetOrderId = parsedGuid;
        }
        else if (!string.IsNullOrWhiteSpace(id))
        {
            // If redirect query doesn't contain our Guid, query Paymob Transaction API using the transaction id
            var tx = await _paymentService.GetTransactionDetailsAsync(id, cancellationToken);
            if (tx != null)
            {
                isSuccess = tx.IsSuccess;
                if (!string.IsNullOrWhiteSpace(tx.SpecialReference) && Guid.TryParse(tx.SpecialReference, out var txGuid))
                {
                    targetOrderId = txGuid;
                }
            }
        }

        if (targetOrderId.HasValue)
        {
            var order = await _db.Orders
                .Include(o => o.Payment)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Organization)
                .FirstOrDefaultAsync(o => o.Id == targetOrderId.Value, cancellationToken);

            if (order != null && isSuccess && order.PaymentStatus != PaymentStatus.Paid)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Confirmed;
                order.UpdatedAt = DateTimeOffset.UtcNow;

                if (order.Payment != null)
                {
                    order.Payment.Status = PaymentStatus.Paid;
                    order.Payment.TransactionReference = id ?? order.Payment.TransactionReference;
                    order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    var newPayment = new Payment
                    {
                        OrderId = order.Id,
                        Amount = order.TotalAmount,
                        Method = "Paymob",
                        Status = PaymentStatus.Paid,
                        TransactionReference = id ?? string.Empty,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.Payments.Add(newPayment);
                    order.Payment = newPayment;
                }

                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Order {OrderId} marked as Paid via Paymob Redirect Callback.", order.Id);

                // Send real-time notifications after successful payment
                try
                {
                    var notificationService = HttpContext.RequestServices
                        .GetService(typeof(Application.Common.Interfaces.IRealTimeNotificationService))
                        as Application.Common.Interfaces.IRealTimeNotificationService;

                    if (notificationService != null)
                    {
                        // Notify customer
                        await notificationService.SendNotificationToUserAsync(
                            order.UserId,
                            "NotifOrderConfirmedTitle",
                            "NotifOrderConfirmedBody",
                            "OrderConfirmed",
                            Array.Empty<object>(),
                            cancellationToken);

                        // Notify merchant(s)
                        var merchantUserIds = order.Items
                            .Select(i => i.Product?.Organization?.OwnerId)
                            .Where(oid => oid.HasValue && oid.Value != Guid.Empty)
                            .Select(oid => oid!.Value)
                            .Distinct();

                        foreach (var merchantUserId in merchantUserIds)
                        {
                            await notificationService.SendNotificationToUserAsync(
                                merchantUserId,
                                "NotifOrderReceivedTitle",
                                "NotifOrderReceivedBody",
                                "OrderReceived",
                                Array.Empty<object>(),
                                cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Notification failures must not break the payment confirmation flow
                    _logger.LogError(ex, "Failed to send post-payment notifications for Order {OrderId}.", order.Id);
                }
            }
        }

        return Ok(new
        {
            status = isSuccess ? "success" : "failed",
            message = isSuccess ? "Payment completed successfully." : "Payment failed or cancelled.",
            orderId = targetOrderId?.ToString() ?? orderIdStr,
            transactionId = id
        });
    }

    /// <summary>
    /// POST /payments/verify/{orderId} — verify or sync Paymob payment state for an order.
    /// Can be called by client right after Paymob WebView completes.
    /// </summary>
    [HttpPost("verify/{orderId:guid}")]
    public async Task<IActionResult> VerifyOrderPayment(
        Guid orderId,
        [FromBody] VerifyPaymentCallbackRequest? request,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            return NotFound(ApiResponse<string>.Fail("Order not found."));
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                orderId = order.Id,
                paymentStatus = order.PaymentStatus.ToString(),
                orderStatus = order.OrderStatus.ToString(),
                transactionReference = order.Payment?.TransactionReference
            }, "Order is already marked as Paid."));
        }

        var txId = request?.TransactionId?.Trim();
        bool paymentVerified = false;

        if (!string.IsNullOrWhiteSpace(txId))
        {
            var tx = await _paymentService.GetTransactionDetailsAsync(txId, cancellationToken);
            if (tx != null)
            {
                paymentVerified = tx.IsSuccess;
            }
            else
            {
                // Fallback: If client provides transaction ID from successful redirect
                paymentVerified = true;
            }
        }
        else
        {
            // If no transaction ID is sent, check if payment record exists with reference
            if (order.Payment != null && !string.IsNullOrWhiteSpace(order.Payment.TransactionReference))
            {
                var tx = await _paymentService.GetTransactionDetailsAsync(order.Payment.TransactionReference, cancellationToken);
                if (tx != null && tx.IsSuccess)
                {
                    paymentVerified = true;
                    txId = order.Payment.TransactionReference;
                }
            }
        }

        if (paymentVerified)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Confirmed;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Paid;
                if (!string.IsNullOrWhiteSpace(txId))
                {
                    order.Payment.TransactionReference = txId;
                }
                order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var newPayment = new Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    Method = "Paymob",
                    Status = PaymentStatus.Paid,
                    TransactionReference = txId ?? string.Empty,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.Payments.Add(newPayment);
                order.Payment = newPayment;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Order {OrderId} successfully verified and marked as Paid.", order.Id);

            return Ok(ApiResponse<object>.Ok(new
            {
                orderId = order.Id,
                paymentStatus = order.PaymentStatus.ToString(),
                orderStatus = order.OrderStatus.ToString(),
                transactionReference = order.Payment?.TransactionReference
            }, "Payment verified and order confirmed successfully."));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            orderId = order.Id,
            paymentStatus = order.PaymentStatus.ToString(),
            orderStatus = order.OrderStatus.ToString()
        }, "Payment is still pending verification."));
    }

    /// <summary>
    /// POST /payments/paymob-callback — public webhook callback for Paymob transaction updates.
    /// </summary>
    [HttpPost("paymob-callback")]
    public async Task<IActionResult> PaymobCallback([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            JsonElement obj;
            if (payload.TryGetProperty("obj", out var objProp) && objProp.ValueKind == JsonValueKind.Object)
            {
                obj = objProp;
            }
            else if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("amount_cents", out _))
            {
                obj = payload;
            }
            else
            {
                return BadRequest("Invalid payload structure.");
            }

            var queryHmac = Request?.Query["hmac"].ToString();
            var hmacReceived = !string.IsNullOrEmpty(queryHmac)
                ? queryHmac
                : (payload.TryGetProperty("hmac", out var hmacElement) ? (hmacElement.GetString() ?? string.Empty) : string.Empty);

            // Extract fields for HMAC calculation
            if (!obj.TryGetProperty("amount_cents", out var amountCentsElement) ||
                !obj.TryGetProperty("id", out var idProperty) ||
                !obj.TryGetProperty("success", out var successElement))
            {
                return BadRequest("Invalid payload structure.");
            }

            var amountCents = amountCentsElement.GetRawText();
            var createdAt = obj.TryGetProperty("created_at", out var ca) ? (ca.GetString() ?? "") : "";
            var currency = obj.TryGetProperty("currency", out var curr) ? (curr.GetString() ?? "") : "";
            var errorOccured = obj.TryGetProperty("error_occured", out var eo) ? eo.GetRawText() : "false";
            var hasParentTransaction = obj.TryGetProperty("has_parent_transaction", out var hpt) ? hpt.GetRawText() : "false";
            
            var id = idProperty.ValueKind == JsonValueKind.Number 
                ? idProperty.GetInt64().ToString() 
                : idProperty.GetString() ?? idProperty.GetRawText();

            var integrationId = "";
            if (obj.TryGetProperty("integration_id", out var integrationIdProperty))
            {
                integrationId = integrationIdProperty.ValueKind == JsonValueKind.Number
                    ? integrationIdProperty.GetInt64().ToString()
                    : integrationIdProperty.GetString() ?? integrationIdProperty.GetRawText();
            }

            var is3dSecure = obj.TryGetProperty("is_3d_secure", out var i3d) ? i3d.GetRawText() : "false";
            var isAuth = obj.TryGetProperty("is_auth", out var ia) ? ia.GetRawText() : "false";
            var isCapture = obj.TryGetProperty("is_capture", out var ic) ? ic.GetRawText() : "false";
            var isVoided = obj.TryGetProperty("is_voided", out var iv) ? iv.GetRawText() : "false";
            var isRefunded = obj.TryGetProperty("is_refunded", out var ir) ? ir.GetRawText() : "false";
            var pending = obj.TryGetProperty("pending", out var pnd) ? pnd.GetRawText() : "false";

            var pan = "";
            var subType = "";
            var type = "";
            if (obj.TryGetProperty("source_data", out var sourceData) && sourceData.ValueKind == JsonValueKind.Object)
            {
                pan = sourceData.TryGetProperty("pan", out var panProp) ? (panProp.GetString() ?? "") : "";
                subType = sourceData.TryGetProperty("sub_type", out var subProp) ? (subProp.GetString() ?? "") : "";
                type = sourceData.TryGetProperty("type", out var typeProp) ? (typeProp.GetString() ?? "") : "";
            }

            var success = successElement.GetRawText();

            // Construct concatenation
            var hmacConcat = $"{amountCents}{createdAt}{currency}{errorOccured}{hasParentTransaction}{id}{integrationId}{is3dSecure}{isAuth}{isCapture}{isVoided}{isRefunded}{pending}{pan}{subType}{type}{success}";

            // Verify HMAC
            if (!_paymentService.VerifyHmac(hmacConcat, hmacReceived))
            {
                return Unauthorized("HMAC verification failed.");
            }

            // Check if transaction is successful
            var isSuccess = successElement.ValueKind == JsonValueKind.True || (successElement.ValueKind == JsonValueKind.String && bool.TryParse(successElement.GetString(), out var sb) && sb);
            var orderId = ExtractOrderId(obj, payload);

            if (isSuccess)
            {
                // Layer 1 Idempotency Check
                var alreadyProcessed = await _db.Payments
                    .AnyAsync(p => p.TransactionReference == id, cancellationToken);
                if (alreadyProcessed)
                {
                    _logger.LogInformation("Webhook callback already processed for transaction reference {TransactionId}.", id);
                    return Ok(new { status = "success", message = "Transaction already processed." });
                }

                if (orderId.HasValue)
                {
                    var order = await _db.Orders
                        .Include(o => o.Payment)
                        .FirstOrDefaultAsync(o => o.Id == orderId.Value, cancellationToken);

                    if (order != null)
                    {
                        // Verify amount: Paymob amount_cents is in EGP cents.
                        // Use Math.Round to avoid floating-point drift (e.g. 4999 cents = 49.99 EGP).
                        if (!decimal.TryParse(amountCents, out var parsedAmountCents))
                        {
                            return BadRequest("Invalid amount format in callback.");
                        }
                        var callbackAmount = Math.Round(parsedAmountCents / 100.0m, 2);
                        var orderAmount = Math.Round(order.TotalAmount, 2);
                        if (orderAmount != callbackAmount)
                        {
                            _logger.LogWarning("Paymob callback amount mismatch: callback={CallbackAmount} EGP, order={OrderAmount} EGP for Order {OrderId}", callbackAmount, order.TotalAmount, order.Id);
                            return BadRequest("Amount mismatch.");
                        }

                        if (order.PaymentStatus != PaymentStatus.Paid)
                        {
                            order.PaymentStatus = PaymentStatus.Paid;
                            order.OrderStatus = OrderStatus.Confirmed; // Auto-confirm on payment success
                            order.UpdatedAt = DateTimeOffset.UtcNow;

                            if (order.Payment != null)
                            {
                                order.Payment.Status = PaymentStatus.Paid;
                                order.Payment.TransactionReference = id; // Paymob transaction ID
                                order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
                            }
                            else
                            {
                                var newPayment = new Payment
                                {
                                    OrderId = order.Id,
                                    Amount = order.TotalAmount,
                                    Method = "Paymob",
                                    Status = PaymentStatus.Paid,
                                    TransactionReference = id,
                                    CreatedAt = DateTimeOffset.UtcNow,
                                    UpdatedAt = DateTimeOffset.UtcNow
                                };
                                _db.Payments.Add(newPayment);
                                order.Payment = newPayment;
                            }

                            // Layer 2 Idempotency: Catch DbUpdateException from unique index violation on TransactionReference
                            try
                            {
                                await _db.SaveChangesAsync(cancellationToken);
                            }
                            catch (DbUpdateException)
                            {
                                var processedConcurrently = await _db.Payments
                                    .AnyAsync(p => p.TransactionReference == id, cancellationToken);
                                if (processedConcurrently)
                                {
                                    _logger.LogInformation("Transaction {TransactionId} was processed concurrently by another request.", id);
                                    return Ok(new { status = "success", message = "Transaction already processed concurrently." });
                                }
                                throw; // Re-throw other database update errors
                            }
                        }
                    }
                }
            }
            else
            {
                if (orderId.HasValue)
                {
                    var order = await _db.Orders
                        .Include(o => o.Payment)
                        .FirstOrDefaultAsync(o => o.Id == orderId.Value, cancellationToken);
                    if (order != null && order.PaymentStatus != PaymentStatus.Paid)
                    {
                        order.PaymentStatus = PaymentStatus.Failed;
                        order.UpdatedAt = DateTimeOffset.UtcNow;

                        if (order.Payment != null)
                        {
                            order.Payment.Status = PaymentStatus.Failed;
                            order.Payment.TransactionReference = id;
                            order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
                        }
                        else
                        {
                            var newPayment = new Payment
                            {
                                OrderId = order.Id,
                                Amount = order.TotalAmount,
                                Method = "Paymob",
                                Status = PaymentStatus.Failed,
                                TransactionReference = id,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                            };
                            _db.Payments.Add(newPayment);
                            order.Payment = newPayment;
                        }

                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    private static Guid? ExtractOrderId(JsonElement obj, JsonElement root)
    {
        // 1. Check obj.order.merchant_order_id
        if (obj.TryGetProperty("order", out var orderProp) && orderProp.ValueKind == JsonValueKind.Object)
        {
            if (orderProp.TryGetProperty("merchant_order_id", out var mIdProp))
            {
                var mIdStr = mIdProp.ValueKind == JsonValueKind.String ? mIdProp.GetString() : mIdProp.GetRawText();
                if (!string.IsNullOrWhiteSpace(mIdStr) && Guid.TryParse(mIdStr.Trim('\"', ' '), out var g))
                    return g;
            }

            if (orderProp.TryGetProperty("special_reference", out var sRefProp))
            {
                var sRefStr = sRefProp.ValueKind == JsonValueKind.String ? sRefProp.GetString() : sRefProp.GetRawText();
                if (!string.IsNullOrWhiteSpace(sRefStr) && Guid.TryParse(sRefStr.Trim('\"', ' '), out var g))
                    return g;
            }
        }

        // 2. Check obj.special_reference
        if (obj.TryGetProperty("special_reference", out var objSpecialRef))
        {
            var sStr = objSpecialRef.ValueKind == JsonValueKind.String ? objSpecialRef.GetString() : objSpecialRef.GetRawText();
            if (!string.IsNullOrWhiteSpace(sStr) && Guid.TryParse(sStr.Trim('\"', ' '), out var g))
                return g;
        }

        // 3. Check obj.merchant_order_id
        if (obj.TryGetProperty("merchant_order_id", out var objMerchantOrderId))
        {
            var mStr = objMerchantOrderId.ValueKind == JsonValueKind.String ? objMerchantOrderId.GetString() : objMerchantOrderId.GetRawText();
            if (!string.IsNullOrWhiteSpace(mStr) && Guid.TryParse(mStr.Trim('\"', ' '), out var g))
                return g;
        }

        // 4. Check obj.extras.merchant_order_id
        if (obj.TryGetProperty("extras", out var extrasProp) && extrasProp.ValueKind == JsonValueKind.Object)
        {
            if (extrasProp.TryGetProperty("merchant_order_id", out var exMid))
            {
                var exStr = exMid.ValueKind == JsonValueKind.String ? exMid.GetString() : exMid.GetRawText();
                if (!string.IsNullOrWhiteSpace(exStr) && Guid.TryParse(exStr.Trim('\"', ' '), out var g))
                    return g;
            }
        }

        // 5. Check root special_reference or merchant_order_id
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("special_reference", out var rootSRef))
            {
                var rStr = rootSRef.ValueKind == JsonValueKind.String ? rootSRef.GetString() : rootSRef.GetRawText();
                if (!string.IsNullOrWhiteSpace(rStr) && Guid.TryParse(rStr.Trim('\"', ' '), out var g))
                    return g;
            }
            if (root.TryGetProperty("merchant_order_id", out var rootMid))
            {
                var rStr = rootMid.ValueKind == JsonValueKind.String ? rootMid.GetString() : rootMid.GetRawText();
                if (!string.IsNullOrWhiteSpace(rStr) && Guid.TryParse(rStr.Trim('\"', ' '), out var g))
                    return g;
            }
        }

        return null;
    }
}

public class VerifyPaymentCallbackRequest
{
    public string? TransactionId { get; set; }
}

