namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

/// <summary>
/// Represents default service configuration settings.
/// </summary>
public class ServiceDefaults
{
    /// <summary>
    /// Default account name for service execution (e.g., "LocalSystem", "NT AUTHORITY\\NetworkService").
    /// </summary>
    public string Account { get; set; } = "LocalSystem";

    /// <summary>
    /// Default password for the service account. Empty for built-in accounts.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
