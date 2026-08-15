using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>
/// POST /admin/system-settings — persist updated platform-wide operational configuration.
/// Only admins may call this endpoint.
/// </summary>
public record SaveSystemSettingsCommand(
    Guid AdminId,
    int MaxDiscountPerCyclePercent,
    string DefaultPriceFloorPolicy,
    string NewBusinessDefaultAutomationMode,
    bool AutoVerifyPartnerStores,
    bool BulkProductUploadEnabled,
    int PlatformCommissionPercent,
    int ApiRequestRateLimitPerMinute
) : IRequest<SystemSettingsDto>;
