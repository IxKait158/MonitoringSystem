namespace MonitoringSystem.BLL.Models.Anomalies;

public class AnomalyAlgorithmComparisonResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public string? InstanceId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalPoints { get; set; }
    public List<AnomalyAlgorithmComparisonResult> Algorithms { get; set; } = new();
}
