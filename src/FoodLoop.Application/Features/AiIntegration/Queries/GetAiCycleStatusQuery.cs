using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Queries;

public record GetAiCycleStatusQuery : IRequest<Result<AiCyclesOverviewDto>>;
