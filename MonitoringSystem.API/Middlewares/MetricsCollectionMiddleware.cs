using System.Diagnostics;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.Middlewares;

public class MetricsCollectionMiddleware(
    RequestDelegate next,
    string serviceName)
{
    private static long _requestCount = 0;
    private static long _errorCount = 0;

    public async Task InvokeAsync(HttpContext context, IMetricIngestionQueue ingestionQueue)
    {
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
        {
            await next(context);
            return;
        }
        
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _requestCount);

        try
        {
            await next(context);

            if (context.Response.StatusCode >= 500)
                Interlocked.Increment(ref _errorCount);
        }
        catch
        {
            Interlocked.Increment(ref _errorCount);
            throw;
        }
        finally
        {
            sw.Stop();

            var metrics = new List<MetricPoint>
            {
                new()
                {
                    ServiceName = serviceName,
                    MetricName = "http.response_time_ms",
                    Value = sw.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow,
                    Tags = new() { ["path"] = context.Request.Path, ["method"] = context.Request.Method }
                },
                new()
                {
                    ServiceName = serviceName,
                    MetricName = "system.memory_mb",
                    Value = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                    Timestamp = DateTime.UtcNow
                },
                new()
                {
                    ServiceName = serviceName,
                    MetricName = "http.requests_total",
                    Value = Interlocked.Read(ref _requestCount),
                    Timestamp = DateTime.UtcNow
                },
                new()
                {
                    ServiceName = serviceName,
                    MetricName = "http.errors_total",
                    Value = Interlocked.Read(ref _errorCount),
                    Timestamp = DateTime.UtcNow
                }
            };

            await ingestionQueue.QueueAsync(new MetricIngestionRequest
            {
                ServiceName = serviceName,
                Metrics = metrics
            });
        }
    }
}

public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseMetricsCollection(
        this IApplicationBuilder app, string serviceName) =>
        app.UseMiddleware<MetricsCollectionMiddleware>(serviceName);
}
