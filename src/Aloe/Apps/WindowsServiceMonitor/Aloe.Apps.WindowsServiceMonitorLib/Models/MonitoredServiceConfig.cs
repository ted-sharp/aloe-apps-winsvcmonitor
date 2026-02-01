namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class MonitoredServiceConfig
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BinaryPath { get; set; } = string.Empty;
    public string? BinaryPathAlt { get; set; }
    public bool Critical { get; set; } = false;
}
