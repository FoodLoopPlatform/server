using FoodLoop.Application.DTOs.Organizations;
using MediatR;

namespace FoodLoop.Application.Features.Organizations.Commands;

/// <summary>PATCH /organizations/me/location â€” step 2's location fields (business_verification_location).</summary>
public record UpdateOrganizationLocationCommand(Guid OwnerId, UpdateStoreLocationRequest Request) : IRequest<OrganizationDto>;

