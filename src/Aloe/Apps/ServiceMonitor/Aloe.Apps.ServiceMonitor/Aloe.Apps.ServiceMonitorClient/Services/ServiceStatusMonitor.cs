using System.Net.Http;
using Aloe.Apps.ServiceMonitorClient.Models;
using Aloe.Apps.ServiceMonitorLib.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aloe.Apps.ServiceMonitorClient.Services;

public class ServiceStatusMonitor : IAsyncDisposable
{
    private readonly ServiceMonitorClientOptions _options;
    private HubConnection? _hubConnection;
    private bool _hasCriticalServicesDown;

    public event EventHandler<ServiceInfo>? ServiceStatusUpdated;
    public event EventHandler<bool>? CriticalServicesStatusChanged;

    public ServiceStatusMonitor(ServiceMonitorClientOptions options)
    {
        _options = options;
    }

    public async Task StartAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_options.ServerUrl}/servicemonitorhub", options =>
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

        _hubConnection.On<ServiceInfo>("ServiceStatusUpdated", (service) =>
        {
            ServiceStatusUpdated?.Invoke(this, service);
        });

        _hubConnection.Reconnected += async (connectionId) =>
        {
            try
            {
                await _hubConnection.InvokeAsync("SubscribeToServiceUpdates");
            }
            catch (Exception)
            {
                // 再接続後のサブスクライブ失敗は無視（ポーリングでカバー）
            }
        };

        try
        {
            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("SubscribeToServiceUpdates");
        }
        catch (Exception)
        {
            // サーバー接続失敗時は無視（ポーリングでカバー）
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("UnsubscribeFromServiceUpdates");
                await _hubConnection.StopAsync();
            }
            catch (Exception)
            {
                // 切断エラーは無視
            }
        }
    }

    public void UpdateCriticalServicesStatus(bool hasCriticalDown)
    {
        if (_hasCriticalServicesDown != hasCriticalDown)
        {
            _hasCriticalServicesDown = hasCriticalDown;
            CriticalServicesStatusChanged?.Invoke(this, hasCriticalDown);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
