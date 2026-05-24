namespace MonitoringSystem.BLL.Models;

public class ServiceHealthOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public int CheckIntervalSeconds { get; set; } = 5;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
    public TimeSpan CheckInterval => TimeSpan.FromSeconds(CheckIntervalSeconds);
}