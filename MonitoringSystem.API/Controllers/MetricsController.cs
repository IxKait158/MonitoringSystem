using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(IMetricsService metricsService) : ControllerBase
{
    /// <summary>
    /// Прийом метрик від зовнішніх сервісів (або від middleware)
    /// POST /api/metrics/ingest
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] MetricIngestionRequest request)
    {
        if (string.IsNullOrEmpty(request.ServiceName))
            return BadRequest("ServiceName is required");
        
        await metricsService.IngestAsync(request);
        return Ok(new { received = request.Metrics.Count });
    }

    /// <summary>
    /// Отримати метрики за часовим діапазоном
    /// GET /api/metrics?service=MyService&instance=instance-1&metric=cpu&from=...&to=...
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] string service,
        [FromQuery] string? instance,
        [FromQuery] string metric,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
        var toDate = to ?? DateTime.UtcNow;
        
        var metrics = await metricsService.GetMetricsAsync(service, instance, metric, fromDate, toDate);
        return Ok(metrics);
    }

    /// <summary>
    /// Поточний статус всіх сервісів
    /// GET /api/metrics/services
    /// </summary>
    [HttpGet("services")]
    public IActionResult GetServices()
    {
        var statuses = metricsService.GetServiceStatuses();
        return Ok(statuses);
    }
}
