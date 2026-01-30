using Aloe.Apps.ServiceMonitorLib.Models;

namespace Aloe.Apps.ServiceMonitorLib.Interfaces;

/// <summary>
/// Repository interface for persisting monitored services configuration to JSON file.
/// </summary>
public interface IMonitoredServiceRepository
{
    /// <summary>
    /// Retrieves all monitored services from the JSON file.
    /// </summary>
    Task<List<MonitoredServiceConfig>> GetAllAsync();

    /// <summary>
    /// Checks if a service is currently monitored.
    /// </summary>
    Task<bool> ExistsAsync(string serviceName);

    /// <summary>
    /// Adds a new service to the monitored services list.
    /// </summary>
    Task AddAsync(MonitoredServiceConfig service);

    /// <summary>
    /// Removes a service from the monitored services list.
    /// </summary>
    Task RemoveAsync(string serviceName);

    /// <summary>
    /// Saves all monitored services to the JSON file.
    /// </summary>
    Task SaveAsync(List<MonitoredServiceConfig> services);
}
