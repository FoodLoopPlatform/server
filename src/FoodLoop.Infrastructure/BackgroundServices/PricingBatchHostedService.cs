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

public class PricingBatchHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<PricingBatchOptions> _optionsMonitor;
    private readonly ILogger<PricingBatchHostedService> _logger;

    public PricingBatchHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<PricingBatchOptions> optionsMonitor,
        ILogger<PricingBatchHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Pricing Batch Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = _optionsMonitor.CurrentValue.IntervalMinutes;
            if (intervalMinutes <= 0)
            {
                intervalMinutes = 60; // Sane fallback
            }

            _logger.LogInformation("Starting scheduled AI pricing batch run. Next run in {Minutes} minutes.", intervalMinutes);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new RunPricingBatchCommand(), stoppingToken);

                _logger.LogInformation("AI pricing batch run completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during background AI pricing batch execution.");
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

        _logger.LogInformation("AI Pricing Batch Background Service stopped.");
    }
}
