using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Aloe.Apps.ServiceMonitorLib.Models;

namespace Aloe.Apps.ServiceMonitorServer.Services;

public class BackgroundServiceMonitor : BackgroundService
{
    private readonly IHubContext<ServiceMonitorHub> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BackgroundServiceMonitor> _logger;
    private readonly ServiceMonitorOptions _options;
    private Dictionary<string, ServiceStatus> _previousStatuses = new();

    public BackgroundServiceMonitor(
        IHubContext<ServiceMonitorHub> hubContext,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BackgroundServiceMonitor> logger,
        IOptions<ServiceMonitorOptions> options)
    {
        _hubContext = hubContext;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundServiceMonitor が開始されました");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.EnableAutoRefresh)
                {
                    await MonitorServices(stoppingToken);
                }

                await Task.Delay(_options.PollingIntervalSeconds * 1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackgroundServiceMonitor でエラーが発生しました");
            }
        }

        _logger.LogInformation("BackgroundServiceMonitor が停止されました");
    }

    private async Task MonitorServices(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var serviceManager = scope.ServiceProvider.GetRequiredService<IServiceManager>();

            var services = await serviceManager.GetAllServicesAsync();

            foreach (var service in services)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (_previousStatuses.TryGetValue(service.ServiceName, out var previousStatus))
                {
                    if (previousStatus != service.Status)
                    {
                        _logger.LogInformation(
                            "サービス '{ServiceName}' の状態が変更されました: {OldStatus} -> {NewStatus}",
                            service.ServiceName, previousStatus, service.Status);

                        await _hubContext.Clients.Group("ServiceMonitors")
                            .SendAsync("ServiceStatusUpdated", service, cancellationToken: stoppingToken);
                    }
                }

                _previousStatuses[service.ServiceName] = service.Status;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス監視中にエラーが発生しました");
        }
    }
}
