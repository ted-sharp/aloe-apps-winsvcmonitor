using Aloe.Apps.ServiceMonitorLib.Models;

namespace Aloe.Apps.ServiceMonitorLib.Interfaces;

public interface IServiceRegistrar
{
    Task<ServiceOperationResult> RegisterServiceAsync(ServiceRegistrationRequest request);
    Task<ServiceOperationResult> UnregisterServiceAsync(string serviceName);
}
