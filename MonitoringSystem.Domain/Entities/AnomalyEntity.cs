using MonitoringSystem.BLL.Interfaces.Entities;

namespace MonitoringSystem.Domain.Entities;

public class AnomalyEntity : IEntity
{
    public int Id { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double ExpectedValue { get; set; }
    public double AnomalyScore { get; set; }
    public bool IsAnomaly { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public int ServiceId { get; set; }
    public ServiceEntity? Service { get; set; }
}
