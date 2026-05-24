using System.Net.Http.Json;

var apiUrl = Environment.GetEnvironmentVariable("MONITORING_API_URL") ?? "http://localhost:5169";

var clients = new Dictionary<string, HttpClient>
{
    ["OrderService"] = CreateClient(apiUrl, "mk_dev_order_service_key_001"),
    ["PaymentService"] = CreateClient(apiUrl, "mk_dev_payment_service_key_002"),
    ["UserService"] = CreateClient(apiUrl, "mk_dev_user_service_key_003")
};

var random = new Random(42);
var iteration = 0;

Console.WriteLine($"Simulator is running. Sending metrics to {apiUrl}");

while (true)
{
    iteration++;

    foreach (var client in clients)
    {
        // Нормальні значення
        var cpuBase = random.NextDouble() * 30 + 10; // 10-40%
        var memBase = random.NextDouble() * 200 + 100; // 100-300 MB
        var responseTimeBase = random.NextDouble() * 50 + 20; // 20-70ms

        // Кожні 50 ітерацій — штучна аномалія для демонстрації
        var isAnomaly = iteration % 50 == 0 && client.Key == "PaymentService";
        if (isAnomaly)
        {
            cpuBase = 95 + random.NextDouble() * 5; // 95-100% CPU
            responseTimeBase = 2000 + random.NextDouble() * 1000; // 2-3 sec
            Console.WriteLine($"Simulate the anomaly for {client.Key}!");
        }

        var request = new
        {
            serviceName = client.Key,
            metrics = new[]
            {
                new { serviceName = client.Key, metricName = "system.cpu_percent", value = cpuBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = client.Key, metricName = "system.memory_mb", value = memBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = client.Key, metricName = "http.response_time_ms", value = responseTimeBase, timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() },
                new { serviceName = client.Key, metricName = "http.requests_per_second", value = (double)(random.Next(10, 100)), timestamp = DateTime.UtcNow, tags = new Dictionary<string,string>() }
            }
        };

        try
        {
            var response = await client.Value.PostAsJsonAsync("/api/metrics/ingest", request);
            if (response.IsSuccessStatusCode)
                Console.WriteLine($"SUCCESS: [{DateTime.UtcNow:HH:mm:ss}] {client.Key}: CPU={cpuBase:F1}%, Mem={memBase:F0}MB, RT={responseTimeBase:F0}ms");
            else
                Console.WriteLine($"ERROR: Error for {client.Key}: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Could not connect to API: {ex.Message}");
        }
    }

    await Task.Delay(2000); // Кожні 2 секунди
}

static HttpClient CreateClient(string url, string apiKey)
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(url)
    };
    
    client.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
    
    return client;
}