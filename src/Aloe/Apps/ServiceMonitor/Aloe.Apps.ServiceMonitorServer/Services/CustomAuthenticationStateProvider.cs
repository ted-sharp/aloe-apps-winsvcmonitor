using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Aloe.Apps.ServiceMonitorServer.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpContext? _httpContext;

    public CustomAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContext = httpContextAccessor.HttpContext;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
