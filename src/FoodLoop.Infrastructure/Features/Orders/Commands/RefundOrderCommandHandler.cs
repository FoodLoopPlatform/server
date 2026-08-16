using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class RefundOrderCommandHandler : IRequestHandler<RefundOrderCommand, OrderDto>
{
    private readonly ApplicationDbContext _db;
    private readonly FoodLoop.Application.Common.Interfaces.IAuditLogService _auditLogService;

    public RefundOrderCommandHandler(ApplicationDbContext db, FoodLoop.Application.Common.Interfaces.IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    public async Task<OrderDto> Handle(RefundOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Refund amount must be positive.");
        }

        // 1. Fetch the order
        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // 2. Verify that this order belongs to the merchant's store
        var store = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OwnerId == request.MerchantUserId && !o.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Merchant organization not found.");

        var belongsToStore = order.Items.Any(i => i.Product != null && i.Product.OrganizationId == store.Id);
        if (!belongsToStore)
        {
            throw new UnauthorizedAccessException("You are not authorized to refund this order as it does not belong to your store.");
        }

        // 3. Safety Check: Verify refund amount does not exceed the order's total amount
        if (request.Amount > order.TotalAmount)
        {
            throw new InvalidOperationException($"Cannot refund {request.Amount:F2} EGP. The total amount of the order is only {order.TotalAmount:F2} EGP.");
        }

        // 4. Update the Customer's wallet balance
        var customer = await _db.Users.FindAsync(new object[] { order.UserId }, cancellationToken)
            ?? throw new NotFoundException("Customer", order.UserId);

        customer.WalletBalance += request.Amount;

        // 5. Create a WalletTransaction record
        var transaction = new WalletTransaction
        {
            UserId = order.UserId,
            Amount = request.Amount,
            Type = "Refund",
            ReferenceId = order.Id.ToString(),
            Description = $"Refund for Order #{order.Id.ToString()[..8].ToUpper()}: {request.Reason}"
        };
        _db.WalletTransactions.Add(transaction);

        // 6. Update order details if fully refunded
        if (request.Amount == order.TotalAmount)
        {
            order.PaymentStatus = FoodLoop.Domain.Enums.PaymentStatus.Refunded;
            order.OrderStatus = FoodLoop.Domain.Enums.OrderStatus.Cancelled;
            _db.Orders.Update(order);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 7. Audit Log
        await _auditLogService.LogAsync(
            request.MerchantUserId,
            store.Id,
            "OrderRefunded",
            "Order Refunded",
            $"Store owner refunded {request.Amount:F2} EGP for order '{order.Id}'. Reason: {request.Reason}",
            null,
            cancellationToken);

        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserFullName = customer.FullName,
            TotalAmount = order.TotalAmount,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            StoreId = store.Id,
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductTitle = i.Product?.Title ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                StoreId = i.Product?.OrganizationId ?? Guid.Empty
            }).ToList()
        };
    }
}
