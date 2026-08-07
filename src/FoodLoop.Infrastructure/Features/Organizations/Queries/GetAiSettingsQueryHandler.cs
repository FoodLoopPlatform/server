using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Queries;
using FoodLoop.Infrastructure.Features.Organizations;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using FoodLoop.Application.Common.Interfaces;

namespace FoodLoop.Infrastructure.Features.Organizations.Queries;

public class GetAiSettingsQueryHandler : IRequestHandler<GetAiSettingsQuery, AiSettingsDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAiSettingsQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<AiSettingsDto> Handle(GetAiSettingsQuery request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);
        return new AiSettingsDto
        {
            AiAutoDiscountEnabled = org.AiAutoDiscountEnabled,
            AiAutoDiscountPercent = org.AiAutoDiscountPercent,
            AiAutoDiscountDaysBeforeExpiry = org.AiAutoDiscountDaysBeforeExpiry,
            AiAutoPricingEnabled = org.AiAutoPricingEnabled
        };
    }
}
