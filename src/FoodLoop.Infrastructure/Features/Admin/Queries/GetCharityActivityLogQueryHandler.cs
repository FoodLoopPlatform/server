using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Domain.Entities;
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

public class GetCharityActivityLogQueryHandler
    : IRequestHandler<GetCharityActivityLogQuery, IReadOnlyList<ActivityLogEntryDto>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public GetCharityActivityLogQueryHandler(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IReadOnlyList<ActivityLogEntryDto>> Handle(
        GetCharityActivityLogQuery request, CancellationToken cancellationToken)
    {
        var organization = await _db.Organizations.FirstOrDefaultAsync(
            s => s.Id == request.OrganizationId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.OrganizationId);

        var owner = await _userManager.FindByIdAsync(organization.OwnerId.ToString());
        var ownerId = owner?.Id;

        return await _db.AuditLogs
            .Where(l => l.OrganizationId == request.OrganizationId || (ownerId != null && l.UserId == ownerId))
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .Select(l => new ActivityLogEntryDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = owner != null ? owner.FullName : "Charity",
                ActorType = "Charity",
                OrganizationId = l.OrganizationId,
                OrganizationName = organization.Name,
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
