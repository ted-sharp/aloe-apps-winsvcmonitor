namespace Aloe.Apps.ServiceMonitorServer.Models;

public class AuthOptions
{
    public const string SectionName = "Authentication";

    public string Password { get; set; } = string.Empty;
    public CookieSettings CookieSettings { get; set; } = new();
}

public class CookieSettings
{
    public int ExpireTimeMinutes { get; set; } = 60;
    public bool SlidingExpiration { get; set; } = true;
}
