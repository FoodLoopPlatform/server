using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class ProductPricingEpisodeConfiguration : IEntityTypeConfiguration<ProductPricingEpisode>
{
    public void Configure(EntityTypeBuilder<ProductPricingEpisode> builder)
    {
        builder.ToTable("ProductPricingEpisodes");

        builder.HasKey(pe => pe.Id);

        builder.Property(pe => pe.EventId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pe => pe.IngestionCorrelationId)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(pe => pe.IngestedAt)
            .IsRequired(false);

        builder.Property(pe => pe.Outcome)
            .HasMaxLength(50)
            .IsRequired();

        // Foreign Key to Product with cascade delete
        builder.HasOne(pe => pe.Product)
            .WithMany()
            .HasForeignKey(pe => pe.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on (ProductId, RecordedAt) for lookups
        builder.HasIndex(pe => new { pe.ProductId, pe.RecordedAt });

        // UNIQUE index on (ProductId, EventId)
        builder.HasIndex(pe => new { pe.ProductId, pe.EventId })
            .IsUnique();
    }
}
