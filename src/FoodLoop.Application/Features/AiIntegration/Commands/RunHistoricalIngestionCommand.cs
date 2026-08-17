using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Commands;

public record RunHistoricalIngestionCommand : IRequest<Result<Unit>>;
