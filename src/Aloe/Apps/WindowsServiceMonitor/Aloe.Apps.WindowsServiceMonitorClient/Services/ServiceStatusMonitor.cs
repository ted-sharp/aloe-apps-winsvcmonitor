using System.Net.Http;
using Aloe.Apps.WindowsServiceMonitorClient.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aloe.Apps.WindowsServiceMonitorClient.Services;

public class ServiceStatusMonitor : IAsyncDisposable
{
    private readonly WindowsServiceMonitorClientOptions _options;
    private HubConnection? _hubConnection;
    private bool _hasCriticalServicesDown;

    public event EventHandler<ServiceInfo>? ServiceStatusUpdated;
    public event EventHandler<bool>? CriticalServicesStatusChanged;

    public ServiceStatusMonitor(WindowsServiceMonitorClientOptions options)
    {
        this._options = options;
    }

    public async Task StartAsync()
    {
        this._hubConnection = new HubConnectionBuilder()
            .WithUrl($"{this._options.ServerUrl}/windowsservicemonitorhub", options =>
            {
                options.HttpMessageHandlerFactory = (handler) =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.UseCookies = true;
                        clientHandler.CookieContainer = new System.Net.CookieContainer();
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        this._hubConnection.On<ServiceInfo>("ServiceStatusUpdated", (service) =>
        {
            ServiceStatusUpdated?.Invoke(this, service);
        });

        this._hubConnection.Reconnected += async (connectionId) =>
        {
            try
            {
                await this._hubConnection.InvokeAsync("SubscribeToServiceUpdates");
            }
            catch (Exception)
            {
                // 再接続後のサブスクライブ失敗は無視（ポーリングでカバー）
            }
        };

        try
        {
            await this._hubConnection.StartAsync();
            await this._hubConnection.InvokeAsync("SubscribeToServiceUpdates");
        }
        catch (Exception ex)
        {
            // サーバー接続失敗時は無視（ポーリングでカバー）
            System.Diagnostics.Debug.WriteLine($"SignalR接続エラー: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (this._hubConnection != null)
        {
            try
            {
                await this._hubConnection.InvokeAsync("UnsubscribeFromServiceUpdates");
                await this._hubConnection.StopAsync();
            }
            catch (Exception)
            {
                // 切断エラーは無視
            }
        }
    }

    public void UpdateCriticalServicesStatus(bool hasCriticalDown)
    {
        if (this._hasCriticalServicesDown != hasCriticalDown)
        {
            this._hasCriticalServicesDown = hasCriticalDown;
            CriticalServicesStatusChanged?.Invoke(this, hasCriticalDown);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this.StopAsync();
        if (this._hubConnection != null)
        {
            await this._hubConnection.DisposeAsync();
        }
    }
}
