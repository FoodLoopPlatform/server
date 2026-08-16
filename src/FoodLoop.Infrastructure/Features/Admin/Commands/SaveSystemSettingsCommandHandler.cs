using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class SaveSystemSettingsCommandHandler : IRequestHandler<SaveSystemSettingsCommand, SystemSettingsDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public SaveSystemSettingsCommandHandler(ApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<SystemSettingsDto> Handle(SaveSystemSettingsCommand request, CancellationToken cancellationToken)
    {
        // ── Validate MaxDiscountPerCyclePercent (hard ceiling: 1–15) ───────
        if (request.MaxDiscountPerCyclePercent < 1 || request.MaxDiscountPerCyclePercent > 15)
            throw new ArgumentException("MaxDiscountPerCyclePercent must be between 1 and 15.");

        // ── Validate PlatformCommissionPercent (0–100) ─────────────────────
        if (request.PlatformCommissionPercent < 0 || request.PlatformCommissionPercent > 100)
            throw new ArgumentException("PlatformCommissionPercent must be between 0 and 100.");

        // ── Validate ApiRequestRateLimitPerMinute (1–10000) ────────────────
        if (request.ApiRequestRateLimitPerMinute < 1 || request.ApiRequestRateLimitPerMinute > 10_000)
            throw new ArgumentException("ApiRequestRateLimitPerMinute must be between 1 and 10000.");

        // ── Parse enums ────────────────────────────────────────────────────
        if (!Enum.TryParse<PriceFloorPolicy>(request.DefaultPriceFloorPolicy, ignoreCase: true, out var priceFloor))
            throw new ArgumentException(
                $"Invalid DefaultPriceFloorPolicy '{request.DefaultPriceFloorPolicy}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<PriceFloorPolicy>())}.");

        if (!Enum.TryParse<AutomationMode>(request.NewBusinessDefaultAutomationMode, ignoreCase: true, out var automationMode))
            throw new ArgumentException(
                $"Invalid NewBusinessDefaultAutomationMode '{request.NewBusinessDefaultAutomationMode}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<AutomationMode>())}.");

        // ── Upsert the singleton row ───────────────────────────────────────
        var settings = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            // Defensive: should never happen because of seeded data, but handle gracefully.
            settings = new SystemSettings { Id = SystemSettings.SingletonId };
            _db.SystemSettings.Add(settings);
        }

        settings.MaxDiscountPerCyclePercent       = request.MaxDiscountPerCyclePercent;
        settings.DefaultPriceFloorPolicy          = priceFloor;
        settings.NewBusinessDefaultAutomationMode = automationMode;
        settings.AutoVerifyPartnerStores          = request.AutoVerifyPartnerStores;
        settings.BulkProductUploadEnabled         = request.BulkProductUploadEnabled;
        settings.PlatformCommissionPercent        = request.PlatformCommissionPercent;
        settings.ApiRequestRateLimitPerMinute     = request.ApiRequestRateLimitPerMinute;
        settings.UpdatedAt                        = DateTimeOffset.UtcNow;
        settings.UpdatedBy                        = request.AdminId;

        await _db.SaveChangesAsync(cancellationToken);

        // ── Audit log ─────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.AdminId,
            null,
            "SystemSettingsUpdated",
            "Platform System Settings Updated",
            $"Admin updated platform settings: MaxDiscount={request.MaxDiscountPerCyclePercent}%, " +
            $"PriceFloor={priceFloor}, AutomationMode={automationMode}, " +
            $"Commission={request.PlatformCommissionPercent}%, RateLimit={request.ApiRequestRateLimitPerMinute}req/min.",
            null,
            cancellationToken);

        return GetSystemSettingsQueryHandler.MapToDto(settings);
    }
}
