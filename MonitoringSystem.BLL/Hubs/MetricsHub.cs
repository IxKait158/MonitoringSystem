using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MonitoringSystem.BLL.Interfaces.Repositories;

namespace MonitoringSystem.BLL.Hubs;

/// <summary>
/// SignalR хаб. Клієнт підключається з ?apiKey=...
/// і автоматично потрапляє у групу "apiKey:{id}", куди надсилаються тільки
/// його метрики, аномалії та оновлення статусу сервісів.
/// </summary>
public class MetricsHub(
    IApiKeysRepository apiKeysRepository,
    ILogger<MetricsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var keyValue = httpContext?.Request.Query["apiKey"].ToString();

        if (!string.IsNullOrWhiteSpace(keyValue))
        {
            var apiKey = await apiKeysRepository.FirstOrDefaultAsync(
                x => x.Key == keyValue && x.IsActive);

            if (apiKey != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"apiKey:{apiKey.Id}");
                logger.LogInformation(
                    "Client connected: {ConnectionId} -> apiKey #{ApiKeyId}",
                    Context.ConnectionId, apiKey.Id);
            }
            else
            {
                logger.LogWarning(
                    "Client connected with invalid api key: {ConnectionId}",
                    Context.ConnectionId);
            }
        }
        else
        {
            logger.LogInformation(
                "Client connected without api key: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
