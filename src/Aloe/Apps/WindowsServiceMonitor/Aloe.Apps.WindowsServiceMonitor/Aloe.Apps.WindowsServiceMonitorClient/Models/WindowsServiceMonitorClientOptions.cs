namespace Aloe.Apps.WindowsServiceMonitorClient.Models;

public class WindowsServiceMonitorClientOptions
{
    public string ServerUrl { get; set; } = "http://localhost:5298";
    public int PollingIntervalSeconds { get; set; } = 30;
    public int TrayIconUpdateIntervalSeconds { get; set; } = 30;
}
