using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.DTOs.AI;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.Common.Interfaces.AI;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Admin.Commands;
using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Features.Admin.Commands;
using FoodLoop.Infrastructure.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FoodLoop.Infrastructure.Tests.Features.AiIntegration;

public class HistoricalEpisodeCorrectionTests
{
    private readonly Mock<IAiServiceClient> _mockAiClient;
    private readonly Mock<ICorrelationIdAccessor> _mockCorrelationAccessor;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ILogger<RequestHistoricalEpisodeCorrectionCommandHandler>> _mockCorrectionLogger;
    private readonly Mock<ILogger<RunHistoricalIngestionCommandHandler>> _mockIngestionLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly HistoricalIngestionOptions _options;

    public HistoricalEpisodeCorrectionTests()
    {
        _mockAiClient = new Mock<IAiServiceClient>();
        _mockCorrelationAccessor = new Mock<ICorrelationIdAccessor>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCorrectionLogger = new Mock<ILogger<RequestHistoricalEpisodeCorrectionCommandHandler>>();
        _mockIngestionLogger = new Mock<ILogger<RunHistoricalIngestionCommandHandler>>();
        _mockTimeProvider = new Mock<TimeProvider>();

        _options = new HistoricalIngestionOptions
        {
            IntervalMinutes = 60,
            BatchSize = 10
        };

        _mockCorrelationAccessor.Setup(x => x.GetCorrelationId()).Returns("test-correlation-id");
        
        var utcNow = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        _mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(utcNow);
    }

    private RequestHistoricalEpisodeCorrectionCommandHandler CreateCorrectionHandler(ApplicationDbContext dbContext)
    {
        return new RequestHistoricalEpisodeCorrectionCommandHandler(
            dbContext,
            _mockCurrentUserService.Object,
            _mockCorrectionLogger.Object,
            _mockCorrelationAccessor.Object
        );
    }

    private RunHistoricalIngestionCommandHandler CreateIngestionHandler(ApplicationDbContext dbContext)
    {
        var mockOptions = new Mock<IOptions<HistoricalIngestionOptions>>();
        mockOptions.Setup(x => x.Value).Returns(_options);

        return new RunHistoricalIngestionCommandHandler(
            dbContext,
            _mockAiClient.Object,
            _mockCorrelationAccessor.Object,
            _mockTimeProvider.Object,
            mockOptions.Object,
            _mockIngestionLogger.Object
        );
    }

    [Fact]
    public async Task Correction_should_throw_UnauthorizedAccessException_when_user_is_not_admin()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(false);

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(Guid.NewGuid(), null, "Unauthorized correction test");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only administrators are authorized to request historical episode corrections.");
    }

    [Fact]
    public async Task Correction_should_validate_rowId_eventId_presence()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(null, null, "Missing identifiers");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("At least one of RowId or EventId must be supplied.");
    }

    [Fact]
    public async Task Correction_should_validate_reason_presence()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(Guid.NewGuid(), "some-event", "   ");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("A reason must be supplied for auditing the correction.");
    }

    [Fact]
    public async Task Correction_should_throw_ArgumentException_when_RowId_and_EventId_point_to_different_rows()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var ep1 = new ProductPricingEpisode { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), EventId = "event-1", Outcome = "UNSOLD" };
        var ep2 = new ProductPricingEpisode { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), EventId = "event-2", Outcome = "UNSOLD" };
        dbContext.ProductPricingEpisodes.AddRange(ep1, ep2);
        await dbContext.SaveChangesAsync();

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(ep1.Id, "event-2", "Conflicting identifiers");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Supplied RowId and EventId resolve to different historical episodes.");
    }

    [Fact]
    public async Task Correction_should_validate_discount_percentage_tolerance()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var ep = new ProductPricingEpisode { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), EventId = "event-1", Outcome = "UNSOLD" };
        dbContext.ProductPricingEpisodes.Add(ep);
        await dbContext.SaveChangesAsync();

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(ep.Id, null, "Correction", CorrectedDiscountPercentage: 15.02);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Corrected discount percentage 15.02% is outside the historical schema bounds [-0.01, 15.01].");
    }

    [Fact]
    public async Task Correction_should_validate_outcome_enum_membership()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var ep = new ProductPricingEpisode { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), EventId = "event-1", Outcome = "UNSOLD" };
        dbContext.ProductPricingEpisodes.Add(ep);
        await dbContext.SaveChangesAsync();

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(ep.Id, null, "Correction", CorrectedOutcome: "INVALID_OUTCOME");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Corrected outcome must be one of: SOLD_OUT, PARTIALLY_SOLD, UNSOLD, EXPIRED");
    }

    [Fact]
    public async Task Correction_should_reset_ingestion_fields_and_update_metrics_and_log_audit()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(adminId);
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var productId = Guid.NewGuid();
        var originalTime = DateTimeOffset.UtcNow.AddHours(-1);
        
        var targetEpisode = new ProductPricingEpisode 
        { 
            Id = Guid.NewGuid(), 
            ProductId = productId, 
            EventId = "event-target", 
            IngestedAt = originalTime, 
            IngestionCorrelationId = "corr-old",
            Outcome = "UNSOLD",
            DiscountPercentage = 5.0,
            SellThroughRate = 0.2
        };

        var siblingEpisode = new ProductPricingEpisode 
        { 
            Id = Guid.NewGuid(), 
            ProductId = productId, 
            EventId = "event-sibling", 
            IngestedAt = originalTime, 
            IngestionCorrelationId = "corr-old",
            Outcome = "SOLD_OUT",
            DiscountPercentage = 10.0,
            SellThroughRate = 1.0
        };

        dbContext.ProductPricingEpisodes.AddRange(targetEpisode, siblingEpisode);
        await dbContext.SaveChangesAsync();

        var handler = CreateCorrectionHandler(dbContext);
        var command = new RequestHistoricalEpisodeCorrectionCommand(
            RowId: targetEpisode.Id, 
            EventId: null, 
            Reason: "Correcting target episode details", 
            CorrectedDiscountPercentage: 8.5,
            CorrectedSellThroughRate: 0.4,
            CorrectedOutcome: "PARTIALLY_SOLD"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var dbTarget = await dbContext.ProductPricingEpisodes.FindAsync(targetEpisode.Id);
        dbTarget.Should().NotBeNull();
        dbTarget.IngestedAt.Should().BeNull();
        dbTarget.IngestionCorrelationId.Should().BeNull();
        dbTarget.DiscountPercentage.Should().Be(8.5);
        dbTarget.SellThroughRate.Should().Be(0.4);
        dbTarget.Outcome.Should().Be("PARTIALLY_SOLD");

        var dbSibling = await dbContext.ProductPricingEpisodes.FindAsync(siblingEpisode.Id);
        dbSibling.Should().NotBeNull();
        dbSibling.IngestedAt.Should().Be(originalTime);
        dbSibling.IngestionCorrelationId.Should().Be("corr-old");
        dbSibling.DiscountPercentage.Should().Be(10.0);
        dbSibling.SellThroughRate.Should().Be(1.0);
        dbSibling.Outcome.Should().Be("SOLD_OUT");
    }

    [Fact]
    public async Task IngestionSweep_should_include_deactivated_products_that_have_pending_corrected_episodes()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // This product is active and has QuantityAvailable > 0 and ExpirationDate > today.
        // It does NOT match normal candidate criteria.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Active Product with no expiry pressure",
            OriginalPrice = 100m,
            DiscountedPrice = 100m,
            QuantityAvailable = 10,
            ExpirationDate = today.AddDays(10),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-5),
            IsDeleted = false
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);

        // Add a corrected/pending episode for it (IngestedAt == null)
        var candidateEventId = $"ep-{product.Id}-nodisc";
        var pendingEpisode = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = candidateEventId,
            RecordedAt = product.CreatedAt,
            IngestedAt = null,
            Outcome = "UNSOLD",
            DiscountPercentage = 0.0,
            SellThroughRate = 0.0
        };
        dbContext.ProductPricingEpisodes.Add(pendingEpisode);
        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string> { candidateEventId }));

        var sweepHandler = CreateIngestionHandler(dbContext);

        // Act
        var result = await sweepHandler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Events.Should().HaveCount(1);
        capturedRequest.Events[0].EventId.Should().Be(candidateEventId);
        capturedRequest.Events[0].ProductId.Should().Be(product.Id.ToString());
    }

    [Fact]
    public async Task IngestionSweep_should_deduplicate_candidate_overlap_and_prioritize_corrected_snapshots()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        // This product is active but expired (Quantity == 0), so it is a standard sweep candidate.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Overlap Candidate",
            OriginalPrice = 100m,
            DiscountedPrice = 85m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);

        // Add a corrected/pending episode for it (EventId matches standard EventId, i.e. ep-prodId-nodisc)
        var candidateEventId = $"ep-{product.Id}-nodisc";
        var pendingEpisode = new ProductPricingEpisode
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            EventId = candidateEventId,
            RecordedAt = product.CreatedAt,
            IngestedAt = null, // pending
            Outcome = "EXPIRED", // corrected outcome
            DiscountPercentage = 15.0, // corrected discount
            SellThroughRate = 0.55 // corrected STR
        };
        dbContext.ProductPricingEpisodes.Add(pendingEpisode);
        await dbContext.SaveChangesAsync();

        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string> { candidateEventId }));

        var sweepHandler = CreateIngestionHandler(dbContext);

        // Act
        var result = await sweepHandler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        
        // Assert that the product appears EXACTLY once in the outbound request (no duplicates)
        capturedRequest!.Events.Should().HaveCount(1);
        var sentEvent = capturedRequest.Events[0];
        sentEvent.ProductId.Should().Be(product.Id.ToString());
        sentEvent.EventId.Should().Be(candidateEventId);
        
        // Assert that corrected snapshot values are prioritized over freshly computed values
        sentEvent.DiscountPercentage.Should().Be(15.0);
        sentEvent.SellThroughRate.Should().Be(0.55);
        sentEvent.Outcome.Should().Be("EXPIRED");

        // Verify the existing row is updated in place
        var dbEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.Id == pendingEpisode.Id);
        dbEpisode.Should().NotBeNull();
        dbEpisode!.IngestedAt.Should().BeCloseTo(_mockTimeProvider.Object.GetUtcNow(), TimeSpan.FromSeconds(5));
        dbEpisode.IngestionCorrelationId.Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task Full_Correction_E2E_Roundtrip_should_ingest_correct_and_re_ingest_correctly()
    {
        // Arrange
        using var dbContext = ApplicationDbContextFactory.Create();
        var adminId = Guid.NewGuid();

        var org = new Organization { Id = Guid.NewGuid(), Name = "Store A" };
        var category = new Category { Id = Guid.NewGuid(), Name = "Dairy" };
        var today = DateOnly.FromDateTime(_mockTimeProvider.Object.GetUtcNow().DateTime);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Title = "Roundtrip Product",
            OriginalPrice = 100m,
            DiscountedPrice = 90m,
            QuantityAvailable = 0,
            ExpirationDate = today.AddDays(5),
            Organization = org,
            Category = category,
            CreatedAt = _mockTimeProvider.Object.GetUtcNow().AddDays(-10)
        };

        dbContext.Organizations.Add(org);
        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var candidateEventId = $"ep-{product.Id}-nodisc";

        // 1. Initial Sweep Ingests Episode
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string> { candidateEventId }));

        var sweepHandler = CreateIngestionHandler(dbContext);
        var firstSweepResult = await sweepHandler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        firstSweepResult.Success.Should().BeTrue();

        var initialEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.EventId == candidateEventId);
        initialEpisode.Should().NotBeNull();
        initialEpisode!.IngestedAt.Should().NotBeNull();
        initialEpisode.IngestionCorrelationId.Should().Be("test-correlation-id");
        initialEpisode.DiscountPercentage.Should().Be(10.0); // Computed (100 - 90) / 100 * 100

        // 2. Admin Corrects Episode (Clears IngestedAt, sets CorrectedDiscountPercentage = 12.0)
        _mockCurrentUserService.Setup(x => x.UserId).Returns(adminId);
        _mockCurrentUserService.Setup(x => x.IsInRole(AppRole.Admin)).Returns(true);

        var correctionHandler = CreateCorrectionHandler(dbContext);
        var correctionCommand = new RequestHistoricalEpisodeCorrectionCommand(
            RowId: initialEpisode.Id,
            EventId: null,
            Reason: "Admin corrected discount percentage",
            CorrectedDiscountPercentage: 12.0
        );
        var correctionResult = await correctionHandler.Handle(correctionCommand, CancellationToken.None);
        correctionResult.Success.Should().BeTrue();

        var correctedEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.Id == initialEpisode.Id);
        correctedEpisode.Should().NotBeNull();
        correctedEpisode!.IngestedAt.Should().BeNull(); // Reset
        correctedEpisode.DiscountPercentage.Should().Be(12.0); // Updated

        // 3. Second Sweep Re-ingests Corrected Episode under same EventId
        HistoricalIngestionRequestDto? capturedRequest = null;
        _mockAiClient.Setup(x => x.IngestHistoricalPricingAsync(It.IsAny<HistoricalIngestionRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<HistoricalIngestionRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HistoricalIngestionResponseDto(1, 1, 0, new List<string> { candidateEventId }));

        var secondSweepResult = await sweepHandler.Handle(new RunHistoricalIngestionCommand(), CancellationToken.None);
        secondSweepResult.Success.Should().BeTrue();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Events.Should().HaveCount(1);
        capturedRequest.Events[0].EventId.Should().Be(candidateEventId);
        capturedRequest.Events[0].DiscountPercentage.Should().Be(12.0); // corrected value sent!

        var finalEpisode = await dbContext.ProductPricingEpisodes.FirstOrDefaultAsync(pe => pe.Id == initialEpisode.Id);
        finalEpisode.Should().NotBeNull();
        finalEpisode!.IngestedAt.Should().NotBeNull(); // re-ingested!
        finalEpisode.IngestionCorrelationId.Should().Be("test-correlation-id");
    }
}
