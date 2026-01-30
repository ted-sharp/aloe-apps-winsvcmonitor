using Aloe.Apps.ServiceMonitorLib.Models;

namespace Aloe.Apps.ServiceMonitorLib.Interfaces;

public interface IServiceManager
{
    Task<List<ServiceInfo>> GetAllServicesAsync();
    Task<ServiceInfo?> GetServiceAsync(string serviceName);
    Task<ServiceOperationResult> StartServiceAsync(string serviceName);
    Task<ServiceOperationResult> StopServiceAsync(string serviceName);
    Task<ServiceOperationResult> RestartServiceAsync(string serviceName);
    Task<bool> AddToMonitoringAsync(string serviceName, string displayName, string description, bool critical);
    Task<bool> RemoveFromMonitoringAsync(string serviceName);
    Task<List<ServiceInfo>> GetAllInstalledServicesAsync();
    Task<bool> IsServiceMonitoredAsync(string serviceName);
}
