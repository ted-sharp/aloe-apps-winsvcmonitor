using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

public interface IServiceRegistrar
{
    Task<ServiceOperationResult> RegisterServiceAsync(ServiceRegistrationRequest request);
    Task<ServiceOperationResult> UnregisterServiceAsync(string serviceName);
}
