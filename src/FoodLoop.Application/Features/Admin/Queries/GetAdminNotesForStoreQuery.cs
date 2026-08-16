using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetAdminNotesForStoreQuery(
    Guid StoreId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<IReadOnlyList<AdminNoteDto>>;
