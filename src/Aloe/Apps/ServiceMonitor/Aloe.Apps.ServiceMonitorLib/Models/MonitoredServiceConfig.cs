namespace Aloe.Apps.ServiceMonitorLib.Models;

public class MonitoredServiceConfig
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Critical { get; set; } = false;
}
