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

public class MonitoringScannerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<MonitoringScannerOptions> _optionsMonitor;
    private readonly ILogger<MonitoringScannerHostedService> _logger;

    public MonitoringScannerHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<MonitoringScannerOptions> optionsMonitor,
        ILogger<MonitoringScannerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Monitoring Scanner Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = _optionsMonitor.CurrentValue.IntervalMinutes;
            if (intervalMinutes <= 0)
            {
                intervalMinutes = 60; // Sane fallback
            }

            _logger.LogInformation("Starting scheduled AI monitoring scan. Next run in {Minutes} minutes.", intervalMinutes);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                await mediator.Send(new RunMonitoringScanCommand(), stoppingToken);

                _logger.LogInformation("AI monitoring scan run completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during background AI monitoring scan execution.");
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

        _logger.LogInformation("AI Monitoring Scanner Background Service stopped.");
    }
}
