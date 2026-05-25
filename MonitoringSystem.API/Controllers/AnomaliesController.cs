using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnomaliesController(IMetricsService metricsService) : ControllerBase
{
    private ApiKeyEntity CurrentApiKey =>
        (ApiKeyEntity)HttpContext.Items["ApiKey"]!;

    /// <summary>
    /// Останні аномалії по сервісах поточного API ключа.
    /// GET /api/anomalies?count=20
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAnomalies([FromQuery] int count = 20)
    {
        try
        {
            var anomalies = await metricsService.GetRecentAnomaliesAsync(CurrentApiKey, count);
            return Ok(anomalies);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Порівнює Z-score і SrCnn на історичних даних сервісу поточного ключа.
    /// POST /api/anomalies/compare
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareAlgorithms([FromBody] AnomalyAlgorithmComparisonRequest request)
    {
        try
        {
            var comparison = await metricsService.CompareAnomalyAlgorithmsAsync(CurrentApiKey, request);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Пакетний SrCnn-аналіз для сервісу поточного ключа.
    /// POST /api/anomalies/srcnn-batch
    /// </summary>
    [HttpPost("srcnn-batch")]
    public async Task<IActionResult> SrCnnBatch([FromBody] SrCnnBatchAnalysisRequest request)
    {
        try
        {
            var result = await metricsService.AnalyzeSrCnnBatchAsync(CurrentApiKey, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
