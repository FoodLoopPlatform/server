using System;
using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Commands;

public record RejectAiRecommendationCommand(Guid MerchantUserId, Guid Id, string? Reason) : IRequest<Result<Unit>>;
