namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

/// <summary>
/// Data model for JSON serialization/deserialization of monitored services.
/// Represents the root structure of monitored-services.json file.
/// </summary>
public class MonitoredServicesData
{
    /// <summary>
    /// List of services being monitored.
    /// </summary>
    public List<MonitoredServiceConfig> Services { get; set; } = [];
}
