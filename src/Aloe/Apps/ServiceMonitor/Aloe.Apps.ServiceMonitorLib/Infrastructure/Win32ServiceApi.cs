using System.Runtime.InteropServices;
using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.ServiceMonitorLib.Infrastructure;

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

}
