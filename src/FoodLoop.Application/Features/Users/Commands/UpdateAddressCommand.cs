using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

/// <summary>PATCH /users/me/addresses/{id} — partially updates one of the user's addresses.</summary>
public record UpdateAddressCommand(Guid UserId, Guid AddressId, UpdateAddressRequest Request) : IRequest<AddressDto>;
