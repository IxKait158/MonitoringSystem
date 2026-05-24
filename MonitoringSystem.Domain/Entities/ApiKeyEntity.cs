using MonitoringSystem.Domain.Interfaces;

namespace MonitoringSystem.Domain.Entities;

public class ApiKeyEntity : IEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastUsedAt { get; set; }
}