using FoodLoop.Application.Common.Models;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Admin.Commands;

/// <summary>
/// Command to correct historical pricing episodes by resetting IngestedAt and updating snapshot metrics.
/// </summary>
public record RequestHistoricalEpisodeCorrectionCommand(
    Guid? RowId,
    string? EventId,
    string Reason,
    double? CorrectedDiscountPercentage = null,
    double? CorrectedSellThroughRate = null,
    string? CorrectedOutcome = null
) : IRequest<Result>;
