using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Method).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.TransactionReference).HasMaxLength(200);
    }
}
