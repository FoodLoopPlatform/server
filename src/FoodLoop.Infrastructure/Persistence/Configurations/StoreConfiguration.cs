using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Governorate).HasMaxLength(100);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.Neighborhood).HasMaxLength(100);
        builder.Property(s => s.Street).HasMaxLength(200);

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => s.VerificationStatus);

        builder.HasMany(s => s.ProductListings)
            .WithOne(p => p.Store)
            .HasForeignKey(p => p.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Verifications)
            .WithOne(v => v.Store)
            .HasForeignKey(v => v.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Reviews)
            .WithOne(r => r.Store)
            .HasForeignKey(r => r.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
