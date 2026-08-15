using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Organizations.Queries;

/// <summary>GET /stores/me/disputes — list reports and disputes filed on current merchant store's products.</summary>
public record GetStoreDisputesQuery(
    Guid OwnerId,
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsResolved = null) : IRequest<IReadOnlyList<DisputeDto>>;
