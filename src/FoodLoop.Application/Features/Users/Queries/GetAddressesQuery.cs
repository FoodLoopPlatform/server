using FoodLoop.Application.DTOs.Users;
using MediatR;

namespace FoodLoop.Application.Features.Users.Queries;

/// <summary>GET /users/me/addresses — every saved address for the authenticated user.</summary>
public record GetAddressesQuery(Guid UserId) : IRequest<IReadOnlyList<AddressDto>>;
