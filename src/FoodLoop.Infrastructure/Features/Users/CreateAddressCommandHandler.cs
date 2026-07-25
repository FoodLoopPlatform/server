using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Mappings;
using MediatR;

namespace FoodLoop.Infrastructure.Features.Users;

public class CreateAddressCommandHandler : IRequestHandler<CreateAddressCommand, AddressDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateAddressCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AddressDto> Handle(CreateAddressCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (request.IsDefault)
        {
            await _unitOfWork.Addresses.ClearDefaultAsync(command.UserId, cancellationToken: cancellationToken);
        }

        var address = new Address
        {
            UserId = command.UserId,
            AddressType = request.AddressType,
            City = request.City,
            District = request.District,
            Street = request.Street,
            BuildingNo = request.BuildingNo,
            Floor = request.Floor,
            ApartmentNo = request.ApartmentNo,
            Notes = request.Notes,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsDefault = request.IsDefault,
        };

        _unitOfWork.Addresses.Add(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return address.ToDto();
    }
}
