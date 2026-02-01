using Aloe.Apps.WindowsServiceMonitorServer.Components;
using Aloe.Apps.WindowsServiceMonitorServer.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;
using Aloe.Apps.WindowsServiceMonitorServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

// WindowsサービスはSCM起動時に作業ディレクトリがSystem32になるため、
// 実行ファイルのディレクトリに明示的に変更する
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Authentication設定
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "WindowsServiceMonitor.Auth";
    });
builder.Services.AddAuthorizationBuilder();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// WindowsServiceMonitor設定
builder.Services.Configure<WindowsServiceMonitorOptions>(
    builder.Configuration.GetSection(WindowsServiceMonitorOptions.SectionName));

// Monitored Services Repository
var monitoredServicesPath = Path.Combine(AppContext.BaseDirectory, "appsettings.services.json");
builder.Services.AddScoped<IMonitoredServiceRepository>(
    sp => new JsonMonitoredServiceRepository(sp.GetRequiredService<ILogger<JsonMonitoredServiceRepository>>(), monitoredServicesPath));

builder.Services.AddScoped<IWin32ServiceApi, Win32ServiceApi>();
builder.Services.AddScoped<IServiceManager, ServiceManager>();
builder.Services.AddScoped<IServiceRegistrar, ServiceRegistrar>();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddHostedService<BackgroundWindowsServiceMonitor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map API controllers
app.MapControllers();

// Login endpoint
app.MapPost("/api/login", async (HttpContext context, LoginService loginService, IFormCollection form) =>
{
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var (success, message) = await loginService.AuthenticateAsync(password);

    if (success)
    {
        var principal = loginService.CreateClaimsPrincipal();

        // クッキーを永続化するためにIsPersistent = trueを設定
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        var redirectUrl = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/services";

        return Results.Redirect(redirectUrl);
    }
    else
    {
        var errorUrl = $"/login?error={Uri.EscapeDataString(message)}";
        if (!string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            errorUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
        return Results.Redirect(errorUrl);
    }
});

// Logout endpoint
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapHub<WindowsServiceMonitorHub>("/windowsservicemonitorhub");

app.Run();
