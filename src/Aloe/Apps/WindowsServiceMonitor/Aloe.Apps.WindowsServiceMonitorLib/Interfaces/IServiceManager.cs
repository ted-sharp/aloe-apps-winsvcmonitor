using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

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
    Task<ServiceOperationResult> DeleteServiceAsync(string serviceName);
    Task<ServiceDefaults> GetServiceDefaultsAsync();
}
