using Microsoft.AspNetCore.SignalR;
using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Aloe.Apps.ServiceMonitorLib.Models;

namespace Aloe.Apps.ServiceMonitorServer.Services;

public class ServiceMonitorHub : Hub
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<ServiceMonitorHub> _logger;

    public ServiceMonitorHub(IServiceManager serviceManager, ILogger<ServiceMonitorHub> logger)
    {
        _serviceManager = serviceManager;
        _logger = logger;
    }

    public async Task SubscribeToServiceUpdates()
    {
        _logger.LogInformation("クライアント {ConnectionId} がサービス更新を購読しました", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "ServiceMonitors");
    }

    public async Task UnsubscribeFromServiceUpdates()
    {
        _logger.LogInformation("クライアント {ConnectionId} がサービス更新の購読を解除しました", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "ServiceMonitors");
    }

    public async Task GetServiceStatus(string serviceName)
    {
        try
        {
            var service = await _serviceManager.GetServiceAsync(serviceName);
            if (service != null)
            {
                await Clients.Caller.SendAsync("ServiceStatusUpdated", service);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス状態の取得に失敗しました");
            await Clients.Caller.SendAsync("Error", $"エラー: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("クライアント {ConnectionId} が接続しました", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("クライアント {ConnectionId} が切断されました", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
