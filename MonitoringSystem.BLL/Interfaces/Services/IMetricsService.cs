using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;
using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IMetricsService
{
    Task IngestAsync(ApiKeyEntity apiKey, MetricIngestionRequest request);

    Task<List<MetricPointDTO>> GetMetricsAsync(
        ApiKeyEntity apiKey,
        string serviceName,
        string metricName,
        DateTime from,
        DateTime to);

    Task<List<AnomalyResult>> GetRecentAnomaliesAsync(ApiKeyEntity apiKey, string? metricName, int count);

    Task<SrCnnBatchAnalysisResponse> AnalyzeSrCnnBatchAsync(
        ApiKeyEntity apiKey,
        SrCnnBatchAnalysisRequest request);

    Task<List<ServiceStatus>> GetServiceStatusesAsync(ApiKeyEntity apiKey);

    Task RefreshServiceHealthAsync(TimeSpan timeout);

    Task<List<string>> GetMetricsNamesAsync(ApiKeyEntity apiKey);
}
