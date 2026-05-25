using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MonitoringSystem.BLL.Hubs;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;
using MonitoringSystem.BLL.Models.Services;
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

    public async Task IngestAsync(MetricIngestionRequest request)
    {
        if (string.IsNullOrEmpty(request.ServiceName))
            throw new Exception("Ім'я сервісу обов'язкове");
        
        var anomalies = new List<AnomalyResult>();
        var serviceName = request.ServiceName.Trim();

        foreach (var metric in request.Metrics)
        {
            metric.ServiceName = serviceName;

            await metricPointRepository.AddAsync(new MetricPointEntity
            {
                Id = metric.Id,
                ServiceName = metric.ServiceName,
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
        string metricName,
        DateTime from,
        DateTime to)
    {
        var query = metricPointRepository.GetAll(m =>
            m.ServiceName == serviceName &&
            m.MetricName == metricName &&
            m.Timestamp >= from &&
            m.Timestamp <= to);

        var entities = query
            .OrderBy(m => m.Timestamp);

        return entities.Select(e => new MetricPoint
        {
            Id = e.Id,
            ServiceName = e.ServiceName,
            MetricName = e.MetricName,
            Value = e.Value,
            Timestamp = e.Timestamp,
            Tags = e.Tags
        }).ToList();
    }

    public List<AnomalyResult> GetRecentAnomaliesAsync(int count = 20)
    {
        var entities = anomalyRepository.GetRecentAnomalies(count).ToList();

        return entities.Select(e => new AnomalyResult
        {
            ServiceName = e.ServiceName,
            MetricName = e.MetricName,
            Value = e.Value,
            ExpectedValue = e.ExpectedValue,
            AnomalyScore = e.AnomalyScore,
            IsAnomaly = e.IsAnomaly,
            DetectedAt = e.DetectedAt
        }).ToList();
    }

    public async Task<SrCnnBatchAnalysisResponse> AnalyzeSrCnnBatchAsync(SrCnnBatchAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            throw new Exception("Ім'я сервісу обов'язкове");

        if (string.IsNullOrWhiteSpace(request.MetricName))
            throw new Exception("Ім'я метрики обов'язкове");

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            throw new Exception("Дата від має бути раніше, ніж дата до");

        var sensitivity = request.Sensitivity ?? 0.3;
        if (sensitivity is < 0.0 or > 1.0)
            throw new Exception("Чутливість має бути в діапазоні [0.0, 1.0]");

        var from = request.From ?? DateTime.UtcNow.AddDays(-1);
        var to = request.To ?? DateTime.UtcNow;

        var metrics = await GetMetricsAsync(request.ServiceName, request.MetricName, from, to);

        var timeSeries = metrics
            .OrderBy(x => x.Timestamp)
            .Select(x => (x.Timestamp, x.Value))
            .ToList();

        if (timeSeries.Count < 12)
            throw new Exception(
                $"Недостатньо даних для SrCnn-аналізу (потрібно ≥ 12 точок, знайдено {timeSeries.Count}). Розширте часовий діапазон.");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var detections = anomalyDetectionService.AnalyzeBatchWithMlNet(
            request.ServiceName, request.MetricName, timeSeries, sensitivity);
        stopwatch.Stop();

        var points = detections.Select(d => new SrCnnBatchPoint
        {
            Timestamp = d.DetectedAt,
            Value = d.Value,
            AnomalyScore = d.AnomalyScore,
            IsAnomaly = d.IsAnomaly,
            Severity = d.Severity
        }).ToList();

        var anomalies = points
            .Where(p => p.IsAnomaly)
            .OrderByDescending(p => p.AnomalyScore)
            .ToList();

        return new SrCnnBatchAnalysisResponse
        {
            ServiceName = request.ServiceName,
            MetricName = request.MetricName,
            From = from,
            To = to,
            Sensitivity = sensitivity,
            TotalPoints = points.Count,
            AnomalyCount = anomalies.Count,
            CriticalCount = anomalies.Count(a => a.Severity == "Critical"),
            WarningCount = anomalies.Count(a => a.Severity == "Warning"),
            InfoCount = anomalies.Count(a => a.Severity == "Info"),
            AverageScore = points.Count == 0 ? 0 : points.Average(p => p.AnomalyScore),
            MaxScore = points.Count == 0 ? 0 : points.Max(p => p.AnomalyScore),
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            Points = points,
            Anomalies = anomalies
        };
    }

    public async Task<AnomalyAlgorithmComparisonResponse> CompareAnomalyAlgorithmsAsync(
        AnomalyAlgorithmComparisonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            throw new Exception("Ім'я сервісу обов'язкове");

        if (string.IsNullOrWhiteSpace(request.MetricName))
            throw new Exception("Ім'я метрики обов'язкове");

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            throw new Exception("Дата від має бути раніше, ніж дата до");
        
        var from = request.From ?? DateTime.UtcNow.AddHours(-1);
        var to = request.To ?? DateTime.UtcNow;

        var metrics = await GetMetricsAsync(
            request.ServiceName,
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
            MetricName = request.MetricName,
            From = from,
            To = to,
            TotalPoints = timeSeries.Count,
            Algorithms = anomalyDetectionService.CompareAlgorithms(
                request.ServiceName,
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
        lock (ServiceStatusLock)
        {
            var status = ServiceStatuses.GetOrAdd(
                metric.ServiceName,
                _ => new ServiceStatus
                {
                    ServiceName = metric.ServiceName,
                    IsHealthy = true
                });

            status.ServiceName = metric.ServiceName;
            status.LastSeen = ToUtc(metric.Timestamp);
            status.IsHealthy = true;
            status.LatestMetrics[metric.MetricName] = metric.Value;

            if (hasAnomaly)
                status.AnomalyCount++;
        }
    }

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
            IsHealthy = status.IsHealthy,
            LastSeen = status.LastSeen,
            AnomalyCount = status.AnomalyCount,
            LatestMetrics = new Dictionary<string, double>(status.LatestMetrics)
        };
}
