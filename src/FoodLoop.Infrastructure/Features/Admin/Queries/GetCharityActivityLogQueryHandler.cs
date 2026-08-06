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

        var entries = new List<ActivityLogEntryDto>();

        // 1. Account created
        if (owner != null)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "AccountCreated",
                Title = "Charity Account Created",
                Description = $"New charity account registered with email {owner.Email} for charity association '{organization.Name}'.",
                OccurredAt = owner.CreatedAt,
            });
        }

        // 2. Charity Profile & Location updates
        if (organization.UpdatedAt.HasValue && organization.UpdatedAt.Value != organization.CreatedAt)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "CharityProfileUpdated",
                Title = "Charity Profile Updated",
                Description = $"Updated charity settings or location coordinates for '{organization.Name}'.",
                OccurredAt = organization.UpdatedAt.Value,
            });
        }

        // 3. Document Uploads & Reviews
        var verifications = await _db.OrganizationVerifications
            .Where(v => v.OrganizationId == organization.Id)
            .OrderByDescending(v => v.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var v in verifications)
        {
            entries.Add(new ActivityLogEntryDto
            {
                EventType = "DocumentUploaded",
                Title = "Document Uploaded",
                Description = $"Uploaded {v.VerificationType} document.",
                OccurredAt = v.CreatedAt,
            });

            if (v.ReviewedAt.HasValue)
            {
                entries.Add(new ActivityLogEntryDto
                {
                    EventType = "DocumentVerified",
                    Title = "Document Reviewed",
                    Description = $"{v.VerificationType} was marked {v.Status} by admin.",
                    OccurredAt = v.ReviewedAt.Value,
                });
            }
        }

        // 4. Support tickets opened by owner
        if (owner != null)
        {
            var tickets = await _db.SupportTickets
                .Where(t => t.UserId == owner.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            foreach (var t in tickets)
            {
                entries.Add(new ActivityLogEntryDto
                {
                    EventType = "SupportTicket",
                    Title = "Support Ticket Opened",
                    Description = $"Ticket: {t.Category} â€” {t.Status}.",
                    OccurredAt = t.CreatedAt,
                });
            }
        }

        return entries
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .ToList();
    }
}



