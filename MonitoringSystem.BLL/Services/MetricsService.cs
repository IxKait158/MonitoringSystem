using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MonitoringSystem.BLL.Hubs;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Services;

public class MetricsService(
    IMetricPointRepository metricPointRepository,
    IAnomalyRepository anomalyRepository,
    IAnomalyDetectionService anomalyDetectionService,
    IHubContext<MetricsHub> hubContext) : IMetricsService
{
    private static readonly ConcurrentDictionary<string, ServiceStatus> ServiceStatuses = new();
    private static readonly object ServiceStatusLock = new();
    private const string DefaultInstanceId = "default";

    public async Task IngestAsync(MetricIngestionRequest request)
    {
        var anomalies = new List<AnomalyResult>();
        var serviceName = request.ServiceName.Trim();
        var instanceId = NormalizeInstanceId(request.InstanceId);

        foreach (var metric in request.Metrics)
        {
            metric.ServiceName = serviceName;
            metric.InstanceId = string.IsNullOrWhiteSpace(metric.InstanceId)
                ? instanceId
                : NormalizeInstanceId(metric.InstanceId);

            await metricPointRepository.AddAsync(new MetricPointEntity
            {
                Id = metric.Id,
                ServiceName = metric.ServiceName,
                InstanceId = metric.InstanceId,
                MetricName = metric.MetricName,
                Value = metric.Value,
                Timestamp = metric.Timestamp,
                Tags = metric.Tags
            });

            var anomaly = anomalyDetectionService.Analyze(metric);
            if (anomaly.IsAnomaly)
            {
                anomalies.Add(anomaly);

                await anomalyRepository.AddAsync(new AnomalyEntity
                {
                    ServiceName = anomaly.ServiceName,
                    InstanceId = anomaly.InstanceId,
                    MetricName = anomaly.MetricName,
                    Value = anomaly.Value,
                    ExpectedValue = anomaly.ExpectedValue,
                    AnomalyScore = anomaly.AnomalyScore,
                    IsAnomaly = true,
                    DetectedAt = anomaly.DetectedAt
                });
            }

            UpdateServiceStatus(metric, anomaly.IsAnomaly);
        }
        
        await hubContext.Clients.All.SendAsync("MetricsUpdated", request.Metrics);

        if (anomalies.Count != 0)
            await hubContext.Clients.All.SendAsync("AnomaliesDetected", anomalies);

        await hubContext.Clients.All.SendAsync("ServiceStatusUpdated", GetServiceStatuses());
    }

    public async Task<List<MetricPoint>> GetMetricsAsync(
        string serviceName,
        string? instanceId,
        string metricName,
        DateTime from,
        DateTime to)
    {
        var query = metricPointRepository.GetAll()
            .Where(m =>
                m.ServiceName == serviceName &&
                m.MetricName == metricName &&
                m.Timestamp >= from &&
                m.Timestamp <= to)
            .ToList();

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            var normalizedInstanceId = NormalizeInstanceId(instanceId);
            query = query.Where(m => m.InstanceId == normalizedInstanceId).ToList();
        }

        var entities = query
            .OrderBy(m => m.Timestamp);

        return entities.Select(e => new MetricPoint
        {
            Id = e.Id,
            ServiceName = e.ServiceName,
            InstanceId = e.InstanceId,
            MetricName = e.MetricName,
            Value = e.Value,
            Timestamp = e.Timestamp,
            Tags = e.Tags
        }).ToList();
    }

    public List<AnomalyResult> GetRecentAnomaliesAsync(int count = 20)
    {
        var entities = anomalyRepository.GetAllNoTracking()
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToList();

        return entities.Select(e => new AnomalyResult
        {
            ServiceName = e.ServiceName,
            InstanceId = e.InstanceId,
            MetricName = e.MetricName,
            Value = e.Value,
            ExpectedValue = e.ExpectedValue,
            AnomalyScore = e.AnomalyScore,
            IsAnomaly = e.IsAnomaly,
            DetectedAt = e.DetectedAt
        }).ToList();
    }

    public async Task<AnomalyAlgorithmComparisonResponse> CompareAnomalyAlgorithmsAsync(
        AnomalyAlgorithmComparisonRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddHours(-1);
        var to = request.To ?? DateTime.UtcNow;

        var metrics = await GetMetricsAsync(
            request.ServiceName,
            request.InstanceId,
            request.MetricName,
            from,
            to);

        var timeSeries = metrics
            .OrderBy(x => x.Timestamp)
            .Select(x => (x.Timestamp, x.Value))
            .ToList();

        return new AnomalyAlgorithmComparisonResponse
        {
            ServiceName = request.ServiceName,
            InstanceId = request.InstanceId,
            MetricName = request.MetricName,
            From = from,
            To = to,
            TotalPoints = timeSeries.Count,
            Algorithms = anomalyDetectionService.CompareAlgorithms(
                request.ServiceName,
                request.InstanceId,
                request.MetricName,
                timeSeries)
        };
    }

    public List<ServiceStatus> GetServiceStatuses()
    {
        lock (ServiceStatusLock)
        {
            return ServiceStatuses.Values
                .OrderBy(s => s.ServiceName)
                .ThenBy(s => s.InstanceId)
                .Select(CloneStatus)
                .ToList();
        }
    }

    public async Task RefreshServiceHealthAsync(TimeSpan timeout)
    {
        var changed = false;
        var now = DateTime.UtcNow;

        lock (ServiceStatusLock)
        {
            foreach (var status in ServiceStatuses.Values)
            {
                var isHealthy = now - ToUtc(status.LastSeen) <= timeout;
                if (status.IsHealthy == isHealthy)
                    continue;

                status.IsHealthy = isHealthy;
                changed = true;
            }
        }

        if (changed)
            await hubContext.Clients.All.SendAsync("ServiceStatusUpdated", GetServiceStatuses());
    }

    private static void UpdateServiceStatus(MetricPoint metric, bool hasAnomaly)
    {
        var key = CreateStatusKey(metric.ServiceName, metric.InstanceId);

        lock (ServiceStatusLock)
        {
            var status = ServiceStatuses.GetOrAdd(
                key,
                _ => new ServiceStatus
                {
                    ServiceName = metric.ServiceName,
                    InstanceId = metric.InstanceId,
                    IsHealthy = true
                });

            status.ServiceName = metric.ServiceName;
            status.InstanceId = metric.InstanceId;
            status.LastSeen = ToUtc(metric.Timestamp);
            status.IsHealthy = true;
            status.LatestMetrics[metric.MetricName] = metric.Value;

            if (hasAnomaly)
                status.AnomalyCount++;
        }
    }

    private static string CreateStatusKey(string serviceName, string instanceId) =>
        $"{serviceName}:{NormalizeInstanceId(instanceId)}";

    private static string NormalizeInstanceId(string? instanceId) =>
        string.IsNullOrWhiteSpace(instanceId) ? DefaultInstanceId : instanceId.Trim();

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static ServiceStatus CloneStatus(ServiceStatus status) =>
        new()
        {
            ServiceName = status.ServiceName,
            InstanceId = status.InstanceId,
            IsHealthy = status.IsHealthy,
            LastSeen = status.LastSeen,
            AnomalyCount = status.AnomalyCount,
            LatestMetrics = new Dictionary<string, double>(status.LatestMetrics)
        };
}
