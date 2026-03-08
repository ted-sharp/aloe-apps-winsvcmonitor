using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, ServiceStatus> _previousStatuses = new();

    public BackgroundWindowsServiceMonitor(
        IHubContext<WindowsServiceMonitorHub> hubContext,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BackgroundWindowsServiceMonitor> logger,
        IOptions<WindowsServiceMonitorOptions> options,
        IOperationTracker operationTracker)
    {
        this._hubContext = hubContext;
        this._serviceScopeFactory = serviceScopeFactory;
        this._logger = logger;
        this._options = options.Value;
        this._operationTracker = operationTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("BackgroundWindowsServiceMonitor が開始されました");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (this._options.EnableAutoRefresh)
                {
                    await this.MonitorServices(stoppingToken);
                }

                await Task.Delay(this._options.PollingIntervalSeconds * 1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "BackgroundWindowsServiceMonitor でエラーが発生しました");
            }
        }

        this._logger.LogInformation("BackgroundWindowsServiceMonitor が停止されました");
    }

    private async Task MonitorServices(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = this._serviceScopeFactory.CreateScope();
            var serviceManager = scope.ServiceProvider.GetRequiredService<IServiceManager>();

            var services = await serviceManager.GetAllServicesAsync();

            foreach (var service in services)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                // 操作によるステータス変更があれば、前の状態を更新して誤検知を防ぐ
                var targetStatus = this._operationTracker.ConsumeTargetStatus(service.ServiceName);
                if (targetStatus.HasValue)
                {
                    this._previousStatuses[service.ServiceName] = targetStatus.Value;
                }

                if (this._previousStatuses.TryGetValue(service.ServiceName, out var previousStatus))
                {
                    if (previousStatus != service.Status)
                    {
                        this._logger.LogInformation(
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

                        await this._hubContext.Clients.Group("ServiceMonitors")
                            .SendAsync("ServiceStatusUpdated", service, cancellationToken: stoppingToken);
                    }
                }

                this._previousStatuses[service.ServiceName] = service.Status;
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "サービス監視中にエラーが発生しました");
        }
    }
}
