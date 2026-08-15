using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Domain.Enums;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>PATCH /stores/me/ai-settings — update merchant AI automation preferences.</summary>
public record UpdateAiSettingsCommand(
    Guid OwnerId,
    bool? AiAutoDiscountEnabled,
    int AiAutoDiscountPercent,
    int AiAutoDiscountDaysBeforeExpiry,
    bool? AiAutoPricingEnabled,
    AutomationMode? AutomationMode = null) : IRequest<AiSettingsDto>;
