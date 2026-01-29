namespace Aloe.Apps.ServiceMonitorLib.Models;

public class ServiceMonitorOptions
{
    public const string SectionName = "ServiceMonitor";

    public List<MonitoredServiceConfig> MonitoredServices { get; set; } = new();
    public int PollingIntervalSeconds { get; set; } = 5;
    public bool EnableAutoRefresh { get; set; } = true;
    public bool RequireAdminForControl { get; set; } = true;
}
