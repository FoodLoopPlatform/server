using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Queries;

public class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, OrderDto>
{
    private readonly ApplicationDbContext _db;

    public GetOrderDetailQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OrderDto> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
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

        var customer = await _db.Users.FindAsync(new object[] { order.UserId }, cancellationToken);

        return new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserFullName = customer?.FullName ?? string.Empty,
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
        };
    }
}
