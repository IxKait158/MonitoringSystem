using System.Threading.Channels;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Services;

public class MetricIngestionQueue : IMetricIngestionQueue
{
    private readonly Channel<MetricIngestionEnvelope> _queue = Channel.CreateBounded<MetricIngestionEnvelope>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(MetricIngestionEnvelope envelope, CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(envelope, cancellationToken);

    public ValueTask<MetricIngestionEnvelope> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAsync(cancellationToken);
}
