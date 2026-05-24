namespace MonitoringSystem.BLL.Models;

public class MetricPoint
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string ServiceName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Tags { get; set; } = new();
}
