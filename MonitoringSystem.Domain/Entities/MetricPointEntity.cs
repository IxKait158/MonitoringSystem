using MonitoringSystem.Domain.Interfaces;

namespace MonitoringSystem.Domain.Entities;

public class MetricPointEntity : IEntity
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Tags { get; set; } = new();
}
