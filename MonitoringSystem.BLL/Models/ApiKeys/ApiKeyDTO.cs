namespace MonitoringSystem.BLL.Models.ApiKeys;

public class ApiKeyDTO
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; }
    public int ServiceCount { get; set; }
}
