using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>PATCH /admin/organizations/{id}/verify â€” approve or reject a organization's verification.</summary>
public record VerifyOrganizationCommand(Guid OrganizationId, Guid AdminId, VerifyOrganizationRequest Request) : IRequest<AdminOrganizationDto>;

