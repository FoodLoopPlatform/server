using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin;

public class GetAnalyticsSummaryQueryHandler : IRequestHandler<GetAnalyticsSummaryQuery, AnalyticsSummaryDto>
{
    private readonly ApplicationDbContext _context;

    public GetAnalyticsSummaryQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AnalyticsSummaryDto> Handle(GetAnalyticsSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. User metrics
        var roleCounts = await (from ur in _context.UserRoles
                                join r in _context.Roles on ur.RoleId equals r.Id
                                group ur by r.Name into g
                                select new { RoleName = g.Key, Count = g.Count() })
                               .ToListAsync(cancellationToken);

        var customerCount = roleCounts.FirstOrDefault(r => r.RoleName == AppRole.Customer)?.Count ?? 0;
        var merchantCount = roleCounts.FirstOrDefault(r => r.RoleName == AppRole.Merchant)?.Count ?? 0;
        var charityCount = roleCounts.FirstOrDefault(r => r.RoleName == AppRole.Charity)?.Count ?? 0;
        var adminCount = roleCounts.FirstOrDefault(r => r.RoleName == AppRole.Admin)?.Count ?? 0;
        var totalUsers = await _context.Users.CountAsync(cancellationToken);

        // 2. Store metrics
        var storeCounts = await _context.Stores
            .GroupBy(s => s.VerificationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var unverifiedStores = storeCounts.FirstOrDefault(s => s.Status == VerificationStatus.Unverified)?.Count ?? 0;
        var pendingStores = storeCounts.FirstOrDefault(s => s.Status == VerificationStatus.Pending)?.Count ?? 0;
        var verifiedStores = storeCounts.FirstOrDefault(s => s.Status == VerificationStatus.Verified)?.Count ?? 0;
        var rejectedStores = storeCounts.FirstOrDefault(s => s.Status == VerificationStatus.Rejected)?.Count ?? 0;
        var totalStores = await _context.Stores.CountAsync(cancellationToken);

        // 3. Product metrics
        var listingCounts = await _context.Products
            .Where(l => !l.IsDeleted)
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeListings = listingCounts.FirstOrDefault(l => l.Status == ListingStatus.Active)?.Count ?? 0;
        var soldOutListings = listingCounts.FirstOrDefault(l => l.Status == ListingStatus.SoldOut)?.Count ?? 0;
        var expiredListings = listingCounts.FirstOrDefault(l => l.Status == ListingStatus.Expired)?.Count ?? 0;
        var totalListings = await _context.Products.CountAsync(l => !l.IsDeleted, cancellationToken);

        // 4. Order metrics
        var orderCounts = await _context.Orders
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pendingOrders = orderCounts.FirstOrDefault(o => o.Status == OrderStatus.Pending)?.Count ?? 0;
        var completedOrders = orderCounts.FirstOrDefault(o => o.Status == OrderStatus.Completed)?.Count ?? 0;
        var cancelledOrders = orderCounts.FirstOrDefault(o => o.Status == OrderStatus.Cancelled)?.Count ?? 0;
        var totalOrders = await _context.Orders.CountAsync(cancellationToken);

        // 5. Total Revenue & Food Savings
        var totalRevenue = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatus.Completed || o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        var totalFoodSavings = await _context.OrderItems
            .Where(oi => oi.Order!.OrderStatus == OrderStatus.Completed)
            .SumAsync(oi => oi.Quantity * (oi.Product!.OriginalPrice - oi.Product.DiscountedPrice), cancellationToken);

        return new AnalyticsSummaryDto
        {
            Users = new UserMetricsDto
            {
                Total = totalUsers,
                Customers = customerCount,
                Merchants = merchantCount,
                Charities = charityCount,
                Admins = adminCount
            },
            Stores = new StoreMetricsDto
            {
                Total = totalStores,
                Unverified = unverifiedStores,
                Pending = pendingStores,
                Verified = verifiedStores,
                Rejected = rejectedStores
            },
            Listings = new ListingMetricsDto
            {
                Total = totalListings,
                Active = activeListings,
                SoldOut = soldOutListings,
                Expired = expiredListings
            },
            Orders = new OrderMetricsDto
            {
                Total = totalOrders,
                Pending = pendingOrders,
                Completed = completedOrders,
                Cancelled = cancelledOrders
            },
            TotalRevenue = totalRevenue,
            TotalFoodSavings = totalFoodSavings
        };
    }
}
