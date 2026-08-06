using FoodLoop.Application.DTOs.Organizations;
using MediatR;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>GET /organizations/me â€” the calling merchant's own organization, including uploaded documents.</summary>
public record GetMyOrganizationQuery(Guid OwnerId) : IRequest<OrganizationDto>;

