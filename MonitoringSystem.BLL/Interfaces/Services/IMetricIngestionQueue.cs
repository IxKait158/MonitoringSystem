using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IMetricIngestionQueue
{
    ValueTask QueueAsync(MetricIngestionRequest request, CancellationToken cancellationToken = default);
    
    ValueTask<MetricIngestionRequest> DequeueAsync(CancellationToken cancellationToken);
}