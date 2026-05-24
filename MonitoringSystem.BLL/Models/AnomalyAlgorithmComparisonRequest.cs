namespace MonitoringSystem.BLL.Models;

public class AnomalyAlgorithmComparisonRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string? InstanceId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
