using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Queries;
using MediatR;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Queries;

public class GetAiCycleStatusQueryHandler : IRequestHandler<GetAiCycleStatusQuery, Result<AiCyclesOverviewDto>>
{
    private readonly IAiCycleStatusTracker _tracker;

    public GetAiCycleStatusQueryHandler(IAiCycleStatusTracker tracker)
    {
        _tracker = tracker;
    }

    public Task<Result<AiCyclesOverviewDto>> Handle(GetAiCycleStatusQuery request, CancellationToken cancellationToken)
    {
        var overview = _tracker.GetAllCyclesStatus();
        return Task.FromResult(Result<AiCyclesOverviewDto>.Ok(overview));
    }
}
