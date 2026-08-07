using MediatR;
using FoodLoop.Application.DTOs.Organizations;
using System;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>GET /stores/me/ai-settings — read merchant AI automation preferences.</summary>
public record GetAiSettingsQuery(Guid OwnerId) : IRequest<AiSettingsDto>;
