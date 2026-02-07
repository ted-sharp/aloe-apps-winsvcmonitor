using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorServer.Services;

[Authorize]
public class WindowsServiceMonitorHub : Hub
{
    private readonly IServiceManager _serviceManager;
    private readonly ILogger<WindowsServiceMonitorHub> _logger;

    public WindowsServiceMonitorHub(IServiceManager serviceManager, ILogger<WindowsServiceMonitorHub> logger)
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
