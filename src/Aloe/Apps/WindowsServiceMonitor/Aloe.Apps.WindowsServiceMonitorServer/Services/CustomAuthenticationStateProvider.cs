using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Aloe.Apps.WindowsServiceMonitorServer.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpContext? _httpContext;

    public CustomAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        this._httpContext = httpContextAccessor.HttpContext;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = this._httpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
