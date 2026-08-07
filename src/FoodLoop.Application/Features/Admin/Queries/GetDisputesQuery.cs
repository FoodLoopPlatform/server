using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

/// <summary>GET /admin/disputes — list product reports awaiting resolution.</summary>
public record GetDisputesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsResolved = null) : IRequest<IReadOnlyList<DisputeDto>>;
