using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetAdminNotesForCharityQueryHandler
    : IRequestHandler<GetAdminNotesForCharityQuery, IReadOnlyList<AdminNoteDto>>
{
    private readonly ApplicationDbContext _db;

    public GetAdminNotesForCharityQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminNoteDto>> Handle(
        GetAdminNotesForCharityQuery request, CancellationToken cancellationToken)
    {
        // 1. Find the charity organization and verify its owner
        var charity = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.CharityId && !o.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Charity", request.CharityId);

        var recipient = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == charity.OwnerId, cancellationToken)
            ?? throw new NotFoundException("Charity Owner User", charity.OwnerId);

        // 2. Fetch notes for the charity owner
        var notes = await _db.AdminNotes
            .AsNoTracking()
            .Where(n => n.RecipientUserId == charity.OwnerId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 3. Load admin sender names
        var adminIds = notes.Select(n => n.SentByAdminId).Distinct().ToList();
        var adminNames = await _db.Users
            .Where(u => adminIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return notes.Select(n => new AdminNoteDto
        {
            Id              = n.Id,
            SentByAdminId   = n.SentByAdminId,
            SentByAdminName = adminNames.TryGetValue(n.SentByAdminId, out var name) ? name : "Admin",
            RecipientUserId = n.RecipientUserId,
            RecipientName   = recipient.FullName,
            Category        = n.Category,
            Template        = n.Template,
            Title           = n.Title,
            Body            = n.Body,
            IsInternal      = n.IsInternal,
            SentAt          = n.CreatedAt
        }).ToList();
    }
}
