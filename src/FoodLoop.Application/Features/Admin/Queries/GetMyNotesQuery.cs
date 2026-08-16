using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetMyNotesQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<IReadOnlyList<AdminNoteDto>>;
