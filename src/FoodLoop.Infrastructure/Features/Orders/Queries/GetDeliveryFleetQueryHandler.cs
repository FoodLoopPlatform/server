using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Queries;

public class GetDeliveryFleetQueryHandler : IRequestHandler<GetDeliveryFleetQuery, DeliveryFleetDto>
{
    private readonly ApplicationDbContext _db;

    public GetDeliveryFleetQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<DeliveryFleetDto> Handle(GetDeliveryFleetQuery request, CancellationToken cancellationToken)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerId == request.OwnerId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OwnerId);

        var activeStatuses = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.ReadyForPickup };

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => activeStatuses.Contains(o.OrderStatus)
                     && o.Items.Any(i => i.Product != null && i.Product.OrganizationId == org.Id))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        // Load user names in one query
        var userIds = orders.Select(o => o.UserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var fleetOrders = orders.Select(o => new FleetOrderDto
        {
            OrderId = o.Id,
            CustomerName = users.TryGetValue(o.UserId, out var name) ? name : "Customer",
            OrderStatus = o.OrderStatus.ToString(),
            TotalAmount = o.TotalAmount,
            ItemCount = o.Items.Count,
            PlacedAt = o.CreatedAt,
            LastUpdatedAt = o.UpdatedAt
        }).ToList();

        return new DeliveryFleetDto
        {
            TotalActiveOrders = fleetOrders.Count,
            PendingCount = fleetOrders.Count(x => x.OrderStatus == OrderStatus.Pending.ToString()),
            PreparingCount = fleetOrders.Count(x => x.OrderStatus == OrderStatus.Preparing.ToString()),
            ReadyForPickupCount = fleetOrders.Count(x => x.OrderStatus == OrderStatus.ReadyForPickup.ToString()),
            Orders = fleetOrders
        };
    }
}
