using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class AiPricingRecommendationConfiguration : IEntityTypeConfiguration<AiPricingRecommendation>
{
    public void Configure(EntityTypeBuilder<AiPricingRecommendation> builder)
    {
        builder.ToTable("AiPricingRecommendations");

        builder.Property(p => p.DiscountPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.Reason)
            .IsRequired();

        builder.Property(p => p.Confidence)
            .IsRequired();

        builder.Property(p => p.ActionRequirement)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.ActionReason)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.CorrelationId)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.RiskAssessment)
            .WithMany()
            .HasForeignKey(p => p.RiskAssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.RiskAssessmentId)
            .IsUnique()
            .HasFilter("[RiskAssessmentId] IS NOT NULL");

        builder.HasIndex(p => new { p.OrganizationId, p.Status });
    }
}
