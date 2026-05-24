using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IAnomalyDetectionService
{
    AnomalyResult Analyze(MetricPoint point);

    List<AnomalyAlgorithmComparisonResult> CompareAlgorithms(
        string serviceName,
        string? instanceId,
        string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries);

    List<AnomalyResult> AnalyzeBatchWithMlNet(string serviceName, string? instanceId, string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries);
}
