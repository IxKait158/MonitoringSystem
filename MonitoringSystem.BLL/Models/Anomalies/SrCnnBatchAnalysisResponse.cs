namespace MonitoringSystem.BLL.Models.Anomalies;

public class SrCnnBatchAnalysisResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public double Sensitivity { get; set; }
    public int TotalPoints { get; set; }
    public int AnomalyCount { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public double AverageScore { get; set; }
    public double MaxScore { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<SrCnnBatchPoint> Points { get; set; } = new();
    public List<SrCnnBatchPoint> Anomalies { get; set; } = new();
}

public class SrCnnBatchPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public double AnomalyScore { get; set; }
    public bool IsAnomaly { get; set; }
    public string Severity { get; set; } = "Info";
}
