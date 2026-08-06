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

    public UpdateOrderStatusCommandHandler(ApplicationDbContext db, IAuditLogService auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    public async Task<Result<OrderDto>> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
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
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductTitle = i.Product?.Title ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        });
    }
}
