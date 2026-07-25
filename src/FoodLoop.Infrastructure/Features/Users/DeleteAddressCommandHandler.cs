using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Users;

public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAddressCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteAddressCommand command, CancellationToken cancellationToken)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(command.AddressId, cancellationToken)
            ?? throw new NotFoundException(nameof(Address), command.AddressId);

        if (address.UserId != command.UserId)
            throw new ForbiddenAccessException("You cannot delete another user's address.");

        _unitOfWork.Addresses.Remove(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
