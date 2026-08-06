using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Users.Commands;

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, AddressDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _loc;

    public UpdateAddressCommandHandler(IUnitOfWork unitOfWork, ILocalizationService loc)
    {
        _unitOfWork = unitOfWork;
        _loc = loc;
    }

    public async Task<AddressDto> Handle(UpdateAddressCommand command, CancellationToken cancellationToken)
    {
        var address = await _unitOfWork.Addresses.GetByIdAsync(command.AddressId, cancellationToken)
            ?? throw new NotFoundException(nameof(Address), command.AddressId);

        if (address.UserId != command.UserId)
            throw new ForbiddenAccessException(_loc["CannotModifyOtherUserAddress"]);

        var request = command.Request;

        if (request.AddressType.HasValue) address.AddressType = request.AddressType.Value;
        if (request.City != null) address.City = request.City;
        if (request.District != null) address.District = request.District;
        if (request.Street != null) address.Street = request.Street;
        if (request.BuildingNo != null) address.BuildingNo = request.BuildingNo;
        if (request.Floor != null) address.Floor = request.Floor;
        if (request.ApartmentNo != null) address.ApartmentNo = request.ApartmentNo;
        if (request.Notes != null) address.Notes = request.Notes;
        if (request.Latitude.HasValue) address.Latitude = request.Latitude.Value;
        if (request.Longitude.HasValue) address.Longitude = request.Longitude.Value;

        if (request.IsDefault.HasValue)
        {
            if (request.IsDefault.Value)
                await _unitOfWork.Addresses.ClearDefaultAsync(command.UserId, exceptAddressId: address.Id, cancellationToken: cancellationToken);

            address.IsDefault = request.IsDefault.Value;
        }

        address.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return address.ToDto();
    }
}

