using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.BLL.Services;

namespace MonitoringSystem.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBLL(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceHealthOptions>(configuration.GetSection("ServiceHealth"));
        
        services.AddSingleton<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddSingleton<IMetricIngestionQueue, MetricIngestionQueue>();
        services.AddScoped<IMetricsService, MetricsService>();
        services.AddScoped<IApiKeysService, ApiKeysService>();

        services.AddHostedService<MetricIngestionBackgroundService>();
        services.AddHostedService<ServiceHealthBackgroundService>();
        
        return services;
    }
}