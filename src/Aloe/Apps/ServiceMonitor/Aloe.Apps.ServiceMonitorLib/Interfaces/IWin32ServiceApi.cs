namespace Aloe.Apps.ServiceMonitorLib.Interfaces;

public interface IWin32ServiceApi
{
    int? GetProcessId(string serviceName);
}
