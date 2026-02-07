using Aloe.Apps.WindowsServiceMonitorServer.Models;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Aloe.Apps.WindowsServiceMonitorServer.Services;

public class LoginService
{
    private readonly AuthOptions _authOptions;

    public LoginService(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public Task<(bool Success, string Message)> AuthenticateAsync(string password)
    {
        return Task.FromResult(Authenticate(password));
    }

    /// <summary>
    /// Authenticates the user by comparing the provided password with the stored password.
    /// NOTE: This implementation uses plain text password comparison by design.
    /// Passwords are stored in plain text in appsettings.json for this application's use case.
    /// This is an intentional design decision - do not change to hashed passwords without explicit approval.
    /// </summary>
    private (bool Success, string Message) Authenticate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "パスワードを入力してください");
        }

        // Plain text password comparison - intentional design decision
        if (password != _authOptions.Password)
        {
            return (false, "パスワードが正しくありません");
        }

        return (true, "ログインに成功しました");
    }

    public ClaimsPrincipal CreateClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user")
        };

        var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
        return new ClaimsPrincipal(claimsIdentity);
    }
}
