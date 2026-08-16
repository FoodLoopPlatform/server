using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public PaymentsController(IApplicationDbContext db, IPaymentService paymentService)
    {
        _db = db;
        _paymentService = paymentService;
    }

    /// <summary>
    /// POST /payments/paymob-callback — public webhook callback for Paymob transaction updates.
    /// </summary>
    [HttpPost("paymob-callback")]
    public async Task<IActionResult> PaymobCallback([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            if (!payload.TryGetProperty("obj", out var obj) || !payload.TryGetProperty("hmac", out var hmacElement))
            {
                return BadRequest("Invalid payload structure.");
            }

            var hmacReceived = hmacElement.GetString() ?? string.Empty;

            // Extract fields for HMAC calculation
            var amountCents = obj.GetProperty("amount_cents").GetRawText();
            var createdAt = obj.GetProperty("created_at").GetString() ?? "";
            var currency = obj.GetProperty("currency").GetString() ?? "";
            var errorOccured = obj.GetProperty("error_occured").GetRawText(); // "true" or "false"
            var hasParentTransaction = obj.GetProperty("has_parent_transaction").GetRawText();
            var id = obj.GetProperty("id").GetRawText();
            var integrationId = obj.GetProperty("integration_id").GetRawText();
            var is3dSecure = obj.GetProperty("is_3d_secure").GetRawText();
            var isAuth = obj.GetProperty("is_auth").GetRawText();
            var isCapture = obj.GetProperty("is_capture").GetRawText();
            var isVoided = obj.GetProperty("is_voided").GetRawText();
            var isRefunded = obj.GetProperty("is_refunded").GetRawText();
            var pending = obj.GetProperty("pending").GetRawText();

            var sourceData = obj.GetProperty("source_data");
            var pan = sourceData.GetProperty("pan").GetString() ?? "";
            var subType = sourceData.GetProperty("sub_type").GetString() ?? "";
            var type = sourceData.GetProperty("type").GetString() ?? "";

            var success = obj.GetProperty("success").GetRawText();

            // Construct concatenation
            var hmacConcat = $"{amountCents}{createdAt}{currency}{errorOccured}{hasParentTransaction}{id}{integrationId}{is3dSecure}{isAuth}{isCapture}{isVoided}{isRefunded}{pending}{pan}{subType}{type}{success}";

            // Verify HMAC
            if (!_paymentService.VerifyHmac(hmacConcat, hmacReceived))
            {
                return Unauthorized("HMAC verification failed.");
            }

            // Check if transaction is successful
            var isSuccess = obj.GetProperty("success").GetBoolean();
            if (isSuccess)
            {
                // Retrieve merchant_order_id
                var orderObj = obj.GetProperty("order");
                var merchantOrderIdStr = orderObj.GetProperty("merchant_order_id").GetString();

                if (Guid.TryParse(merchantOrderIdStr, out var orderId))
                {
                    var order = await _db.Orders
                        .Include(o => o.Payment)
                        .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
                    if (order != null && order.PaymentStatus != PaymentStatus.Paid)
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

                        _db.Orders.Update(order);
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
}
