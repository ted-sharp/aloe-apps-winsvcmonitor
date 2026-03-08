namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class ServiceInfo
{
    public string ServiceName { get; set; } = String.Empty;
    public string DisplayName { get; set; } = String.Empty;
    public string Description { get; set; } = String.Empty;
    public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;
    public string StartupType { get; set; } = "Unknown";
    public int ProcessId { get; set; } = 0;
    public string BinaryPath { get; set; } = String.Empty;
    public string? BinaryPathAlt { get; set; }
    public bool IsCritical { get; set; } = false;

    /// <summary>
    /// How long the service has been running (if Status == Running).
    /// </summary>
    public TimeSpan Uptime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// When the service status last changed.
    /// </summary>
    public DateTime? LastStatusChange { get; set; }

    /// <summary>
    /// Number of services that depend on this service.
    /// </summary>
    public int DependentServicesCount { get; set; } = 0;

    /// <summary>
    /// Memory usage of the service process in MB.
    /// </summary>
    public double MemoryUsageMB { get; set; } = 0.0;
}
