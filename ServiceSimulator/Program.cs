using System.Net.Http.Json;

var apiUrl = Environment.GetEnvironmentVariable("MONITORING_API_URL") ?? "http://localhost:5169";
var simulatorInstanceId = Environment.GetEnvironmentVariable("SIMULATOR_INSTANCE_ID") ?? Environment.MachineName;
var httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };

var services = new[] { "OrderService", "PaymentService", "UserService" };
var random = new Random(42);
var iteration = 0;

Console.WriteLine($"Simulator is running. Sending metrics to {apiUrl}");

while (true)
{
    iteration++;

    foreach (var service in services)
    {
        var instanceId = $"{service}-{simulatorInstanceId}";

        // Нормальні значення
        var cpuBase = random.NextDouble() * 30 + 10; // 10-40%
        var memBase = random.NextDouble() * 200 + 100; // 100-300 MB
        var responseTimeBase = random.NextDouble() * 50 + 20; // 20-70ms

        // Кожні 50 ітерацій — штучна аномалія для демонстрації
        var isAnomaly = iteration % 50 == 0 && service == "PaymentService";
        if (isAnomaly)
        {
            cpuBase = 95 + random.NextDouble() * 5; // 95-100% CPU
            responseTimeBase = 2000 + random.NextDouble() * 1000; // 2-3 sec
            Console.WriteLine($"Simulate the anomaly for {service}!");
        }

        var request = new
        {
            serviceName = service,
            instanceId,
            metrics = new[]
            {
                new { serviceName = service, instanceId, metricName = "system.cpu_percent", value = cpuBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = service, instanceId, metricName = "system.memory_mb", value = memBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = service, instanceId, metricName = "http.response_time_ms", value = responseTimeBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = service, instanceId, metricName = "http.requests_per_second", value = (double)(random.Next(10, 100)), timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() }
            }
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/metrics/ingest", request);
            if (response.IsSuccessStatusCode)
                Console.WriteLine($"SUCCESS: [{DateTime.Now:HH:mm:ss}] {instanceId}: CPU={cpuBase:F1}%, Mem={memBase:F0}MB, RT={responseTimeBase:F0}ms");
            else
                Console.WriteLine($"ERROR: Error for {service}: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Could not connect to API: {ex.Message}");
        }
    }

    await Task.Delay(2000); // Кожні 2 секунди
}
