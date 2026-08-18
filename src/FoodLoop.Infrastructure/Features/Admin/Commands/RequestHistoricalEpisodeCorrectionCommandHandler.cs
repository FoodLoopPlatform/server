using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Admin.Commands;

public class RequestHistoricalEpisodeCorrectionCommandHandler
    : IRequestHandler<RequestHistoricalEpisodeCorrectionCommand, Result>
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RequestHistoricalEpisodeCorrectionCommandHandler> _logger;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public RequestHistoricalEpisodeCorrectionCommandHandler(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<RequestHistoricalEpisodeCorrectionCommandHandler> logger,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result> Handle(
        RequestHistoricalEpisodeCorrectionCommand request, CancellationToken cancellationToken)
    {
        // 1. Authorize: privileged/admin-only action
        if (_currentUserService.UserId == null || !_currentUserService.IsInRole(AppRole.Admin))
        {
            throw new UnauthorizedAccessException("Only administrators are authorized to request historical episode corrections.");
        }

        // 2. Validate RowId/EventId presence
        if (request.RowId == null && string.IsNullOrWhiteSpace(request.EventId))
        {
            throw new ArgumentException("At least one of RowId or EventId must be supplied.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reason must be supplied for auditing the correction.");
        }

        // 3. Resolve target row and verify matching
        ProductPricingEpisode? episode = null;

        if (request.RowId != null)
        {
            episode = await _context.ProductPricingEpisodes
                .FirstOrDefaultAsync(pe => pe.Id == request.RowId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.EventId))
        {
            var episodeByEvent = await _context.ProductPricingEpisodes
                .FirstOrDefaultAsync(pe => pe.EventId == request.EventId, cancellationToken);

            if (episode != null && episodeByEvent != null && episode.Id != episodeByEvent.Id)
            {
                throw new ArgumentException("Supplied RowId and EventId resolve to different historical episodes.");
            }

            episode ??= episodeByEvent;
        }

        if (episode == null)
        {
            throw new NotFoundException("Historical episode not found matching the provided identifiers.");
        }

        // 4. Validate corrected snapshot values
        if (request.CorrectedDiscountPercentage != null)
        {
            var discount = request.CorrectedDiscountPercentage.Value;
            if (discount < -0.01 || discount > 15.01)
            {
                throw new ArgumentException($"Corrected discount percentage {discount}% is outside the historical schema bounds [-0.01, 15.01].");
            }
            episode.DiscountPercentage = Math.Clamp(discount, 0.0, 15.0);
        }

        if (request.CorrectedSellThroughRate != null)
        {
            episode.SellThroughRate = request.CorrectedSellThroughRate.Value;
        }

        if (request.CorrectedOutcome != null)
        {
            var validOutcomes = new[] { "SOLD_OUT", "PARTIALLY_SOLD", "UNSOLD", "EXPIRED" };
            var upperOutcome = request.CorrectedOutcome.ToUpperInvariant();
            if (!validOutcomes.Contains(upperOutcome))
            {
                throw new ArgumentException($"Corrected outcome must be one of: {string.Join(", ", validOutcomes)}");
            }
            episode.Outcome = upperOutcome;
        }

        // 5. Reset IngestedAt and IngestionCorrelationId
        var previousIngestedAt = episode.IngestedAt;
        episode.IngestedAt = null;
        episode.IngestionCorrelationId = null;

        // 6. Log correlation-scoped audit log
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = _correlationIdAccessor.GetCorrelationId() ?? Guid.NewGuid().ToString()
        });

        _logger.LogInformation("[Historical Ingestion Correction] Corrected event {EventId} (Row: {RowId}) requested by Admin {AdminId}. Previous IngestedAt: {PreviousIngestedAt}. Reason: {Reason}.",
            episode.EventId,
            episode.Id,
            _currentUserService.UserId,
            previousIngestedAt,
            request.Reason);

        _context.ProductPricingEpisodes.Update(episode);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
