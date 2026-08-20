using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Result<OrderDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IRealTimeNotificationService _notification;

    public UpdateOrderStatusCommandHandler(ApplicationDbContext db, IAuditLogService auditLog, IRealTimeNotificationService notification)
    {
        _db = db;
        _auditLog = auditLog;
        _notification = notification;
    }

    public async Task<Result<OrderDto>> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // Verify that the order belongs to this merchant's store items
        var hasItems = order.Items.Any(i => i.Product?.OrganizationId == org.Id);
        if (!hasItems)
        {
            return Result<OrderDto>.Fail("Unauthorized access to update this order.");
        }

        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var newStatus))
        {
            return Result<OrderDto>.Fail($"Invalid OrderStatus value: {request.Status}");
        }

        var oldStatus = order.OrderStatus;
        order.OrderStatus = newStatus;

        // If order is completed, mark Cash payment as Paid
        if (newStatus == OrderStatus.Completed && order.PaymentStatus != PaymentStatus.Paid)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Paid;
                order.Payment.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        // If order is cancelled, return products back to inventory
        if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                if (item.Product != null)
                {
                    item.Product.QuantityAvailable += item.Quantity;
                }
            }

            order.PaymentStatus = PaymentStatus.Refunded;
            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Refunded;
            }
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditLog.LogAsync(
            request.OwnerId,
            org.Id,
            "OrderStatusUpdated",
            "Order Status Updated",
            $"Order {order.Id} status set to {newStatus} by merchant.",
            null,
            cancellationToken);

        // Send Realtime Notification to Customer — each OrderStatus value has its own resx key.
        var (titleKey, bodyKey, notifType) = newStatus switch
        {
            OrderStatus.Pending        => ("NotifOrderPendingTitle",        "NotifOrderPendingBody",        "OrderPending"),
            OrderStatus.Confirmed      => ("NotifOrderConfirmedTitle",      "NotifOrderConfirmedBody",      "OrderConfirmed"),
            OrderStatus.Preparing      => ("NotifOrderPreparingTitle",      "NotifOrderPreparingBody",      "OrderPreparing"),
            OrderStatus.ReadyForPickup => ("NotifOrderReadyForPickupTitle", "NotifOrderReadyForPickupBody", "OrderReadyForPickup"),
            OrderStatus.Completed      => ("NotifOrderCompletedTitle",      "NotifOrderCompletedBody",      "OrderCompleted"),
            OrderStatus.Cancelled      => ("NotifOrderCancelledTitle",      "NotifOrderCancelledBody",      "OrderCancelled"),
            _                          => ("NotifOrderStatusGenericTitle",   "NotifOrderStatusGenericBody",   "OrderStatusUpdated")
        };

        await _notification.SendNotificationToUserAsync(
            order.UserId,
            titleKey,
            bodyKey,
            notifType,
            Array.Empty<object>(),
            cancellationToken);

        var user = await _db.Users.FindAsync(new object[] { order.UserId }, cancellationToken);

        return Result<OrderDto>.Ok(new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserFullName = user?.FullName ?? string.Empty,
            TotalAmount = order.TotalAmount,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            StoreId = order.Items.Select(i => i.Product?.OrganizationId).FirstOrDefault(id => id.HasValue),
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductTitle = i.Product?.Title ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                StoreId = i.Product?.OrganizationId ?? Guid.Empty
            }).ToList()
        });
    }
}
