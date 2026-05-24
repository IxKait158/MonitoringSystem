using MonitoringSystem.BLL.Interfaces.Repositories;

namespace MonitoringSystem.Middlewares;

public class ApiKeyMiddleware(RequestDelegate next)
{
    private const string ApiKeyHeader = "X-API-KEY";

    private static readonly string[] PublicEndpoint =
    {
        "/health",
        "/swagger",
        "/hub/metrics",
        "/api/keys"
    };

    public async Task InvokeAsync(HttpContext context, IApiKeysRepository apiKeysRepository)
    {
        var path = context.Request.Path.Value ?? "";

        if (PublicEndpoint.Any(x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var keyValue))
        {
            context.Response.StatusCode = 402;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API ключ відсутній",
                hint = $"Додайте заголовок: {ApiKeyHeader}: ключ"
            });
            return;
        }
        
        var apiKey = await apiKeysRepository.FirstOrDefaultAsync(x => x.Key == keyValue.ToString() && x.IsActive);
        if (apiKey == null)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Невірний або неактивний API ключ"
            });
            return;
        }
        
        context.Items["ApiKey"] = apiKey;
        context.Items["ServiceName"] = apiKey.ServiceName;
        
        apiKey.LastUsedAt = DateTime.UtcNow;
        await apiKeysRepository.UpdateAsync(apiKey);
        
        await next(context);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app) => 
        app.UseMiddleware<ApiKeyMiddleware>();
}
