using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class StoreVerificationConfiguration : IEntityTypeConfiguration<StoreVerification>
{
    public void Configure(EntityTypeBuilder<StoreVerification> builder)
    {
        builder.Property(v => v.VerificationType).HasMaxLength(100).IsRequired();
        builder.Property(v => v.DocumentUrl).HasMaxLength(500).IsRequired();
    }
}
