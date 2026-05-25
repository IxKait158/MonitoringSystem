using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Models.Metrics;

public class MetricIngestionEnvelope
{
    public ApiKeyEntity ApiKey { get; init; } = null!;
    public MetricIngestionRequest Request { get; init; } = new();
}
