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
        this._serviceManager = serviceManager;
        this._logger = logger;
    }

    public async Task SubscribeToServiceUpdates()
    {
        this._logger.LogInformation("クライアント {ConnectionId} がサービス更新を購読しました", this.Context.ConnectionId);
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, "ServiceMonitors");
    }

    public async Task UnsubscribeFromServiceUpdates()
    {
        this._logger.LogInformation("クライアント {ConnectionId} がサービス更新の購読を解除しました", this.Context.ConnectionId);
        await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, "ServiceMonitors");
    }

    public async Task GetServiceStatus(string serviceName)
    {
        try
        {
            var service = await this._serviceManager.GetServiceAsync(serviceName);
            if (service != null)
            {
                await this.Clients.Caller.SendAsync("ServiceStatusUpdated", service);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "サービス状態の取得に失敗しました");
            await this.Clients.Caller.SendAsync("Error", $"エラー: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        this._logger.LogInformation("クライアント {ConnectionId} が接続しました", this.Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        this._logger.LogInformation("クライアント {ConnectionId} が切断されました", this.Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
