using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Anomalies;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnomaliesController(IMetricsService metricsService) : ControllerBase
{
    /// <summary>
    /// Повертає нещодавно виявлені аномалії
    /// GET /api/anomalies?count=20
    /// </summary>
    [HttpGet]
    public IActionResult GetAnomalies([FromQuery] int count = 20)
    {
        try
        {
            var anomalies = metricsService.GetRecentAnomaliesAsync(count);
            return Ok(anomalies);
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
    /// Порівнює алгоритми виявлення аномалій на історичних метричних даних
    /// POST /api/anomalies/compare
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareAlgorithms([FromBody] AnomalyAlgorithmComparisonRequest request)
    {
        try
        {
            var comparison = await metricsService.CompareAnomalyAlgorithmsAsync(request);
            return Ok(comparison);
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
