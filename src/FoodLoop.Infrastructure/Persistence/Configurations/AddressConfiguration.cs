using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.District).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Street).HasMaxLength(200).IsRequired();
        builder.Property(a => a.BuildingNo).HasMaxLength(20);
        builder.Property(a => a.Floor).HasMaxLength(20);
        builder.Property(a => a.ApartmentNo).HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(300);

        builder.HasIndex(a => a.UserId);
    }
}
