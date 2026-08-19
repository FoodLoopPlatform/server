using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Admin;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class SendAdminNoteCommandHandler : IRequestHandler<SendAdminNoteCommand, AdminNoteDto>
{
    private static readonly HashSet<string> ValidCategories =
        new(StringComparer.OrdinalIgnoreCase) { "Notice", "Warning", "Urgent", "Internal" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRealTimeNotificationService _notification;
    private readonly IAuditLogService _audit;

    public SendAdminNoteCommandHandler(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IRealTimeNotificationService notification,
        IAuditLogService audit)
    {
        _db = db;
        _userManager = userManager;
        _notification = notification;
        _audit = audit;
    }

    public async Task<AdminNoteDto> Handle(SendAdminNoteCommand request, CancellationToken cancellationToken)
    {
        // ── Validate category ─────────────────────────────────────────────
        if (!ValidCategories.Contains(request.Category))
            throw new ArgumentException(
                $"Invalid category '{request.Category}'. " +
                $"Valid values: {string.Join(", ", ValidCategories)}.");

        // ── Validate recipient exists ─────────────────────────────────────
        var recipient = await _userManager.FindByIdAsync(request.RecipientUserId.ToString())
            ?? throw new NotFoundException("User", request.RecipientUserId);

        // ── Validate admin exists ─────────────────────────────────────────
        var admin = await _userManager.FindByIdAsync(request.AdminId.ToString())
            ?? throw new NotFoundException("User", request.AdminId);

        // ── Persist the note ──────────────────────────────────────────────
        var note = new AdminNote
        {
            SentByAdminId    = request.AdminId,
            RecipientUserId  = request.RecipientUserId,
            Category         = request.Category,
            Template         = request.Template,
            Title            = request.Title,
            Body             = request.Body,
            IsInternal       = request.IsInternal
        };

        _db.AdminNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);

        // ── Push notification to user (skip if internal) ──────────────────
        if (!request.IsInternal)
        {
            // Map category to a notification type string the mobile app can handle
            var notificationType = request.Category switch
            {
                "Warning"  => "AdminWarning",
                "Urgent"   => "AdminUrgent",
                _          => "AdminNotice"
            };

            await _notification.SendNotificationToUserAsync(
                request.RecipientUserId,
                request.Title,
                request.Body,
                notificationType,
                Array.Empty<object>(),
                cancellationToken);
        }

        // ── Audit log ─────────────────────────────────────────────────────
        await _audit.LogAsync(
            request.AdminId,
            null,
            "AdminNoteSent",
            "Admin Note Sent to User",
            $"Admin sent a '{request.Category}' note to user '{recipient.Email}'. " +
            $"Title: '{request.Title}'. Internal: {request.IsInternal}.",
            null,
            cancellationToken);

        return new AdminNoteDto
        {
            Id               = note.Id,
            SentByAdminId    = note.SentByAdminId,
            SentByAdminName  = admin.FullName,
            RecipientUserId  = note.RecipientUserId,
            RecipientName    = recipient.FullName,
            Category         = note.Category,
            Template         = note.Template,
            Title            = note.Title,
            Body             = note.Body,
            IsInternal       = note.IsInternal,
            SentAt           = note.CreatedAt
        };
    }
}
