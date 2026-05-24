using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;
using MonitoringSystem.BLL.Models.Services;

namespace MonitoringSystem.BLL.Services;

public class ServiceHealthBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ServiceHealthOptions> options,
    ILogger<ServiceHealthBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.CheckInterval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();

                await metricsService.RefreshServiceHealthAsync(options.Value.Timeout);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh service health statuses.");
            }
        }
    }
}
