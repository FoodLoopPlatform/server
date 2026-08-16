using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Queries;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin.Queries;

public class GetAdminNotesForUserQueryHandler
    : IRequestHandler<GetAdminNotesForUserQuery, IReadOnlyList<AdminNoteDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GetAdminNotesForUserQueryHandler(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IReadOnlyList<AdminNoteDto>> Handle(
        GetAdminNotesForUserQuery request, CancellationToken cancellationToken)
    {
        // Verify the recipient user exists
        var recipient = await _userManager.FindByIdAsync(request.RecipientUserId.ToString())
            ?? throw new NotFoundException("User", request.RecipientUserId);

        // Fetch notes for this recipient
        var notes = await _db.AdminNotes
            .AsNoTracking()
            .Where(n => n.RecipientUserId == request.RecipientUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Batch-load admin sender names to avoid N+1
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
