namespace MonitoringSystem.BLL.Models.Anomalies;

public class SrCnnBatchAnalysisRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public double? Sensitivity { get; set; }
}
