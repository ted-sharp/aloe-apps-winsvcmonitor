namespace Aloe.Apps.ServiceMonitorLib.Models;

public class ServiceInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;
    public string StartupType { get; set; } = "Unknown";
    public int ProcessId { get; set; } = 0;
    public bool IsCritical { get; set; } = false;
}
