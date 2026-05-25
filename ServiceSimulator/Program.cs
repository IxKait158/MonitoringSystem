using System.Net.Http.Json;

const string apiUrl = "http://localhost:5169";
const string apiKey = "mk_dev_demo_user_key_0000000000000001";

var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
client.DefaultRequestHeaders.Add("X-API-KEY", apiKey);

var serviceNames = new[] { "OrderService", "PaymentService", "UserService" };
var random = new Random(42);
var iteration = 0;

Console.WriteLine($"Simulator is running. Sending metrics to {apiUrl} under api key {apiKey[..16]}...");

while (true)
{
    iteration++;

    foreach (var serviceName in serviceNames)
    {
        var cpuBase = random.NextDouble() * 30 + 10;
        var memBase = random.NextDouble() * 200 + 100;
        var responseTimeBase = random.NextDouble() * 50 + 20;

        var isAnomaly = iteration % 50 == 0 && serviceName == "PaymentService";
        if (isAnomaly)
        {
            cpuBase = 95 + random.NextDouble() * 5;
            responseTimeBase = 2000 + random.NextDouble() * 1000;
            Console.WriteLine($"Simulate the anomaly for {serviceName}!");
        }

        var request = new
        {
            serviceName,
            metrics = new[]
            {
                new { serviceName, metricName = "system.cpu_percent", value = cpuBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName, metricName = "system.memory_mb", value = memBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName, metricName = "http.response_time_ms", value = responseTimeBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName, metricName = "http.requests_per_second", value = (double)(random.Next(10, 100)), timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() }
            }
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/metrics/ingest", request);
            if (response.IsSuccessStatusCode)
                Console.WriteLine(
                    $"SUCCESS: [{DateTime.UtcNow:HH:mm:ss}] {serviceName}: CPU={cpuBase:F1}%, Mem={memBase:F0}MB, RT={responseTimeBase:F0}ms");
            else
                Console.WriteLine($"ERROR: Error for {serviceName}: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Could not connect to API: {ex.Message}");
        }
    }

    await Task.Delay(2000);
}