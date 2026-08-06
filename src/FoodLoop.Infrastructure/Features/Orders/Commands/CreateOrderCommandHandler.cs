using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IRealTimeNotificationService _notification;

    public CreateOrderCommandHandler(ApplicationDbContext db, IAuditLogService auditLog, IRealTimeNotificationService notification)
    {
        _db = db;
        _auditLog = auditLog;
        _notification = notification;
    }

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        if (user.Status != UserStatus.Active)
        {
            return Result<OrderDto>.Fail("Your account must be active to place orders.");
        }

        if (request.Items == null || !request.Items.Any())
        {
            return Result<OrderDto>.Fail("Cannot checkout an empty cart.");
        }

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Include(p => p.Organization)
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var order = new Order
        {
            UserId = request.UserId,
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending
        };

        decimal totalAmount = 0;
        var organizationsToLog = new HashSet<Guid>();

        foreach (var itemRequest in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == itemRequest.ProductId);
            if (product == null)
            {
                return Result<OrderDto>.Fail($"Product with ID {itemRequest.ProductId} not found.");
            }

            if (product.Status != ProductStatus.Active || product.ExpirationDate < today)
            {
                return Result<OrderDto>.Fail($"Product '{product.Title}' is no longer active or has expired.");
            }

            if (product.QuantityAvailable < itemRequest.Quantity)
            {
                return Result<OrderDto>.Fail($"Insufficient stock for product '{product.Title}'. Available: {product.QuantityAvailable}. Requested: {itemRequest.Quantity}.");
            }

            // Deduct stock
            product.QuantityAvailable -= itemRequest.Quantity;

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = itemRequest.Quantity,
                UnitPrice = product.DiscountedPrice
            };
            order.Items.Add(orderItem);

            totalAmount += product.DiscountedPrice * itemRequest.Quantity;

            if (product.OrganizationId != Guid.Empty)
            {
                organizationsToLog.Add(product.OrganizationId);
            }
        }

        order.TotalAmount = totalAmount;

        // Auto-simulate payment paid for simplicity in MVP
        order.PaymentStatus = PaymentStatus.Paid;
        order.Payment = new Payment
        {
            OrderId = order.Id,
            Amount = totalAmount,
            Method = "CreditCard",
            TransactionReference = Guid.NewGuid().ToString(),
            Status = PaymentStatus.Paid
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        // 1. Consumer side
        await _auditLog.LogAsync(
            request.UserId,
            null,
            "OrderPlaced",
            "Order Placed",
            $"Placed order {order.Id} with total amount {totalAmount:C}.",
            request.IpAddress,
            cancellationToken);

        await _notification.SendNotificationToUserAsync(
            request.UserId,
            "Order Placed Successfully",
            $"Your order #{order.Id.ToString().Substring(0, 8)} has been placed successfully.",
            "OrderPlaced",
            cancellationToken);

        // 2. Merchant side
        foreach (var orgId in organizationsToLog)
        {
            await _auditLog.LogAsync(
                null,
                orgId,
                "OrderReceived",
                "Order Received",
                $"Received new order {order.Id} for pickup.",
                request.IpAddress,
                cancellationToken);

            var org = products.FirstOrDefault(p => p.OrganizationId == orgId)?.Organization;
            if (org != null && org.OwnerId != Guid.Empty)
            {
                await _notification.SendNotificationToUserAsync(
                    org.OwnerId,
                    "New Order Received",
                    $"Store '{org.Name}' received order #{order.Id.ToString().Substring(0, 8)} for pickup.",
                    "OrderReceived",
                    cancellationToken);
            }
        }

        var responseDto = MapToDto(order, user.FullName);
        return Result<OrderDto>.Ok(responseDto);
    }

    private static OrderDto MapToDto(Order o, string userFullName)
    {
        return new OrderDto
        {
            Id = o.Id,
            UserId = o.UserId,
            UserFullName = userFullName,
            TotalAmount = o.TotalAmount,
            OrderStatus = o.OrderStatus.ToString(),
            PaymentStatus = o.PaymentStatus.ToString(),
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt,
            Items = o.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductTitle = i.Product?.Title ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
    }
}
