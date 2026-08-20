using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class CashCheckoutCommandHandler : IRequestHandler<CashCheckoutCommand, CashCheckoutResultDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IRealTimeNotificationService _notification;

    public CashCheckoutCommandHandler(
        ApplicationDbContext db,
        IAuditLogService auditLog,
        IRealTimeNotificationService notification)
    {
        _db = db;
        _auditLog = auditLog;
        _notification = notification;
    }

    public async Task<CashCheckoutResultDto> Handle(CashCheckoutCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch the order including items and products
        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.Organization)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // 2. Authorization Check
        if (order.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You are not authorized to pay for this order.");
        }

        // 3. Status Checks
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new ConflictException("This order has already been paid.");
        }

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot checkout a cancelled order.");
        }

        if (order.TotalAmount <= 0)
        {
            throw new ArgumentException("Order amount must be greater than zero.");
        }

        // 4. Update Order and Payment state
        order.PaymentStatus = PaymentStatus.Pending;
        order.OrderStatus = OrderStatus.Confirmed; // Confirm order so store starts preparation
        order.UpdatedAt = DateTimeOffset.UtcNow;

        if (order.Payment == null)
        {
            order.Payment = new Payment
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = "Cash",
                Status = PaymentStatus.Pending,
                TransactionReference = $"CASH-{order.Id.ToString()[..8].ToUpper()}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Payments.Add(order.Payment);
        }
        else
        {
            order.Payment.Method = "Cash";
            order.Payment.Status = PaymentStatus.Pending;
            order.Payment.TransactionReference = $"CASH-{order.Id.ToString()[..8].ToUpper()}";
            order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
            _db.Payments.Update(order.Payment);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 5. Audit Log
        await _auditLog.LogAsync(
            request.UserId,
            null,
            "CashCheckout",
            "Cash Checkout Selected",
            $"Customer confirmed order {order.Id} with Cash on Pickup/Delivery ({order.TotalAmount:C}).",
            null,
            cancellationToken);

        // 6. Notify Merchant(s)
        var orgsToNotify = order.Items
            .Select(i => i.Product?.Organization)
            .Where(o => o != null && o.OwnerId != Guid.Empty)
            .GroupBy(o => o!.Id)
            .Select(g => g.First())
            .ToList();

        foreach (var org in orgsToNotify)
        {
            await _notification.SendNotificationToUserAsync(
                org!.OwnerId,
                "NotifOrderConfirmedTitle",
                "NotifOrderConfirmedBody",
                "OrderConfirmed",
                new object[] { order.Id.ToString().Substring(0, 8) },
                cancellationToken);
        }

        return new CashCheckoutResultDto
        {
            OrderId = order.Id,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            PaymentMethod = "Cash",
            AmountDue = order.TotalAmount,
            Message = "Order confirmed. Please pay in cash upon pickup or delivery."
        };
    }
}
