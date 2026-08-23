using System;
using FoodLoop.Application.Common.Models;
using MediatR;

namespace FoodLoop.Application.Features.Admin.Commands;

public record ExtendProductExpirationResultDto(
    int TotalProductsUpdated,
    int ReactivatedCount,
    DateOnly NewEarliestExpiration,
    DateOnly NewLatestExpiration,
    string Message
);

public record ExtendProductExpirationCommand(
    int Days = 7,
    bool ReactivateExpiredProducts = true,
    Guid? StoreId = null
) : IRequest<Result<ExtendProductExpirationResultDto>>;
