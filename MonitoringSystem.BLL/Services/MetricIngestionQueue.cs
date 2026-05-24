using System.Threading.Channels;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.BLL.Services;

public class MetricIngestionQueue : IMetricIngestionQueue
{
    private readonly Channel<MetricIngestionRequest> queue = Channel.CreateBounded<MetricIngestionRequest>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(MetricIngestionRequest request, CancellationToken cancellationToken = default) =>
        queue.Writer.WriteAsync(request, cancellationToken);

    public ValueTask<MetricIngestionRequest> DequeueAsync(CancellationToken cancellationToken) =>
        queue.Reader.ReadAsync(cancellationToken);
}
