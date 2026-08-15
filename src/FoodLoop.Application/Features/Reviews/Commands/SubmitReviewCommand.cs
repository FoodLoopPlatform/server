using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Reviews;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Reviews.Commands;

public record SubmitReviewCommand(
    Guid UserId,
    Guid OrderId,
    int Rating,
    string? Comment
) : IRequest<Result<ReviewDto>>;
