using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Organizations;
using FoodLoop.Application.Features.Organizations.Commands;
using FoodLoop.Infrastructure.Features.Organizations;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Organizations.Commands;

public class UpdateAiSettingsCommandHandler : IRequestHandler<UpdateAiSettingsCommand, AiSettingsDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAiSettingsCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<AiSettingsDto> Handle(UpdateAiSettingsCommand request, CancellationToken cancellationToken)
    {
        var org = await _unitOfWork.FindByOwnerOrThrowAsync(request.OwnerId, "Organization not found.", cancellationToken);

        if (request.AiAutoDiscountPercent < 0 || request.AiAutoDiscountPercent > 100)
            throw new System.ArgumentException("AiAutoDiscountPercent must be between 0 and 100.");
        if (request.AiAutoDiscountDaysBeforeExpiry < 1)
            throw new System.ArgumentException("AiAutoDiscountDaysBeforeExpiry must be at least 1.");

        org.AiAutoDiscountEnabled = request.AiAutoDiscountEnabled;
        org.AiAutoDiscountPercent = request.AiAutoDiscountPercent;
        org.AiAutoDiscountDaysBeforeExpiry = request.AiAutoDiscountDaysBeforeExpiry;
        org.AiAutoPricingEnabled = request.AiAutoPricingEnabled;

        _unitOfWork.Organizations.Update(org);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AiSettingsDto
        {
            AiAutoDiscountEnabled = org.AiAutoDiscountEnabled,
            AiAutoDiscountPercent = org.AiAutoDiscountPercent,
            AiAutoDiscountDaysBeforeExpiry = org.AiAutoDiscountDaysBeforeExpiry,
            AiAutoPricingEnabled = org.AiAutoPricingEnabled
        };
    }
}
