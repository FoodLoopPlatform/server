using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Queries;

public class GetMerchantOrdersQueryHandler : IRequestHandler<GetMerchantOrdersQuery, IReadOnlyList<OrderDto>>
{
    private readonly ApplicationDbContext _db;

    public GetMerchantOrdersQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OrderDto>> Handle(GetMerchantOrdersQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var orders = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.Items.Any(i => i.Product!.OrganizationId == org.Id))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var userIds = orders.Select(o => o.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return orders.Select(o => new OrderDto
        {
            Id = o.Id,
            UserId = o.UserId,
            UserFullName = users.TryGetValue(o.UserId, out var name) ? name : string.Empty,
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
        }).ToList();
    }
}
