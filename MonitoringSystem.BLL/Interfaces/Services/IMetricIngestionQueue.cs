using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IMetricIngestionQueue
{
    ValueTask QueueAsync(MetricIngestionEnvelope envelope, CancellationToken cancellationToken = default);

    ValueTask<MetricIngestionEnvelope> DequeueAsync(CancellationToken cancellationToken);
}
