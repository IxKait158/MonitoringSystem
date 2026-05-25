namespace MonitoringSystem.BLL.Models.Metrics;

public class MetricIngestionRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public List<MetricPointDTO> Metrics { get; set; } = new();
}
