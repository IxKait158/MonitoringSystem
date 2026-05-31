using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IAnomalyDetectionService
{
    AnomalyResult Analyze(int serviceId, string serviceName, MetricPointDTO pointDto);

    List<AnomalyResult> AnalyzeBatchWithMlNet(
        string serviceName,
        string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries,
        double threshold = 0.3);
}
