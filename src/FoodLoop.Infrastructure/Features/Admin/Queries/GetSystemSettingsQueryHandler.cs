using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetSystemSettingsQueryHandler : IRequestHandler<GetSystemSettingsQuery, SystemSettingsDto>
{
    private readonly ApplicationDbContext _db;

    public GetSystemSettingsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<SystemSettingsDto> Handle(GetSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        // The singleton row is guaranteed by the seeded data in SystemSettingsConfiguration.
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .FirstAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);

        return MapToDto(settings);
    }

    internal static SystemSettingsDto MapToDto(SystemSettings s) => new()
    {
        MaxDiscountPerCyclePercent        = s.MaxDiscountPerCyclePercent,
        DefaultPriceFloorPolicy           = s.DefaultPriceFloorPolicy.ToString(),
        NewBusinessDefaultAutomationMode  = s.NewBusinessDefaultAutomationMode.ToString(),
        AutoVerifyPartnerStores           = s.AutoVerifyPartnerStores,
        BulkProductUploadEnabled          = s.BulkProductUploadEnabled,
        PlatformCommissionPercent         = s.PlatformCommissionPercent,
        ApiRequestRateLimitPerMinute      = s.ApiRequestRateLimitPerMinute,
        LastUpdatedAt                     = s.UpdatedAt ?? s.CreatedAt
    };
}
