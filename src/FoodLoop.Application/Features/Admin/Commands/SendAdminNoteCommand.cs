using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>
/// POST /admin/users/{userId}/notes
/// Sends an official note from an admin to a user.
/// Non-internal notes are also delivered as a push notification.
/// </summary>
public record SendAdminNoteCommand(
    Guid AdminId,
    Guid RecipientUserId,
    string Category,
    string? Template,
    string Title,
    string Body,
    bool IsInternal
) : IRequest<AdminNoteDto>;
