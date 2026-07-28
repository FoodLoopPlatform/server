using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/users/{id}/activity-log — recent events for a user (admin view).</summary>
public record GetUserActivityLogQuery(Guid UserId) : IRequest<IReadOnlyList<ActivityLogEntryDto>>;
