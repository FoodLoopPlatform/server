using FoodLoop.Application.DTOs.Admin;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>
/// GET /admin/users/{userId}/notes
/// Returns all notes sent by any admin to a specific user,
/// ordered newest-first with pagination.
/// </summary>
public record GetAdminNotesForUserQuery(
    Guid RecipientUserId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<IReadOnlyList<AdminNoteDto>>;
