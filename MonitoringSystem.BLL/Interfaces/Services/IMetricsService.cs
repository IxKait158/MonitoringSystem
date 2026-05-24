using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IMetricsService
{
    Task IngestAsync(MetricIngestionRequest request);

    Task<List<MetricPoint>> GetMetricsAsync(string serviceName, string? instanceId, string metricName, DateTime from,
        DateTime to);

    List<AnomalyResult> GetRecentAnomaliesAsync(int count);

    Task<AnomalyAlgorithmComparisonResponse> CompareAnomalyAlgorithmsAsync(
        AnomalyAlgorithmComparisonRequest request);

    List<ServiceStatus> GetServiceStatuses();

    Task RefreshServiceHealthAsync(TimeSpan timeout);
}
