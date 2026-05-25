namespace MonitoringSystem.BLL.Models.Metrics;

public class MetricPointDTO
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}
