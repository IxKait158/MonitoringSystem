using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Metrics;

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
        try
        {
            await metricsService.IngestAsync(request);
            return Ok(new
            {
                received = request.Metrics.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
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
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
            var toDate = to ?? DateTime.UtcNow;

            var metrics = await metricsService.GetMetricsAsync(service, instance, metric, fromDate, toDate);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Поточний статус всіх сервісів
    /// GET /api/metrics/services
    /// </summary>
    [HttpGet("services")]
    public IActionResult GetServices()
    {
        try
        {
            var statuses = metricsService.GetServiceStatuses();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
