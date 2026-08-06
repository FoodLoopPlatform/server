using FoodLoop.Application.DTOs.Reviews;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Reviews.Queries;

public record GetOrganizationReviewsQuery(
    Guid OrganizationId,
    int PageNumber,
    int PageSize
) : IRequest<IReadOnlyList<ReviewDto>>;
