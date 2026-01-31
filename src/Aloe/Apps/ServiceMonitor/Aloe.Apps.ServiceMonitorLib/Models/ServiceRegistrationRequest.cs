namespace Aloe.Apps.ServiceMonitorLib.Models;

public class ServiceRegistrationRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BinaryPath { get; set; } = string.Empty;
    public string? BinaryPathAlt { get; set; }
    public string StartupType { get; set; } = "Manual";
    public string? Description { get; set; }
    public string Account { get; set; } = "LocalSystem";
    public string Password { get; set; } = string.Empty;
}
