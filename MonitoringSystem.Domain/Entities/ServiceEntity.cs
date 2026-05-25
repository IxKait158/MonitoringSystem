using MonitoringSystem.BLL.Interfaces.Entities;

namespace MonitoringSystem.Domain.Entities;

public class ServiceEntity : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public int ApiKeyId { get; set; }
    public ApiKeyEntity ApiKey { get; set; } = new();
    
    public ICollection<AnomalyEntity> Anomalies { get; set; }
    public ICollection<MetricPointEntity> MetricPoints { get; set; }
}