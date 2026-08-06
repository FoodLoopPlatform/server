using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Users.Commands;

public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public DeleteAddressCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task Handle(DeleteAddressCommand command, CancellationToken cancellationToken)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(command.AddressId, cancellationToken)
            ?? throw new NotFoundException(nameof(Address), command.AddressId);

        if (address.UserId != command.UserId)
            throw new ForbiddenAccessException(_loc["CannotDeleteOtherUserAddress"]);

        _unitOfWork.Addresses.Remove(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

