using MonitoringSystem.BLL.Models;
using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;
using MonitoringSystem.BLL.Models.Services;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IMetricsService
{
    Task IngestAsync(MetricIngestionRequest request);

    Task<List<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTime from,
        DateTime to);

    List<AnomalyResult> GetRecentAnomaliesAsync(int count);

    Task<AnomalyAlgorithmComparisonResponse> CompareAnomalyAlgorithmsAsync(
        AnomalyAlgorithmComparisonRequest request);

    Task<SrCnnBatchAnalysisResponse> AnalyzeSrCnnBatchAsync(SrCnnBatchAnalysisRequest request);

    List<ServiceStatus> GetServiceStatuses();

    Task RefreshServiceHealthAsync(TimeSpan timeout);
}
