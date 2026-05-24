namespace MonitoringSystem.BLL.Models;

public class AnomalyAlgorithmComparisonResult
{
    public string Algorithm { get; set; } = string.Empty;
    public int TotalAnomalies { get; set; }
    public double AverageScore { get; set; }
    public double MaxScore { get; set; }
    public List<AnomalyResult> Anomalies { get; set; } = new();
}
