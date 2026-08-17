using FoodLoop.Application.Features.AiIntegration.Commands;
using FoodLoop.Infrastructure.Options;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.BackgroundServices;

public class HistoricalIngestionHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<HistoricalIngestionOptions> _optionsMonitor;
    private readonly ILogger<HistoricalIngestionHostedService> _logger;

    public HistoricalIngestionHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<HistoricalIngestionOptions> optionsMonitor,
        ILogger<HistoricalIngestionHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Historical Ingestion Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = _optionsMonitor.CurrentValue.IntervalMinutes;
            if (intervalMinutes <= 0)
            {
                intervalMinutes = 60; // Sane fallback
            }

            _logger.LogInformation("Starting scheduled AI historical pricing ingestion sweep. Next run in {Minutes} minutes.", intervalMinutes);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new RunHistoricalIngestionCommand(), stoppingToken);

                _logger.LogInformation("AI historical pricing ingestion sweep completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during background AI historical pricing ingestion sweep execution.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal termination when stoppingToken is cancelled
                break;
            }
        }

        _logger.LogInformation("AI Historical Ingestion Background Service stopped.");
    }
}
