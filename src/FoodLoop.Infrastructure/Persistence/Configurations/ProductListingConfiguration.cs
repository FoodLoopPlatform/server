using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class ProductListingConfiguration : IEntityTypeConfiguration<ProductListing>
{
    public void Configure(EntityTypeBuilder<ProductListing> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.TitleAr).HasMaxLength(200);
        builder.Property(p => p.OriginalPrice).HasPrecision(10, 2);
        builder.Property(p => p.DiscountedPrice).HasPrecision(10, 2);

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasIndex(p => p.StoreId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.ExpirationDate);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.DiscountedPrice);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Listing)
            .HasForeignKey(i => i.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.AIRecognitionResult)
            .WithOne(r => r.Listing)
            .HasForeignKey<AIRecognitionResult>(r => r.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
