using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetAdminReviewsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    int? Rating = null,
    Guid? StoreId = null) : IRequest<IReadOnlyList<AdminReviewDto>>;
