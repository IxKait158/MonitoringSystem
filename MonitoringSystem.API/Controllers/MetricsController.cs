using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Metrics;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController(IMetricsService metricsService) : ControllerBase
{
    private ApiKeyEntity CurrentApiKey =>
        (ApiKeyEntity)HttpContext.Items["ApiKeyDTO"]!;

    /// <summary>
    /// Прийом метрик від користувацького сервісу за X-API-KEY.
    /// Сервіс створюється автоматично, якщо ще не зареєстрований під цим ключем.
    /// POST /api/metrics/ingest
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] MetricIngestionRequest request)
    {
        try
        {
            await metricsService.IngestAsync(CurrentApiKey, request);
            return Ok(new { received = request.Metrics.Count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Метрики поточного користувача за часовим діапазоном.
    /// GET /api/metrics?service=MyService&metric=cpu&from=...&to=...
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] string service,
        [FromQuery] string metric,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
            var toDate = to ?? DateTime.UtcNow;

            var metrics = await metricsService.GetMetricsAsync(CurrentApiKey, service, metric, fromDate, toDate);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Поточний статус усіх сервісів цього API ключа.
    /// GET /api/metrics/services
    /// </summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        try
        {
            var statuses = await metricsService.GetServiceStatusesAsync(CurrentApiKey);
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Отримати всі назви метрик поточного користувача
    /// GET /api/metrics/names
    /// </summary>
    [HttpGet("names")]
    public async Task<IActionResult> GetMetricsNames()
    {
        try
        {
            var names = await metricsService.GetMetricsNamesAsync(CurrentApiKey);
            return Ok(names);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
