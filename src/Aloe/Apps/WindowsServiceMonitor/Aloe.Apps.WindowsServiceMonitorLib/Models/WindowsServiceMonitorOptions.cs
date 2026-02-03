namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class WindowsServiceMonitorOptions
{
    public const string SectionName = "WindowsServiceMonitor";

    public List<MonitoredServiceConfig> MonitoredServices { get; set; } = new();
    public int PollingIntervalSeconds { get; set; } = 5;
    public bool EnableAutoRefresh { get; set; } = true;
    public bool RequireAdminForControl { get; set; } = true;
    /// <summary>
    /// Timeout in seconds for WaitForStatus operations (default: 30 seconds)
    /// </summary>
    public int ServiceOperationTimeoutSeconds { get; set; } = 30;
}
