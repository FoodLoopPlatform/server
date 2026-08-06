using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/organizations/{id} â€” a single organization with all its documents for admin review.</summary>
public record GetOrganizationForReviewQuery(Guid OrganizationId) : IRequest<AdminOrganizationDto>;


