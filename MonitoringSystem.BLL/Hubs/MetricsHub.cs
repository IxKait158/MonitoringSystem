using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MonitoringSystem.BLL.Hubs;

/// <summary>
/// SignalR хаб — клієнти підключаються і отримують метрики в реальному часі.
/// Дашборд оновлюється без перезавантаження сторінки.
/// </summary>
public class MetricsHub(ILogger<MetricsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToService(string serviceName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serviceName);
        logger.LogInformation($"Client ({Context.ConnectionId}) subscribed on service {serviceName}");
    }
}