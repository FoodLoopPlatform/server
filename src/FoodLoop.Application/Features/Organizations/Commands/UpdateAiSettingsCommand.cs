using MediatR;
using FoodLoop.Application.DTOs.Organizations;
using System;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>PATCH /stores/me/ai-settings — update merchant AI automation preferences.</summary>
public record UpdateAiSettingsCommand(
    Guid OwnerId,
    bool AiAutoDiscountEnabled,
    int AiAutoDiscountPercent,
    int AiAutoDiscountDaysBeforeExpiry,
    bool AiAutoPricingEnabled) : IRequest<AiSettingsDto>;
