using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Commands;

/// <summary>POST /users/me/addresses — adds a new saved address for the authenticated user.</summary>
public record CreateAddressCommand(Guid UserId, CreateAddressRequest Request) : IRequest<AddressDto>;
