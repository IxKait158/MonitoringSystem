using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Services;

public class MetricIngestionBackgroundService(
    IMetricIngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MetricIngestionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MetricIngestionRequest request;

            try
            {
                request = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

                await metricsService.IngestAsync(request);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Не вдалося отримати показники з черги для служби {ServiceName}. Кількість показників: {MetricsCount}",
                    request.ServiceName,
                    request.Metrics.Count);
            }
        }
    }
}
