using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

/// <summary>DELETE /users/me/addresses/{id} — removes one of the user's addresses.</summary>
public record DeleteAddressCommand(Guid UserId, Guid AddressId) : IRequest;
