using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.OriginalPrice).HasPrecision(10, 2);
        builder.Property(p => p.DiscountedPrice).HasPrecision(10, 2);
        builder.Property(p => p.ModerationNote).HasMaxLength(1000);

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasIndex(p => p.OrganizationId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.ExpirationDate);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.DiscountedPrice);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product!)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.AIRecognitionResult)
            .WithOne(r => r.Product!)
            .HasForeignKey<AIRecognitionResult>(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

