using System;
using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Commands;

public record ApproveAiRecommendationCommand(Guid MerchantUserId, Guid Id) : IRequest<Result<Unit>>;
