namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

public class ServiceRegistrationRequest
{
    public string ServiceName { get; set; } = String.Empty;
    public string DisplayName { get; set; } = String.Empty;
    public string BinaryPath { get; set; } = String.Empty;
    public string? BinaryPathAlt { get; set; }
    public string StartupType { get; set; } = "Manual";
    public string? Description { get; set; }
    public string Account { get; set; } = "LocalSystem";
    public string Password { get; set; } = String.Empty;
}
