using System.Diagnostics;
using System.ServiceProcess;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

public class ServiceManager : IServiceManager
{
    private readonly IWin32ServiceApi _win32Api;
    private readonly ILogger<ServiceManager> _logger;
    private readonly WindowsServiceMonitorOptions _options;
    private readonly IMonitoredServiceRepository _repository;
    private readonly IServiceRegistrar _registrar;

    public ServiceManager(
        IWin32ServiceApi win32Api,
        ILogger<ServiceManager> logger,
        IOptions<WindowsServiceMonitorOptions> options,
        IMonitoredServiceRepository repository,
        IServiceRegistrar registrar)
    {
        _win32Api = win32Api;
        _logger = logger;
        _options = options.Value;
        _repository = repository;
        _registrar = registrar;
    }

    public async Task<List<ServiceInfo>> GetAllServicesAsync()
    {
        // Combine services from both appsettings.json and JSON repository
        var configServices = _options.MonitoredServices;
        var repositoryServices = await _repository.GetAllAsync();

        var allServiceNames = configServices
            .Select(s => s.ServiceName)
            .Concat(repositoryServices.Select(s => s.ServiceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var services = new List<ServiceInfo>();

        foreach (var serviceName in allServiceNames)
        {
            var serviceInfo = await GetServiceByNameAsync(serviceName);
            if (serviceInfo != null)
            {
                services.Add(serviceInfo);
            }
        }

        return services;
    }

    public async Task<ServiceInfo?> GetServiceAsync(string serviceName)
    {
        return await GetServiceByNameAsync(serviceName);
    }

    public async Task<ServiceOperationResult> StartServiceAsync(string serviceName)
    {
        // 監視対象のサービスか確認
        if (!await IsServiceMonitoredAsync(serviceName))
        {
            return ServiceOperationResult.FailureResult($"サービス '{serviceName}' は監視対象に登録されていません");
        }

        // サービス名の形式チェック
        if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            return ServiceOperationResult.FailureResult("無効なサービス名です");
        }

        return await Task.Run(async () =>
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

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;
                return ServiceOperationResult.FailureResult($"起動に失敗しました: {errorMessage}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の起動に失敗しました", serviceName);

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;

                // InnerException が Win32Exception の場合、そのメッセージも試す
                if (string.IsNullOrEmpty(exeOutput) && ex.InnerException is System.ComponentModel.Win32Exception win32Ex)
                {
                    errorMessage = win32Ex.Message;
                }

                return ServiceOperationResult.FailureResult($"起動に失敗しました: {errorMessage}");
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
        // 監視対象のサービスか確認
        if (!await IsServiceMonitoredAsync(serviceName))
        {
            return ServiceOperationResult.FailureResult($"サービス '{serviceName}' は監視対象に登録されていません");
        }

        // サービス名の形式チェック
        if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            return ServiceOperationResult.FailureResult("無効なサービス名です");
        }

        return await Task.Run(async () =>
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

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;
                return ServiceOperationResult.FailureResult($"停止に失敗しました: {errorMessage}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の停止に失敗しました", serviceName);

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;

                if (string.IsNullOrEmpty(exeOutput) && ex.InnerException is System.ComponentModel.Win32Exception win32Ex)
                {
                    errorMessage = win32Ex.Message;
                }

                return ServiceOperationResult.FailureResult($"停止に失敗しました: {errorMessage}");
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
        // 監視対象のサービスか確認
        if (!await IsServiceMonitoredAsync(serviceName))
        {
            return ServiceOperationResult.FailureResult($"サービス '{serviceName}' は監視対象に登録されていません");
        }

        // サービス名の形式チェック
        if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            return ServiceOperationResult.FailureResult("無効なサービス名です");
        }

        return await Task.Run(async () =>
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

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;
                return ServiceOperationResult.FailureResult($"再起動に失敗しました: {errorMessage}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の再起動に失敗しました", serviceName);

                var config = _options.MonitoredServices.FirstOrDefault(x =>
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (config == null)
                {
                    var repoServices = await _repository.GetAllAsync();
                    config = repoServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
                var errorMessage = !string.IsNullOrEmpty(exeOutput) ? exeOutput : ex.Message;

                if (string.IsNullOrEmpty(exeOutput) && ex.InnerException is System.ComponentModel.Win32Exception win32Ex)
                {
                    errorMessage = win32Ex.Message;
                }

                return ServiceOperationResult.FailureResult($"再起動に失敗しました: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サービス '{ServiceName}' の操作でエラーが発生しました", serviceName);
                return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
            }
        });
    }

    private async Task<string> GetExeConsoleOutputAsync(MonitoredServiceConfig config)
    {
        try
        {
            if (config == null || string.IsNullOrEmpty(config.BinaryPath))
            {
                _logger.LogWarning("Config is null or BinaryPath is empty");
                return string.Empty;
            }

            var binaryPath = ResolveBinaryPath(config.BinaryPath, config.BinaryPathAlt);
            _logger.LogInformation("Resolved binary path: {BinaryPath}", binaryPath);

            if (!File.Exists(binaryPath))
            {
                _logger.LogWarning("Binary file not found: {BinaryPath}", binaryPath);
                return string.Empty;
            }

            return await Task.Run(async () =>
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = binaryPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(psi))
                    {
                        if (process == null)
                        {
                            _logger.LogWarning("Failed to start process for {BinaryPath}", binaryPath);
                            return string.Empty;
                        }

                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();

                        _logger.LogInformation("Process output - Error: '{Error}', Output: '{Output}'", error, output);

                        process.WaitForExit(5000);

                        var result = (!string.IsNullOrEmpty(error) ? error : output).Trim();
                        if (result.Length > 1000)
                            result = result.Substring(0, 1000) + "...";

                        _logger.LogInformation("Final result: '{Result}'", result);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing process");
                    return string.Empty;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExeConsoleOutputAsync");
            return string.Empty;
        }
    }

    private static string ResolveBinaryPath(string binaryPath, string? binaryPathAlt)
    {
        var primary = ResolveSinglePath(binaryPath);
        if (!string.IsNullOrEmpty(primary) && File.Exists(primary))
            return primary;
        if (!string.IsNullOrWhiteSpace(binaryPathAlt))
        {
            var alt = ResolveSinglePath(binaryPathAlt);
            if (File.Exists(alt))
                return alt;
            if (string.IsNullOrEmpty(primary))
                return alt;
        }
        return primary;
    }

    private static string ResolveSinglePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private async Task<ServiceInfo?> GetServiceByNameAsync(string serviceName)
    {
        return await Task.Run(async () =>
        {
            // 設定は例外前に取得（未登録時も一覧表示するため）
            var configFromSettings = _options.MonitoredServices.FirstOrDefault(x =>
                x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            var configFromRepo = (await _repository.GetAllAsync()).FirstOrDefault(x =>
                x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            var config = configFromSettings ?? configFromRepo;

            try
            {
                var sc = new ServiceController(serviceName);

                var processId = _win32Api.GetProcessId(serviceName) ?? 0;
                var status = ConvertStatus(sc.Status);

                var serviceInfo = new ServiceInfo
                {
                    ServiceName = sc.ServiceName,
                    DisplayName = sc.DisplayName,
                    Description = config?.Description ?? string.Empty,
                    Status = status,
                    StartupType = sc.StartType.ToString(),
                    ProcessId = processId,
                    BinaryPath = config?.BinaryPath ?? string.Empty,
                    BinaryPathAlt = config?.BinaryPathAlt,
                    IsCritical = config?.Critical ?? false
                };

                // Populate new extended properties
                if (status == ServiceStatus.Running && processId > 0)
                {
                    serviceInfo.Uptime = await _win32Api.GetServiceUptimeAsync(serviceName);
                    serviceInfo.MemoryUsageMB = await _win32Api.GetProcessMemoryUsageMBAsync(processId);
                    serviceInfo.LastStatusChange = await _win32Api.GetLastStatusChangeAsync(serviceName);
                }

                serviceInfo.DependentServicesCount = await _win32Api.GetDependentServicesCountAsync(serviceName);

                return serviceInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サービス '{ServiceName}' の情報取得に失敗しました", serviceName);
                // 設定にあれば未登録でも一覧に表示（登録ボタンから sc create 可能）
                if (config != null)
                {
                    return new ServiceInfo
                    {
                        ServiceName = serviceName,
                        DisplayName = config.DisplayName,
                        Description = config.Description,
                        Status = ServiceStatus.Unknown,
                        StartupType = "未登録",
                        ProcessId = 0,
                        BinaryPath = config.BinaryPath ?? string.Empty,
                        BinaryPathAlt = config.BinaryPathAlt,
                        IsCritical = config.Critical
                    };
                }
                return null;
            }
        });
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

    /// <summary>
    /// Adds a service to the monitored services list in the JSON repository.
    /// </summary>
    public async Task<bool> AddToMonitoringAsync(string serviceName, string displayName, string description, bool critical)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("無効なサービス名で追加操作が試みられました");
                return false;
            }

            // Check if service exists on Windows
            try
            {
                var sc = new ServiceController(serviceName);
                // Force evaluation to ensure service exists
                _ = sc.Status;
            }
            catch
            {
                _logger.LogWarning("サービス '{ServiceName}' がWindows上に見つかりません", serviceName);
                return false;
            }

            // Check if already monitored
            var isMonitored = await IsServiceMonitoredAsync(serviceName);
            if (isMonitored)
            {
                _logger.LogInformation("サービス '{ServiceName}' は既に監視されています", serviceName);
                return false;
            }

            var config = new MonitoredServiceConfig
            {
                ServiceName = serviceName,
                DisplayName = displayName,
                Description = description,
                Critical = critical
            };

            await _repository.AddAsync(config);
            _logger.LogInformation("サービス '{ServiceName}' を監視リストに追加しました", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス '{ServiceName}' の監視追加に失敗しました", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Removes a service from the monitored services list in the JSON repository.
    /// </summary>
    public async Task<bool> RemoveFromMonitoringAsync(string serviceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("無効なサービス名で削除操作が試みられました");
                return false;
            }

            var isMonitored = await IsServiceMonitoredAsync(serviceName);
            if (!isMonitored)
            {
                _logger.LogWarning("サービス '{ServiceName}' は監視リストに存在しません", serviceName);
                return false;
            }

            await _repository.RemoveAsync(serviceName);
            _logger.LogInformation("サービス '{ServiceName}' を監視リストから削除しました", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス '{ServiceName}' の監視削除に失敗しました", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Gets all installed Windows services, not just monitored ones.
    /// </summary>
    public async Task<List<ServiceInfo>> GetAllInstalledServicesAsync()
    {
        return await Task.Run(async () =>
        {
            var services = new List<ServiceInfo>();

            try
            {
                var allControllers = ServiceController.GetServices();

                foreach (var sc in allControllers)
                {
                    var config = _options.MonitoredServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(sc.ServiceName, StringComparison.OrdinalIgnoreCase));
                    var repoConfig = (await _repository.GetAllAsync()).FirstOrDefault(x =>
                        x.ServiceName.Equals(sc.ServiceName, StringComparison.OrdinalIgnoreCase));

                    try
                    {
                        var processId = _win32Api.GetProcessId(sc.ServiceName) ?? 0;
                        var status = ConvertStatus(sc.Status);

                        services.Add(new ServiceInfo
                        {
                            ServiceName = sc.ServiceName,
                            DisplayName = sc.DisplayName,
                            Description = config?.Description ?? repoConfig?.Description ?? string.Empty,
                            Status = status,
                            StartupType = sc.StartType.ToString(),
                            ProcessId = processId,
                            IsCritical = config?.Critical ?? repoConfig?.Critical ?? false,
                            DependentServicesCount = await _win32Api.GetDependentServicesCountAsync(sc.ServiceName)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "サービス '{ServiceName}' の情報取得に失敗しました", sc.ServiceName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "インストール済みサービス一覧の取得に失敗しました");
            }

            return services;
        });
    }

    /// <summary>
    /// Checks if a service is currently being monitored.
    /// </summary>
    public async Task<bool> IsServiceMonitoredAsync(string serviceName)
    {
        try
        {
            var inSettings = _options.MonitoredServices.Any(x =>
                x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            var inRepository = await _repository.ExistsAsync(serviceName);
            return inSettings || inRepository;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "サービス '{ServiceName}' の監視状態確認に失敗しました", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Unregisters the Windows service only; the service remains in the monitoring list with status Unknown.
    /// </summary>
    public async Task<ServiceOperationResult> DeleteServiceAsync(string serviceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return ServiceOperationResult.FailureResult("サービス名は空にできません");
            }

            // Validate service name format
            if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
            {
                return ServiceOperationResult.FailureResult("無効なサービス名です");
            }

            // Stop service if running
            try
            {
                var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    _logger.LogInformation("サービス '{ServiceName}' を停止しました", serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サービス '{ServiceName}' の停止に失敗しました（続行）", serviceName);
                // Continue with deletion even if stop fails
            }

            // Unregister the service using sc delete
            var unregisterResult = await _registrar.UnregisterServiceAsync(serviceName);
            if (!unregisterResult.Success)
            {
                return unregisterResult;
            }

            _logger.LogInformation("サービス '{ServiceName}' を Windows から解除しました", serviceName);
            return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を解除しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス '{ServiceName}' の削除に失敗しました", serviceName);
            return ServiceOperationResult.FailureResult($"削除に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the default service configuration settings.
    /// </summary>
    public async Task<ServiceDefaults> GetServiceDefaultsAsync()
    {
        try
        {
            return await _repository.GetServiceDefaultsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービスのデフォルト設定取得に失敗しました");
            return new ServiceDefaults();
        }
    }
}
