using System.ServiceProcess;
using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Aloe.Apps.ServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aloe.Apps.ServiceMonitorLib.Infrastructure;

public class ServiceManager : IServiceManager
{
    private readonly IWin32ServiceApi _win32Api;
    private readonly ILogger<ServiceManager> _logger;
    private readonly ServiceMonitorOptions _options;

    public ServiceManager(IWin32ServiceApi win32Api, ILogger<ServiceManager> logger, IOptions<ServiceMonitorOptions> options)
    {
        _win32Api = win32Api;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<ServiceInfo>> GetAllServicesAsync()
    {
        return await Task.Run(() =>
        {
            var services = new List<ServiceInfo>();

            foreach (var config in _options.MonitoredServices)
            {
                var serviceInfo = GetServiceByName(config.ServiceName);
                if (serviceInfo != null)
                {
                    services.Add(serviceInfo);
                }
            }

            return services;
        });
    }

    public async Task<ServiceInfo?> GetServiceAsync(string serviceName)
    {
        ValidateServiceName(serviceName);
        return await Task.Run(() => GetServiceByName(serviceName));
    }

    public async Task<ServiceOperationResult> StartServiceAsync(string serviceName)
    {
        ValidateServiceName(serviceName);
        return await Task.Run(() =>
        {
            try
            {
                var service = new ServiceController(serviceName);

                if (service.Status == ServiceControllerStatus.Running)
                {
                    return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' は既に実行中です");
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                _logger.LogInformation("サービス '{ServiceName}' が起動されました", serviceName);
                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を起動しました");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の起動に失敗しました", serviceName);
                return ServiceOperationResult.FailureResult($"起動に失敗しました: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' が見つかりません", serviceName);
                return ServiceOperationResult.FailureResult($"サービスが見つかりません: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の操作でエラーが発生しました", serviceName);
                return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
            }
        });
    }

    public async Task<ServiceOperationResult> StopServiceAsync(string serviceName)
    {
        ValidateServiceName(serviceName);
        return await Task.Run(() =>
        {
            try
            {
                var service = new ServiceController(serviceName);

                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' は既に停止しています");
                }

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

                _logger.LogInformation("サービス '{ServiceName}' が停止されました", serviceName);
                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を停止しました");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の停止に失敗しました", serviceName);
                return ServiceOperationResult.FailureResult($"停止に失敗しました: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' が見つかりません", serviceName);
                return ServiceOperationResult.FailureResult($"サービスが見つかりません: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の操作でエラーが発生しました", serviceName);
                return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
            }
        });
    }

    public async Task<ServiceOperationResult> RestartServiceAsync(string serviceName)
    {
        ValidateServiceName(serviceName);
        return await Task.Run(() =>
        {
            try
            {
                var service = new ServiceController(serviceName);

                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                _logger.LogInformation("サービス '{ServiceName}' が再起動されました", serviceName);
                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を再起動しました");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の再起動に失敗しました", serviceName);
                return ServiceOperationResult.FailureResult($"再起動に失敗しました: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' が見つかりません", serviceName);
                return ServiceOperationResult.FailureResult($"サービスが見つかりません: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の操作でエラーが発生しました", serviceName);
                return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
            }
        });
    }

    private ServiceInfo? GetServiceByName(string serviceName)
    {
        try
        {
            var sc = new ServiceController(serviceName);
            var config = _options.MonitoredServices.FirstOrDefault(x => x.ServiceName == serviceName);

            return new ServiceInfo
            {
                ServiceName = sc.ServiceName,
                DisplayName = sc.DisplayName,
                Description = config?.Description ?? string.Empty,
                Status = ConvertStatus(sc.Status),
                StartupType = sc.StartType.ToString(),
                ProcessId = _win32Api.GetProcessId(serviceName) ?? 0,
                IsCritical = config?.Critical ?? false
            };
        }
        catch
        {
            return null;
        }
    }

    private ServiceStatus ConvertStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Running => ServiceStatus.Running,
            ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
            ServiceControllerStatus.Paused => ServiceStatus.Paused,
            ServiceControllerStatus.ContinuePending => ServiceStatus.Continuing,
            ServiceControllerStatus.PausePending => ServiceStatus.Pausing,
            ServiceControllerStatus.StartPending => ServiceStatus.Starting,
            ServiceControllerStatus.StopPending => ServiceStatus.Stopping,
            _ => ServiceStatus.Unknown
        };
    }

    private void ValidateServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("サービス名は空にできません", nameof(serviceName));
        }

        if (!_options.MonitoredServices.Any(x => x.ServiceName == serviceName))
        {
            throw new InvalidOperationException($"サービス '{serviceName}' はホワイトリストに登録されていません");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException("無効なサービス名です", nameof(serviceName));
        }
    }
}
