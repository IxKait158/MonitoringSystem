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
    IServicesRepository servicesRepository,
    IHubContext<MetricsHub> hubContext) : IMetricsService
{
    // Ключ: serviceId. Статус живе у пам'яті, відновлюється з потоку метрик.
    private static readonly ConcurrentDictionary<int, ServiceStatus> ServiceStatuses = new();
    private static readonly Lock ServiceStatusLock = new();

    public async Task IngestAsync(ApiKeyEntity apiKey, MetricIngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            throw new Exception("Ім'я сервісу обов'язкове");

        var serviceName = request.ServiceName.Trim();
        var service = await servicesRepository.FindByApiKeyAndNameAsync(apiKey.Id, serviceName);
        if (service == null)
            throw new Exception(
                $"Сервіс '{serviceName}' не зареєстровано під цим API ключем. " +
                "Спочатку зареєструйте сервіс з таким ім'ям.");

        var anomalies = new List<AnomalyResult>();

        foreach (var metric in request.Metrics)
        {
            metric.ServiceName = serviceName;

            await metricPointRepository.AddAsync(new MetricPointEntity
            {
                ServiceId = service.Id,
                MetricName = metric.MetricName,
                Value = metric.Value,
                Timestamp = metric.Timestamp,
                Tags = metric.Tags
            });

            var anomaly = anomalyDetectionService.Analyze(service.Id, serviceName, metric);
            if (anomaly.IsAnomaly)
            {
                anomalies.Add(anomaly);

                await anomalyRepository.AddAsync(new AnomalyEntity
                {
                    ServiceId = service.Id,
                    MetricName = anomaly.MetricName,
                    Value = anomaly.Value,
                    ExpectedValue = anomaly.ExpectedValue,
                    AnomalyScore = anomaly.AnomalyScore,
                    IsAnomaly = true,
                    DetectedAt = anomaly.DetectedAt
                });
            }

            UpdateServiceStatus(service.Id, serviceName, metric, anomaly.IsAnomaly);
        }

        var group = GroupName(apiKey.Id);
        await hubContext.Clients.Group(group).SendAsync("MetricsUpdated", request.Metrics);

        if (anomalies.Count != 0)
            await hubContext.Clients.Group(group).SendAsync("AnomaliesDetected", anomalies);

        await hubContext.Clients.Group(group)
            .SendAsync("ServiceStatusUpdated", await GetServiceStatusesAsync(apiKey));
    }

    public async Task<List<MetricPointDTO>> GetMetricsAsync(
        ApiKeyEntity apiKey,
        string serviceName,
        string metricName,
        DateTime from,
        DateTime to)
    {
        var service = await servicesRepository.FindByApiKeyAndNameAsync(apiKey.Id, serviceName);
        if (service == null)
            return [];

        var query = metricPointRepository.GetAll(m =>
            m.ServiceId == service.Id &&
            m.MetricName == metricName &&
            m.Timestamp >= from &&
            m.Timestamp <= to);

        return query
            .OrderBy(m => m.Timestamp)
            .Select(e => new MetricPointDTO
            {
                Id = e.Id,
                ServiceName = serviceName,
                MetricName = e.MetricName,
                Value = e.Value,
                Timestamp = e.Timestamp,
                Tags = e.Tags
            })
            .ToList();
    }

    public async Task<List<AnomalyResult>> GetRecentAnomaliesAsync(ApiKeyEntity apiKey, string metricName, int count)
    {
        var services = await servicesRepository.GetByApiKeyAsync(apiKey.Id);
        if (services.Count == 0)
            return [];

        var serviceIds = services.Select(s => s.Id).ToList();
        var entities = string.IsNullOrEmpty(metricName) 
            ? await anomalyRepository.GetAllRecentAnomaliesAsync(serviceIds, count) 
            : await anomalyRepository.GetRecentAnomaliesByMetricAsync(serviceIds, metricName, count);

        return entities.Select(e => new AnomalyResult
        {
            ServiceName = e.Service?.Name ?? string.Empty,
            MetricName = e.MetricName,
            Value = e.Value,
            ExpectedValue = e.ExpectedValue,
            AnomalyScore = e.AnomalyScore,
            IsAnomaly = e.IsAnomaly,
            DetectedAt = e.DetectedAt
        }).ToList();
    }

    public async Task<SrCnnBatchAnalysisResponse> AnalyzeSrCnnBatchAsync(
        ApiKeyEntity apiKey,
        SrCnnBatchAnalysisRequest request)
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

        var metrics = await GetMetricsAsync(apiKey, request.ServiceName, request.MetricName, from, to);

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

    public async Task<List<ServiceStatus>> GetServiceStatusesAsync(ApiKeyEntity apiKey)
    {
        var services = await servicesRepository.GetByApiKeyAsync(apiKey.Id);

        lock (ServiceStatusLock)
        {
            return services
                .Select(s =>
                {
                    if (ServiceStatuses.TryGetValue(s.Id, out var status))
                        return CloneStatus(status);

                    return new ServiceStatus
                    {
                        ServiceName = s.Name,
                        IsHealthy = false,
                        LastSeen = DateTime.MinValue,
                        AnomalyCount = 0,
                        LatestMetrics = new()
                    };
                })
                .OrderBy(s => s.ServiceName)
                .ToList();
        }
    }

    public async Task RefreshServiceHealthAsync(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        var services = servicesRepository.GetAll().ToList();
        var changedApiKeys = new HashSet<int>();

        lock (ServiceStatusLock)
        {
            foreach (var svc in services)
            {
                if (!ServiceStatuses.TryGetValue(svc.Id, out var status))
                    continue;

                var isHealthy = now - ToUtc(status.LastSeen) <= timeout;
                if (status.IsHealthy == isHealthy)
                    continue;

                status.IsHealthy = isHealthy;
                changedApiKeys.Add(svc.ApiKeyId);
            }
        }

        foreach (var apiKeyId in changedApiKeys)
        {
            var apiServices = services.Where(s => s.ApiKeyId == apiKeyId).ToList();

            List<ServiceStatus> snapshot;
            lock (ServiceStatusLock)
            {
                snapshot = apiServices
                    .Select(s => ServiceStatuses.TryGetValue(s.Id, out var st)
                        ? CloneStatus(st)
                        : new ServiceStatus { ServiceName = s.Name, IsHealthy = false })
                    .OrderBy(s => s.ServiceName)
                    .ToList();
            }

            await hubContext.Clients
                .Group(GroupName(apiKeyId))
                .SendAsync("ServiceStatusUpdated", snapshot);
        }
    }

    public async Task<List<string>> GetMetricsNamesAsync(ApiKeyEntity apiKey)
    {
        var services = await servicesRepository.GetByApiKeyAsync(apiKey.Id);
        if (services.Count == 0)
            return [];

        var serviceIds = services.Select(s => s.Id);
        var entities = metricPointRepository.GetAllNoTracking(x => serviceIds.Contains(x.ServiceId));
        
        return entities.Select(x => x.MetricName).Distinct().ToList();
    }

    private static void UpdateServiceStatus(int serviceId, string serviceName, MetricPointDTO metric, bool hasAnomaly)
    {
        lock (ServiceStatusLock)
        {
            var status = ServiceStatuses.GetOrAdd(
                serviceId,
                _ => new ServiceStatus
                {
                    ServiceName = serviceName,
                    IsHealthy = true
                });

            status.ServiceName = serviceName;
            status.LastSeen = ToUtc(metric.Timestamp);
            status.IsHealthy = true;
            status.LatestMetrics[metric.MetricName] = metric.Value;

            if (hasAnomaly)
                status.AnomalyCount++;
        }
    }

    private static string GroupName(int apiKeyId) => $"apiKey:{apiKeyId}";

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
