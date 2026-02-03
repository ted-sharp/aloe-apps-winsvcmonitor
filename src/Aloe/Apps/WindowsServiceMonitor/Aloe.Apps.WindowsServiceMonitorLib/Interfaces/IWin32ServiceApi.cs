namespace Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

public interface IWin32ServiceApi
{
    bool ServiceExists(string serviceName);
    int? GetProcessId(string serviceName);
    Task<TimeSpan> GetServiceUptimeAsync(string serviceName);
    Task<double> GetProcessMemoryUsageMBAsync(int processId);
    Task<int> GetDependentServicesCountAsync(string serviceName);
    Task<DateTime?> GetLastStatusChangeAsync(string serviceName);
}
