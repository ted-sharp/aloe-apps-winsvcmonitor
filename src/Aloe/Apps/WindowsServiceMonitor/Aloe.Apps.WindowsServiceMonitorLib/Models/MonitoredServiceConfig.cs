namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class MonitoredServiceConfig
{
    public string ServiceName { get; set; } = String.Empty;
    public string DisplayName { get; set; } = String.Empty;
    public string Description { get; set; } = String.Empty;
    public string BinaryPath { get; set; } = String.Empty;
    public string? BinaryPathAlt { get; set; }
    public bool Critical { get; set; } = false;
}
