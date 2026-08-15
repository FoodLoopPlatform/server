using FoodLoop.Domain.Common;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using FoodLoop.Application.Common.Interfaces;

namespace FoodLoop.Infrastructure.Persistence;

/// <summary>
/// EF Core context. Inherits IdentityDbContext to get Users/Roles/Claims/Logins/Tokens
/// tables for free, and adds every other domain table from the Database Design doc.
/// Data access from services goes through IUnitOfWork/IRepository (see
/// Persistence/UnitOfWork.cs and Persistence/Repositories/) rather than this type directly.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationVerification> OrganizationVerifications => Set<OrganizationVerification>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<AIRecognitionResult> AIRecognitionResults => Set<AIRecognitionResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<ProductReport> ProductReports => Set<ProductReport>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrganizationId);
        });

        // Donation has two FKs to Organization; specify explicitly to avoid cascade conflicts.
        builder.Entity<Donation>(entity =>
        {
            entity.HasOne(d => d.DonorOrganization)
                  .WithMany(o => o.DonationsGiven)
                  .HasForeignKey(d => d.DonorOrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.RecipientOrganization)
                  .WithMany(o => o.DonationsReceived)
                  .HasForeignKey(d => d.RecipientOrganizationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PriceHistory>(entity =>
        {
            entity.Property(p => p.OldOriginalPrice).HasPrecision(18, 2);
            entity.Property(p => p.OldDiscountedPrice).HasPrecision(18, 2);
            entity.Property(p => p.NewOriginalPrice).HasPrecision(18, 2);
            entity.Property(p => p.NewDiscountedPrice).HasPrecision(18, 2);
        });

        // Rename default Identity tables to a cleaner schema (optional but tidy).
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteAndAuditConventions();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplySoftDeleteAndAuditConventions()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case Microsoft.EntityFrameworkCore.EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case Microsoft.EntityFrameworkCore.EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        // Soft-delete: converts a Deleted state into an Update that stamps DeletedAt/IsDeleted.
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Deleted)
            {
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
            }
        }
    }
}

