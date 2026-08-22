using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.AiIntegration.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.AiIntegration.Queries;

public class GetStoreAiScheduleQueryHandler : IRequestHandler<GetStoreAiScheduleQuery, Result<StoreAiScheduleDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAiCycleStatusTracker _tracker;

    public GetStoreAiScheduleQueryHandler(IApplicationDbContext dbContext, IAiCycleStatusTracker tracker)
    {
        _dbContext = dbContext;
        _tracker = tracker;
    }

    public async Task<Result<StoreAiScheduleDto>> Handle(GetStoreAiScheduleQuery request, CancellationToken cancellationToken)
    {
        var store = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.OwnerId == request.MerchantUserId && !o.IsDeleted, cancellationToken);

        var pricingCycle = _tracker.GetCycleStatus("PricingBatch");
        var monitoringCycle = _tracker.GetCycleStatus("MonitoringScanner");

        var automationMode = store?.AiOperatingMode.ToString() ?? "Assisted";

        var schedule = new StoreAiScheduleDto(
            pricingCycle.NextRunExpectedAt,
            monitoringCycle.NextRunExpectedAt,
            pricingCycle.IntervalMinutes,
            pricingCycle.IsRunning,
            automationMode
        );

        return Result<StoreAiScheduleDto>.Ok(schedule);
    }
}
