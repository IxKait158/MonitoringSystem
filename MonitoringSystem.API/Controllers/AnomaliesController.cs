using Microsoft.AspNetCore.Mvc;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models;

namespace MonitoringSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnomaliesController(IMetricsService metricsService) : ControllerBase
{
    /// <summary>
    /// Returns recent detected anomalies.
    /// GET /api/anomalies?count=20
    /// </summary>
    [HttpGet]
    public IActionResult GetAnomalies([FromQuery] int count = 20)
    {
        var anomalies = metricsService.GetRecentAnomaliesAsync(count);
        return Ok(anomalies);
    }

    /// <summary>
    /// Compares anomaly detection algorithms on historical metric data.
    /// POST /api/anomalies/compare
    /// </summary>
    [HttpPost("compare")]
    public async Task<IActionResult> CompareAlgorithms([FromBody] AnomalyAlgorithmComparisonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
            return BadRequest("ServiceName is required");

        if (string.IsNullOrWhiteSpace(request.MetricName))
            return BadRequest("MetricName is required");

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            return BadRequest("From must be earlier than To");

        var comparison = await metricsService.CompareAnomalyAlgorithmsAsync(request);
        return Ok(comparison);
    }
}
