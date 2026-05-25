namespace MonitoringSystem.Middlewares;

/// <summary>
/// Раніше збирав метрики самого API в єдиний "MonitoringAPI" сервіс.
/// Тепер метрики приходять виключно від користувацьких сервісів за X-API-KEY,
/// тож цей middleware просто передає запит далі. Залишений для зворотної сумісності pipeline.
/// </summary>
public class MetricsCollectionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context) => next(context);
}

public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseMetricsCollection(this IApplicationBuilder app) =>
        app.UseMiddleware<MetricsCollectionMiddleware>();
}
