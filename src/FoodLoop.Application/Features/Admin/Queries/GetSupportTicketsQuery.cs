using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetSupportTicketsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    string? Priority = null) : IRequest<IReadOnlyList<SupportTicketDto>>;
