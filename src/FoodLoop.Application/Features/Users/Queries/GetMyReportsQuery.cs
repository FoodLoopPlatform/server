using MediatR;
using FoodLoop.Application.DTOs.Admin;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Users.Queries;

/// <summary>GET /users/me/reports — list product issue reports submitted by current user.</summary>
public record GetMyReportsQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 10,
    bool? IsResolved = null) : IRequest<IReadOnlyList<DisputeDto>>;
