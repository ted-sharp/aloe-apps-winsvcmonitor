namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class ServiceOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ServiceInfo? ServiceInfo { get; set; }

    public static ServiceOperationResult SuccessResult(string message, ServiceInfo? info = null)
    {
        return new ServiceOperationResult
        {
            Success = true,
            Message = message,
            ServiceInfo = info
        };
    }

    public static ServiceOperationResult FailureResult(string message)
    {
        return new ServiceOperationResult
        {
            Success = false,
            Message = message,
            ServiceInfo = null
        };
    }
}
