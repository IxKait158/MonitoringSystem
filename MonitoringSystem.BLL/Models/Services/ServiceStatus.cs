namespace MonitoringSystem.BLL.Models.Services;

public class ServiceStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastSeen { get; set; }
    public int AnomalyCount { get; set; }
    public Dictionary<string, double> LatestMetrics { get; set; } = new();

    public string StatusKey => $"{ServiceName}:{InstanceId}";
}
