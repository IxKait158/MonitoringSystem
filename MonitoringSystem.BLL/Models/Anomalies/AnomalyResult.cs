namespace MonitoringSystem.BLL.Models.Anomalies;

public class AnomalyResult
{
    public int MetricPointId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double ExpectedValue { get; set; }
    public double AnomalyScore { get; set; }
    public bool IsAnomaly { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public string Severity => AnomalyScore switch
    {
        > 0.8 => "Critical",
        > 0.5 => "Warning",
        _ => "Info"
    };
}
