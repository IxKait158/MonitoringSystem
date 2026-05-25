using MonitoringSystem.BLL.Interfaces.Entities;

namespace MonitoringSystem.Domain.Entities;

public class MetricPointEntity : IEntity
{
    public int Id { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Tags { get; set; } = new();
    
    public int ServiceId { get; set; }
    public ServiceEntity Service { get; set; } = new();
}
