using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Anomalies;
using MonitoringSystem.BLL.Models.Metrics;

namespace MonitoringSystem.BLL.Services;

/// <summary>
/// Виявлення аномалій:
/// 1. Z-score для online-аналізу.
/// 2. ML.NET SrCnn для пакетного аналізу даних.
/// </summary>
public class AnomalyDetectionService(
    MLContext mlContext,
    ILogger<AnomalyDetectionService> logger) : IAnomalyDetectionService
{
    private readonly ConcurrentDictionary<string, Queue<double>> _metricHistory = new();
    private readonly ConcurrentDictionary<string, object> _historyLocks = new();
    private const int WindowSize = 30;
    private const int MinHistorySize = 5;
    private const double ZScoreThreshold = 2.5;

    public AnomalyResult Analyze(int serviceId, string serviceName, MetricPoint point)
    {
        var key = $"{serviceId}:{point.MetricName}";
        var history = _metricHistory.GetOrAdd(key, _ => new Queue<double>());
        var historyLock = _historyLocks.GetOrAdd(key, _ => new object());

        lock (historyLock)
        {
            var result = new AnomalyResult
            {
                MetricPointId = point.Id,
                ServiceName = serviceName,
                MetricName = point.MetricName,
                Value = point.Value,
                DetectedAt = DateTime.UtcNow
            };

            if (history.Count >= MinHistorySize)
            {
                var values = history.ToArray();
                var mean = values.Average();
                var std = StandardDeviation(values);

                result.ExpectedValue = mean;

                if (std > 0)
                {
                    var zScore = Math.Abs((point.Value - mean) / std);
                    result.AnomalyScore = Math.Min(zScore / ZScoreThreshold, 1.0);
                    result.IsAnomaly = zScore > ZScoreThreshold;

                    if (result.IsAnomaly)
                    {
                        logger.LogInformation(
                            "Виявлено аномалію для {ServiceName}:{MetricName}, value={Value}, Z-Score={ZScore:F2}",
                            serviceName,
                            point.MetricName,
                            result.Value,
                            zScore);
                    }
                }
            }

            history.Enqueue(point.Value);
            if (history.Count > WindowSize)
                history.Dequeue();

            return result;
        }
    }

    public List<AnomalyAlgorithmComparisonResult> CompareAlgorithms(
        string serviceName, string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries)
    {
        var orderedSeries = timeSeries
            .OrderBy(x => x.Timestamp)
            .ToList();

        var results = new List<AnomalyAlgorithmComparisonResult>
        {
            BuildComparisonResult(
                "Z-score",
                AnalyzeBatchWithZScore(serviceName, metricName, orderedSeries))
        };

        var srCnnResults = AnalyzeBatchWithMlNet(serviceName, metricName, orderedSeries);
        if (srCnnResults.Count != 0)
            results.Add(BuildComparisonResult("ML.NET SrCnn", srCnnResults));

        return results;
    }

    private List<AnomalyResult> AnalyzeBatchWithZScore(
        string serviceName, string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries)
    {
        var results = new List<AnomalyResult>();

        for (var i = 0; i < timeSeries.Count; i++)
        {
            var point = timeSeries[i];
            var history = timeSeries
                .Skip(Math.Max(0, i - WindowSize))
                .Take(Math.Min(WindowSize, i))
                .Select(x => x.Value)
                .ToArray();

            if (history.Length < MinHistorySize)
                continue;

            var mean = history.Average();
            var std = StandardDeviation(history);
            if (std <= 0)
                continue;

            var zScore = Math.Abs((point.Value - mean) / std);
            var anomalyScore = Math.Min(zScore / ZScoreThreshold, 1.0);

            results.Add(CreateBatchResult(
                serviceName,
                metricName,
                point,
                mean,
                anomalyScore,
                zScore > ZScoreThreshold));
        }

        return results;
    }
    
    /// <summary>
    /// ML.NET підхід — SrCnn алгоритм для часових рядів.
    /// Використовується для пакетного аналізу історичних даних.
    /// </summary>
    public List<AnomalyResult> AnalyzeBatchWithMlNet(
        string serviceName, string metricName,
        List<(DateTime Timestamp, double Value)> timeSeries,
        double threshold = 0.3)
    {
        if (timeSeries.Count < 12)
            return new List<AnomalyResult>();

        var data = timeSeries.Select(x => new TimeSeriesInput { Value = (float)x.Value }).ToList();
        var dataView = mlContext.Data.LoadFromEnumerable(data);

        // SrCnn — Spectral Residual + CNN для виявлення аномалій
        var pipeline = mlContext.Transforms.DetectAnomalyBySrCnn(
            outputColumnName: "Prediction",
            inputColumnName: nameof(TimeSeriesInput.Value),
            windowSize: Math.Min(11, timeSeries.Count / 2),
            backAddWindowSize: 5,
            lookaheadWindowSize: 5,
            averagingWindowSize: 3,
            judgementWindowSize: Math.Min(21, timeSeries.Count),
            threshold: threshold);

        var model = pipeline.Fit(dataView);
        var predictions = model.Transform(dataView);

        var resultColumn = predictions.GetColumn<double[]>("Prediction").ToList();

        return timeSeries.Select((point, i) => new AnomalyResult
        {
            ServiceName = serviceName,
            MetricName = metricName,
            Value = point.Value,
            AnomalyScore = resultColumn[i][1],
            IsAnomaly = resultColumn[i][0] > 0,
            DetectedAt = point.Timestamp
        }).ToList();
    }

    private static AnomalyAlgorithmComparisonResult BuildComparisonResult(
        string algorithm,
        List<AnomalyResult> results)
    {
        var anomalies = results
            .Where(x => x.IsAnomaly)
            .OrderByDescending(x => x.AnomalyScore)
            .ToList();

        return new AnomalyAlgorithmComparisonResult
        {
            Algorithm = algorithm,
            TotalAnomalies = anomalies.Count,
            AverageScore = results.Count == 0 ? 0 : results.Average(x => x.AnomalyScore),
            MaxScore = results.Count == 0 ? 0 : results.Max(x => x.AnomalyScore),
            Anomalies = anomalies
        };
    }

    private static AnomalyResult CreateBatchResult(
        string serviceName, string metricName,
        (DateTime Timestamp, double Value) point,
        double expectedValue,
        double anomalyScore,
        bool isAnomaly) =>
        new()
        {
            ServiceName = serviceName,
            MetricName = metricName,
            Value = point.Value,
            ExpectedValue = expectedValue,
            AnomalyScore = anomalyScore,
            IsAnomaly = isAnomaly,
            DetectedAt = point.Timestamp
        };

    private static double StandardDeviation(double[] values)
    {
        var mean = values.Average();
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
    
    private class TimeSeriesInput
    {
        public float Value { get; set; }
    }
}
