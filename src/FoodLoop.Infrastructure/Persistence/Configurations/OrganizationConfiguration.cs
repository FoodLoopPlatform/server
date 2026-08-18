using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Governorate).HasMaxLength(100);
        builder.Property(s => s.City).HasMaxLength(100);
        builder.Property(s => s.Neighborhood).HasMaxLength(100);
        builder.Property(s => s.Street).HasMaxLength(200);
        builder.Property(s => s.AiOperatingMode)
            .HasConversion<string>()
            .HasDefaultValue(FoodLoop.Domain.Enums.AiOperatingMode.Manual)
            .IsRequired();

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => s.VerificationStatus);

        builder.HasMany(s => s.Products)
            .WithOne(p => p.Organization)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Verifications)
            .WithOne(v => v.Organization)
            .HasForeignKey(v => v.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Reviews)
            .WithOne(r => r.Organization)
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.CommissionWithdrawn)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();
    }
}

