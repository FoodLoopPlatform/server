using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class OrganizationVerificationConfiguration : IEntityTypeConfiguration<OrganizationVerification>
{
    public void Configure(EntityTypeBuilder<OrganizationVerification> builder)
    {
        builder.ToTable("OrganizationVerifications");

        builder.Property(v => v.VerificationType)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(v => v.DocumentUrl).HasMaxLength(500).IsRequired();
    }
}


