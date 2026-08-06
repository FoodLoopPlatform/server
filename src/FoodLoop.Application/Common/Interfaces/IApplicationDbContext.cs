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
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationVerification> OrganizationVerifications { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
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
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


