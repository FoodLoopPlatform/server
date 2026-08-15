using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("SystemSettings");

        // Hard ceiling: MaxDiscountPerCyclePercent must be 1–15
        builder.Property(s => s.MaxDiscountPerCyclePercent).IsRequired();

        // Seed the singleton row so the table always has exactly one record
        builder.HasData(new SystemSettings
        {
            Id = SystemSettings.SingletonId,
            MaxDiscountPerCyclePercent = 10,
            DefaultPriceFloorPolicy = PriceFloorPolicy.DynamicAi,
            NewBusinessDefaultAutomationMode = AutomationMode.Assisted,
            AutoVerifyPartnerStores = false,
            BulkProductUploadEnabled = true,
            PlatformCommissionPercent = 10,
            ApiRequestRateLimitPerMinute = 120,
            CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
