namespace MonitoringSystem.BLL.Models.Metrics;

public class MetricIngestionRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public List<MetricPoint> Metrics { get; set; } = new();
}
