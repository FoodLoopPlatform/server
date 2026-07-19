using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Application-layer abstraction over the EF Core DbContext so services depend on
/// this interface rather than the concrete Infrastructure type (Dependency Inversion).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Address> Addresses { get; }
    DbSet<Store> Stores { get; }
    DbSet<StoreVerification> StoreVerifications { get; }
    DbSet<Category> Categories { get; }
    DbSet<ProductListing> ProductListings { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<AIRecognitionResult> AIRecognitionResults { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
