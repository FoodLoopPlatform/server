using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/organizations/pending â€” all organizations waiting for admin verification.</summary>
public record GetPendingStoresQuery : IRequest<IReadOnlyList<AdminOrganizationDto>>;

