using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class AiRiskAssessmentConfiguration : IEntityTypeConfiguration<AiRiskAssessment>
{
    public void Configure(EntityTypeBuilder<AiRiskAssessment> builder)
    {
        builder.ToTable("AiRiskAssessments");

        builder.Property(a => a.RiskLevel)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Route)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.Reason)
            .IsRequired();

        builder.Property(a => a.Confidence)
            .IsRequired();

        builder.Property(a => a.IsPricingStaged)
            .IsRequired();

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.SnapshotOriginalPrice)
            .HasPrecision(18, 2);

        builder.Property(a => a.SnapshotQuantityAvailable);

        builder.Property(a => a.SnapshotProductStatus)
            .HasConversion<string>();

        builder.HasOne(a => a.Product)
            .WithMany()
            .HasForeignKey(a => a.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ProductId);
    }
}
