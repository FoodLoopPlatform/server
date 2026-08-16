using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Queries;

public class GetOrderTrackingQueryHandler : IRequestHandler<GetOrderTrackingQuery, OrderTrackingDto>
{
    private readonly ApplicationDbContext _db;

    public GetOrderTrackingQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<OrderTrackingDto> Handle(GetOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // Access check: Only the consumer who placed the order or the merchant owning items in the order can retrieve it
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.UserId && !o.IsDeleted, cancellationToken);

        var isOwner = order.UserId == request.UserId;
        var isMerchantOfOrder = org != null && order.Items.Any(i => i.Product!.OrganizationId == org.Id);

        if (!isOwner && !isMerchantOfOrder)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this order.");
        }

        var storeName = order.Items.FirstOrDefault()?.Product?.Organization?.Name ?? "Store";
        var storeLogo = order.Items.FirstOrDefault()?.Product?.Organization?.Logo;

        // Build the pipeline steps based on current status
        var allStatuses = new[]
        {
            OrderStatus.Pending, OrderStatus.Confirmed,
            OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Completed
        };

        int currentIndex = Array.IndexOf(allStatuses, order.OrderStatus);

        var steps = allStatuses.Select((s, i) => new TrackingStepDto
        {
            Status = s.ToString(),
            Label = s switch
            {
                OrderStatus.Pending        => "Order Placed",
                OrderStatus.Confirmed      => "Order Confirmed",
                OrderStatus.Preparing      => "Being Prepared",
                OrderStatus.ReadyForPickup => "Ready for Pickup",
                OrderStatus.Completed      => "Completed",
                _                          => s.ToString()
            },
            Completed = i <= currentIndex && order.OrderStatus != OrderStatus.Cancelled,
            CompletedAt = i <= currentIndex ? order.UpdatedAt ?? order.CreatedAt : null
        }).ToList();

        return new OrderTrackingDto
        {
            OrderId = order.Id,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            TotalAmount = order.TotalAmount,
            PlacedAt = order.CreatedAt,
            LastUpdatedAt = order.UpdatedAt,
            StoreName = storeName,
            StoreLogo = storeLogo,
            Steps = steps,
            Items = order.Items.Select(i => new OrderTrackingItemDto
            {
                ProductTitle = i.Product?.Title ?? "Product",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
    }
}
