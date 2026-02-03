using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorServer.Services;

public class BackgroundWindowsServiceMonitor : BackgroundService
{
    private readonly IHubContext<WindowsServiceMonitorHub> _hubContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BackgroundWindowsServiceMonitor> _logger;
    private readonly WindowsServiceMonitorOptions _options;
    private readonly IOperationTracker _operationTracker;
    private Dictionary<string, ServiceStatus> _previousStatuses = new();

    public BackgroundWindowsServiceMonitor(
        IHubContext<WindowsServiceMonitorHub> hubContext,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BackgroundWindowsServiceMonitor> logger,
        IOptions<WindowsServiceMonitorOptions> options,
        IOperationTracker operationTracker)
    {
        _hubContext = hubContext;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
        _operationTracker = operationTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundWindowsServiceMonitor が開始されました");

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
                _logger.LogError(ex, "BackgroundWindowsServiceMonitor でエラーが発生しました");
            }
        }

        _logger.LogInformation("BackgroundWindowsServiceMonitor が停止されました");
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

                // 操作によるステータス変更があれば、前の状態を更新して誤検知を防ぐ
                var targetStatus = _operationTracker.ConsumeTargetStatus(service.ServiceName);
                if (targetStatus.HasValue)
                {
                    _previousStatuses[service.ServiceName] = targetStatus.Value;
                }

                if (_previousStatuses.TryGetValue(service.ServiceName, out var previousStatus))
                {
                    if (previousStatus != service.Status)
                    {
                        _logger.LogInformation(
                            "サービス '{ServiceName}' の状態が変更されました: {OldStatus} -> {NewStatus}",
                            service.ServiceName, previousStatus, service.Status);

                        // ログ記録（予期しない状態変化のみ）
                        var logRepository = scope.ServiceProvider.GetRequiredService<ILogRepository>();
                        await logRepository.AddLogAsync(new LogEntry
                        {
                            Timestamp = DateTime.Now,
                            Type = LogType.StatusChange,
                            Message = $"サービス '{service.ServiceName}' の状態が変更されました",
                            ServiceName = service.ServiceName,
                            OldStatus = previousStatus.ToString(),
                            NewStatus = service.Status.ToString()
                        });

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
