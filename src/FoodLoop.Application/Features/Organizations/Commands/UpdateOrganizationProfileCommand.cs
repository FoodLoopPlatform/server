using FoodLoop.Application.DTOs.Organizations;
using MediatR;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>PATCH /organizations/me â€” updates the organization's name, description, category, and logo.</summary>
public record UpdateOrganizationProfileCommand(Guid OwnerId, UpdateStoreProfileRequest Request) : IRequest<OrganizationDto>;

