using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class ProductReportConfiguration : IEntityTypeConfiguration<ProductReport>
{
    public void Configure(EntityTypeBuilder<ProductReport> builder)
    {
        builder.Property(r => r.ImageUrl)
            .HasMaxLength(500)
            .IsRequired(false);
    }
}
