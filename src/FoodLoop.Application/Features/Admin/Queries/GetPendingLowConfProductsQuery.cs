using FoodLoop.Application.DTOs.Admin;
using MediatR;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Admin.Queries;

public record GetPendingLowConfProductsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    double ConfidenceThreshold = 0.8
) : IRequest<IReadOnlyList<AdminProductDto>>;
