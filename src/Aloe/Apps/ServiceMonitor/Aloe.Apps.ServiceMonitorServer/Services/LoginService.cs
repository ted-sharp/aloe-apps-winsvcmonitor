using Aloe.Apps.ServiceMonitorServer.Models;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Aloe.Apps.ServiceMonitorServer.Services;

public class LoginService
{
    private readonly AuthOptions _authOptions;

    public LoginService(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public async Task<(bool Success, string Message)> AuthenticateAsync(string password)
    {
        return await Task.FromResult(Authenticate(password));
    }

    private (bool Success, string Message) Authenticate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "パスワードを入力してください");
        }

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
