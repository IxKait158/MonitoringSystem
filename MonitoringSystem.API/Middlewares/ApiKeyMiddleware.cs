using MonitoringSystem.BLL.Interfaces.Repositories;

namespace MonitoringSystem.Middlewares;

public class ApiKeyMiddleware(RequestDelegate next)
{
    private const string ApiKeyHeader = "X-API-KEY";

    public async Task InvokeAsync(HttpContext context, IApiKeysRepository apiKeysRepository)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPublic(path, context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var keyValue) ||
            string.IsNullOrWhiteSpace(keyValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API ключ відсутній",
                hint = $"Додайте заголовок: {ApiKeyHeader}: <ваш ключ>"
            });
            return;
        }

        var apiKey = await apiKeysRepository.FirstOrDefaultAsync(
            x => x.Key == keyValue.ToString() && x.IsActive);

        if (apiKey == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Невірний або неактивний API ключ"
            });
            return;
        }

        context.Items["ApiKeyDTO"] = apiKey;

        apiKey.LastUsedAt = DateTime.UtcNow;
        await apiKeysRepository.UpdateAsync(apiKey);

        await next(context);
    }

    private static bool IsPublic(string path, string method)
    {
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        // POST /api/keys — створення нового ключа (без авторизації).
        if (path.Equals("/api/keys", StringComparison.OrdinalIgnoreCase) &&
            method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiKeyMiddleware>();
}
