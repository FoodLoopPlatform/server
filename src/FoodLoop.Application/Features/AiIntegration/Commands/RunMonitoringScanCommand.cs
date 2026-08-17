using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.AiIntegration.Commands;

/// <summary>
/// Command to run the background product monitoring scan.
/// </summary>
public record RunMonitoringScanCommand : IRequest<Result<Unit>>;
