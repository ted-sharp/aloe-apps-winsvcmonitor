namespace Aloe.Apps.ServiceMonitorLib.Models;

/// <summary>
/// Root configuration model for the appsettings.services.json file.
/// Combines service defaults and the list of monitored services.
/// </summary>
public class ServiceConfiguration
{
    /// <summary>
    /// Default settings for new service registrations.
    /// </summary>
    public ServiceDefaults ServiceDefaults { get; set; } = new();

    /// <summary>
    /// List of monitored Windows services.
    /// </summary>
    public List<MonitoredServiceConfig> Services { get; set; } = [];
}
