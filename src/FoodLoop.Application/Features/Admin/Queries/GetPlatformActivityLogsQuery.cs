using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/activity-logs — global platform-wide activity and audit log feed.</summary>
public record GetPlatformActivityLogsQuery(
    string? SearchTerm = null,
    string? EventType = null,
    Guid? UserId = null,
    Guid? OrganizationId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<IReadOnlyList<ActivityLogEntryDto>>;
