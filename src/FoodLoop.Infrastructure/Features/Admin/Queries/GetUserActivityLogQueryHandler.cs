using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetUserActivityLogQueryHandler
    : IRequestHandler<GetUserActivityLogQuery, IReadOnlyList<ActivityLogEntryDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public GetUserActivityLogQueryHandler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<ActivityLogEntryDto>> Handle(
        GetUserActivityLogQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        return await _db.AuditLogs
            .Where(l => l.UserId == request.UserId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .Select(l => new ActivityLogEntryDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = user.FullName,
                ActorType = "User",
                OrganizationId = l.OrganizationId,
                EventType = l.EventType,
                Title = l.Title,
                Description = l.Description,
                Severity = "Low",
                IpAddress = l.IpAddress,
                OccurredAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}

