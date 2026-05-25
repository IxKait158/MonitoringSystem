using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IAnomalyDetectionService
{
    AnomalyResult Analyze(int serviceId, string serviceName, MetricPoint point);

    List<AnomalyAlgorithmComparisonResult> CompareAlgorithms(
        string serviceName,
        string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries);

    List<AnomalyResult> AnalyzeBatchWithMlNet(
        string serviceName,
        string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries,
        double threshold = 0.3);
}
