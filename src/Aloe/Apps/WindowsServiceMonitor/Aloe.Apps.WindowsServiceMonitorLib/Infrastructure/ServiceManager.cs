using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

public partial class ServiceManager : IServiceManager
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\- ]+$")]
    private static partial Regex ServiceNameRegex();
    private readonly IWin32ServiceApi _win32Api;
    private readonly ILogger<ServiceManager> _logger;
    private readonly WindowsServiceMonitorOptions _options;
    private readonly IMonitoredServiceRepository _repository;
    private readonly IServiceRegistrar _registrar;
    private readonly ILogRepository _logRepository;
    private readonly IOperationTracker _operationTracker;

    public ServiceManager(
        IWin32ServiceApi win32Api,
        ILogger<ServiceManager> logger,
        IOptions<WindowsServiceMonitorOptions> options,
        IMonitoredServiceRepository repository,
        IServiceRegistrar registrar,
        ILogRepository logRepository,
        IOperationTracker operationTracker)
    {
        _win32Api = win32Api;
        _logger = logger;
        _options = options.Value;
        _repository = repository;
        _registrar = registrar;
        _logRepository = logRepository;
        _operationTracker = operationTracker;
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
        var validationResult = await ValidateMonitoredServiceAsync(serviceName);
        if (validationResult != null) return validationResult;

        return await Task.Run(async () =>
        {
            try
            {
                using var service = new ServiceController(serviceName);

                // 操作前のステータスを取得
                var oldStatus = service.Status;

                if (service.Status == ServiceControllerStatus.Running)
                {
                    return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' は既に実行中です");
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(_options.ServiceOperationTimeoutSeconds));

                _logger.LogInformation("サービス '{ServiceName}' が起動されました", serviceName);

                // 期待される状態遷移を記録（状態変化ログの重複を防ぐため）
                _operationTracker.RegisterExpectedTransition(serviceName, ConvertStatus(oldStatus), ServiceStatus.Running);

                // ログ記録（成功時）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{serviceName}' を起動しました ({ConvertStatusToJapanese(oldStatus)}→実行中)",
                    ServiceName = serviceName,
                    Result = "成功"
                });

                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を起動しました");
            }
            catch (Exception ex)
            {
                return await HandleServiceOperationErrorAsync(serviceName, ex, "起動");
            }
        });
    }

    public async Task<ServiceOperationResult> StopServiceAsync(string serviceName)
    {
        var validationResult = await ValidateMonitoredServiceAsync(serviceName);
        if (validationResult != null) return validationResult;

        return await Task.Run(async () =>
        {
            try
            {
                using var service = new ServiceController(serviceName);

                // 操作前のステータスを取得
                var oldStatus = service.Status;

                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' は既に停止しています");
                }

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(_options.ServiceOperationTimeoutSeconds));

                _logger.LogInformation("サービス '{ServiceName}' が停止されました", serviceName);

                // 期待される状態遷移を記録（状態変化ログの重複を防ぐため）
                _operationTracker.RegisterExpectedTransition(serviceName, ConvertStatus(oldStatus), ServiceStatus.Stopped);

                // ログ記録（成功時）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{serviceName}' を停止しました ({ConvertStatusToJapanese(oldStatus)}→停止)",
                    ServiceName = serviceName,
                    Result = "成功"
                });

                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を停止しました");
            }
            catch (Exception ex)
            {
                return await HandleServiceOperationErrorAsync(serviceName, ex, "停止");
            }
        });
    }

    public async Task<ServiceOperationResult> RestartServiceAsync(string serviceName)
    {
        var validationResult = await ValidateMonitoredServiceAsync(serviceName);
        if (validationResult != null) return validationResult;

        return await Task.Run(async () =>
        {
            try
            {
                using var service = new ServiceController(serviceName);

                // 操作前のステータスを取得
                var oldStatus = service.Status;

                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(_options.ServiceOperationTimeoutSeconds));

                    // 期待される状態遷移を記録: Running → Stopped
                    _operationTracker.RegisterExpectedTransition(serviceName, ConvertStatus(oldStatus), ServiceStatus.Stopped);
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(_options.ServiceOperationTimeoutSeconds));

                _logger.LogInformation("サービス '{ServiceName}' が再起動されました", serviceName);

                // 期待される状態遷移を記録: Stopped → Running
                _operationTracker.RegisterExpectedTransition(serviceName, ServiceStatus.Stopped, ServiceStatus.Running);

                // ログ記録（成功時）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{serviceName}' を再起動しました ({ConvertStatusToJapanese(oldStatus)}→停止→実行中)",
                    ServiceName = serviceName,
                    Result = "成功"
                });

                return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を再起動しました");
            }
            catch (Exception ex)
            {
                return await HandleServiceOperationErrorAsync(serviceName, ex, "再起動");
            }
        });
    }

    /// <summary>
    /// Handles errors that occur during service operations (start/stop/restart).
    /// Logs the error and returns an appropriate failure result.
    /// </summary>
    private async Task<ServiceOperationResult> HandleServiceOperationErrorAsync(
        string serviceName,
        Exception ex,
        string operationName)
    {
        _logger.LogError(ex, "サービス '{ServiceName}' の{OperationName}に失敗しました", serviceName, operationName);

        // Try to get config to retrieve exe output for better error messages
        var config = _options.MonitoredServices.FirstOrDefault(x =>
            x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            var repoServices = await _repository.GetAllAsync();
            config = repoServices.FirstOrDefault(x =>
                x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        string errorMessage = ex.Message;

        // For Win32Exception and InvalidOperationException, try to get exe output
        if (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
        {
            var exeOutput = config != null ? await GetExeConsoleOutputAsync(config) : string.Empty;
            if (!string.IsNullOrEmpty(exeOutput))
            {
                errorMessage = exeOutput;
            }
            else if (ex.InnerException is System.ComponentModel.Win32Exception win32Ex)
            {
                errorMessage = win32Ex.Message;
            }
        }

        // Log the error
        await _logRepository.AddLogAsync(new LogEntry
        {
            Timestamp = DateTime.Now,
            Type = LogType.Operation,
            Message = $"サービス '{serviceName}' の{operationName}に失敗しました: {errorMessage}",
            ServiceName = serviceName,
            Result = "失敗"
        });

        return ServiceOperationResult.FailureResult($"{operationName}に失敗しました: {errorMessage}");
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

            var binaryPath = ServiceRegistrar.ResolveBinaryPath(config.BinaryPath, config.BinaryPathAlt);
            _logger.LogInformation("Resolved binary path: {BinaryPath}", binaryPath);

            if (!File.Exists(binaryPath))
            {
                _logger.LogWarning("Binary file not found: {BinaryPath}", binaryPath);
                return string.Empty;
            }

            return await Task.Run(async () =>
            {
                System.Diagnostics.Process? process = null;
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

                    process = System.Diagnostics.Process.Start(psi);
                    if (process == null)
                    {
                        _logger.LogWarning("Failed to start process for {BinaryPath}", binaryPath);
                        return string.Empty;
                    }

                    // タイムアウトを設定（5秒）
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var token = cts.Token;

                    try
                    {
                        // 出力の読み取りをWaitForExitの前に開始（デッドロック防止）
                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        // プロセスの終了を待機（タイムアウト付き）
                        var waitTask = Task.Run(() => process.WaitForExit(), token);
                        await waitTask;

                        var output = await outputTask;
                        var error = await errorTask;

                        _logger.LogInformation("Process output - Error: '{Error}', Output: '{Output}'", error, output);

                        var result = (!string.IsNullOrEmpty(error) ? error : output).Trim();
                        if (result.Length > 1000)
                            result = result.Substring(0, 1000) + "...";

                        _logger.LogInformation("Final result: '{Result}'", result);
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        // タイムアウト発生時はプロセスを強制終了
                        _logger.LogWarning("Process execution timed out for {BinaryPath}, killing process", binaryPath);
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                process.WaitForExit(1000);
                            }
                        }
                        catch (Exception killEx)
                        {
                            _logger.LogWarning(killEx, "Failed to kill process");
                        }
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing process");
                    return string.Empty;
                }
                finally
                {
                    process?.Dispose();
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExeConsoleOutputAsync");
            return string.Empty;
        }
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

            // サービスの存在を例外なしでチェック
            if (!_win32Api.ServiceExists(serviceName))
            {
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

            try
            {
                using var sc = new ServiceController(serviceName);

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

    private string ConvertStatusToJapanese(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Running => "実行中",
            ServiceStatus.Stopped => "停止",
            ServiceStatus.Paused => "一時停止",
            ServiceStatus.Starting => "起動中",
            ServiceStatus.Stopping => "停止中",
            ServiceStatus.Continuing => "再開中",
            ServiceStatus.Pausing => "一時停止中",
            ServiceStatus.Unknown => "不明",
            _ => status.ToString()
        };
    }

    private string ConvertStatusToJapanese(ServiceControllerStatus status)
    {
        return ConvertStatusToJapanese(ConvertStatus(status));
    }

    private static ServiceOperationResult? ValidateServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return ServiceOperationResult.FailureResult("サービス名は空にできません");
        if (!ServiceNameRegex().IsMatch(serviceName))
            return ServiceOperationResult.FailureResult("無効なサービス名です");
        return null;
    }

    private async Task<ServiceOperationResult?> ValidateMonitoredServiceAsync(string serviceName)
    {
        var nameValidation = ValidateServiceName(serviceName);
        if (nameValidation != null) return nameValidation;
        if (!await IsServiceMonitoredAsync(serviceName))
            return ServiceOperationResult.FailureResult($"サービス '{serviceName}' は監視対象に登録されていません");
        return null;
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
                using var sc = new ServiceController(serviceName);
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
                var repositoryServices = await _repository.GetAllAsync();

                foreach (var sc in allControllers)
                {
                    var config = _options.MonitoredServices.FirstOrDefault(x =>
                        x.ServiceName.Equals(sc.ServiceName, StringComparison.OrdinalIgnoreCase));
                    var repoConfig = repositoryServices.FirstOrDefault(x =>
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
    /// Registers a Windows service using sc create command.
    /// </summary>
    public async Task<ServiceOperationResult> RegisterServiceAsync(ServiceRegistrationRequest request)
    {
        try
        {
            var nameValidation = ValidateServiceName(request.ServiceName);
            if (nameValidation != null) return nameValidation;

            var result = await _registrar.RegisterServiceAsync(request);

            if (result.Success)
            {
                _logger.LogInformation("サービス '{ServiceName}' を登録しました", request.ServiceName);

                // 期待される状態遷移を記録: Unknown → Stopped（登録後は停止状態）
                _operationTracker.RegisterExpectedTransition(request.ServiceName, ServiceStatus.Unknown, ServiceStatus.Stopped);

                // ログ記録（成功時）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{request.ServiceName}' を登録しました (不明→停止)",
                    ServiceName = request.ServiceName,
                    Result = "成功"
                });
            }
            else
            {
                // ログ記録（失敗時）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{request.ServiceName}' の登録に失敗しました: {result.Message}",
                    ServiceName = request.ServiceName,
                    Result = "失敗"
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス '{ServiceName}' の登録に失敗しました", request.ServiceName);

            // ログ記録（例外発生時）
            await _logRepository.AddLogAsync(new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogType.Operation,
                Message = $"サービス '{request.ServiceName}' の登録に失敗しました: {ex.Message}",
                ServiceName = request.ServiceName,
                Result = "失敗"
            });

            return ServiceOperationResult.FailureResult($"登録に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregisters the Windows service only; the service remains in the monitoring list with status Unknown.
    /// </summary>
    public async Task<ServiceOperationResult> DeleteServiceAsync(string serviceName)
    {
        try
        {
            var nameValidation = ValidateServiceName(serviceName);
            if (nameValidation != null) return nameValidation;

            // 操作前のステータスを取得（停止前に）
            ServiceControllerStatus? oldStatus = null;
            bool wasStopped = false;
            try
            {
                using var sc = new ServiceController(serviceName);
                oldStatus = sc.Status;
            }
            catch { }

            // Stop service if running
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(_options.ServiceOperationTimeoutSeconds));
                    _logger.LogInformation("サービス '{ServiceName}' を停止しました", serviceName);
                    wasStopped = true;

                    // 期待される状態遷移を記録: Running → Stopped
                    if (oldStatus.HasValue)
                    {
                        _operationTracker.RegisterExpectedTransition(serviceName, ConvertStatus(oldStatus.Value), ServiceStatus.Stopped);
                    }
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
                // ログ記録（失敗）
                await _logRepository.AddLogAsync(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = LogType.Operation,
                    Message = $"サービス '{serviceName}' の解除に失敗しました: {unregisterResult.Message}",
                    ServiceName = serviceName,
                    Result = "失敗"
                });

                return unregisterResult;
            }

            _logger.LogInformation("サービス '{ServiceName}' を Windows から解除しました", serviceName);

            // 期待される状態遷移を記録: Stopped → Unknown
            _operationTracker.RegisterExpectedTransition(serviceName, ServiceStatus.Stopped, ServiceStatus.Unknown);

            // ログ記録（成功時）- 停止操作を実行した場合のみ「→停止」を含める
            string statusChange = "";
            if (oldStatus.HasValue)
            {
                statusChange = wasStopped
                    ? $" ({ConvertStatusToJapanese(oldStatus.Value)}→停止→不明)"
                    : $" ({ConvertStatusToJapanese(oldStatus.Value)}→不明)";
            }
            await _logRepository.AddLogAsync(new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogType.Operation,
                Message = $"サービス '{serviceName}' を解除しました{statusChange}",
                ServiceName = serviceName,
                Result = "成功"
            });

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
