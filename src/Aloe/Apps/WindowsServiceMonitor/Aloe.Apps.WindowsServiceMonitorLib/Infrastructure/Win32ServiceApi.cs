using System.Diagnostics;
using System.Runtime.InteropServices;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

public class Win32ServiceApi : IWin32ServiceApi
{
    private readonly ILogger<Win32ServiceApi> _logger;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(IntPtr hService, int infoLevel, ref SERVICE_STATUS_PROCESS lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const int SC_STATUS_PROCESS_INFO = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    public Win32ServiceApi(ILogger<Win32ServiceApi> logger)
    {
        _logger = logger;
    }

    public int? GetProcessId(string serviceName)
    {
        IntPtr scManager = IntPtr.Zero;
        IntPtr service = IntPtr.Zero;

        try
        {
            scManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scManager == IntPtr.Zero)
            {
                _logger.LogWarning("SCManagerを開けませんでした");
                return null;
            }

            service = OpenService(scManager, serviceName, SERVICE_QUERY_STATUS);
            if (service == IntPtr.Zero)
            {
                _logger.LogWarning("サービス '{ServiceName}' を開けませんでした", serviceName);
                return null;
            }

            var serviceStatus = new SERVICE_STATUS_PROCESS();
            uint bufferSize = (uint)Marshal.SizeOf(serviceStatus);

            if (!QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, ref serviceStatus, bufferSize, out _))
            {
                _logger.LogWarning("サービス '{ServiceName}' の状態を取得できませんでした", serviceName);
                return null;
            }

            return serviceStatus.dwProcessId == 0 ? null : (int)serviceStatus.dwProcessId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プロセスIDの取得に失敗しました");
            return null;
        }
        finally
        {
            if (service != IntPtr.Zero)
                CloseServiceHandle(service);
            if (scManager != IntPtr.Zero)
                CloseServiceHandle(scManager);
        }
    }

    /// <summary>
    /// Gets the uptime of a service based on its process start time.
    /// Returns TimeSpan.Zero if service is not running or PID cannot be retrieved.
    /// </summary>
    public async Task<TimeSpan> GetServiceUptimeAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var processId = GetProcessId(serviceName);
                if (processId == null)
                {
                    return TimeSpan.Zero;
                }

                var process = Process.GetProcessById(processId.Value);
                var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
                return uptime > TimeSpan.Zero ? uptime : TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サービス '{ServiceName}' の稼働時間取得に失敗しました", serviceName);
                return TimeSpan.Zero;
            }
        });
    }

    /// <summary>
    /// Gets the memory usage of a process in megabytes.
    /// Returns 0 if process cannot be accessed.
    /// </summary>
    public async Task<double> GetProcessMemoryUsageMBAsync(int processId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
                return Math.Round(memoryMB, 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "プロセス {ProcessId} のメモリ使用量取得に失敗しました", processId);
                return 0.0;
            }
        });
    }

    /// <summary>
    /// Gets the count of services that depend on the specified service.
    /// Returns 0 if count cannot be determined.
    /// </summary>
    public async Task<int> GetDependentServicesCountAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var controller = new System.ServiceProcess.ServiceController(serviceName);
                var dependentServices = controller.DependentServices;
                return dependentServices.Length;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サービス '{ServiceName}' の依存サービス数取得に失敗しました", serviceName);
                return 0;
            }
        });
    }

    /// <summary>
    /// Gets the last time a service's status changed.
    /// This is an approximation based on service restart time.
    /// Returns null if information cannot be determined.
    /// </summary>
    public async Task<DateTime?> GetLastStatusChangeAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var processId = GetProcessId(serviceName);
                if (processId == null)
                {
                    // Service is not running, so status changed when it stopped
                    return (DateTime?)null;
                }

                var process = Process.GetProcessById(processId.Value);
                return (DateTime?)process.StartTime;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サービス '{ServiceName}' の状態変更時刻取得に失敗しました", serviceName);
                return (DateTime?)null;
            }
        });
    }

}
