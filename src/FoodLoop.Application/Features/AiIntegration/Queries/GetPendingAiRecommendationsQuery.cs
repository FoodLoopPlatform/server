using System;
using System.Collections.Generic;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Queries;

public record GetPendingAiRecommendationsQuery(Guid MerchantUserId) : IRequest<Result<IReadOnlyList<AiPricingRecommendationDto>>>;
