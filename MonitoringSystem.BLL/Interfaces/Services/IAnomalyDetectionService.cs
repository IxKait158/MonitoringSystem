using MonitoringSystem.BLL.Models;
using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IAnomalyDetectionService
{
    AnomalyResult Analyze(MetricPoint point);

    List<AnomalyAlgorithmComparisonResult> CompareAlgorithms(
        string serviceName,
        string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries);

    List<AnomalyResult> AnalyzeBatchWithMlNet(string serviceName, string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries);
}
