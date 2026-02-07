using Aloe.Apps.WindowsServiceMonitorServer.Components;
using Aloe.Apps.WindowsServiceMonitorServer.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;
using Aloe.Apps.WindowsServiceMonitorServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Serilog;
using Serilog.Formatting.Compact;

// WindowsサービスはSCM起動時に作業ディレクトリがSystem32になるため、
// 実行ファイルのディレクトリに明示的に変更する
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// Serilog設定
var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsDirectory);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logsDirectory, "servicemonitor_.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        encoding: System.Text.Encoding.UTF8));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Authentication設定
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(authOptions.CookieSettings.ExpireTimeMinutes);
        options.SlidingExpiration = authOptions.CookieSettings.SlidingExpiration;
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

// Log Repository
var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
builder.Services.AddSingleton<ILogRepository>(
    sp => new JsonLogRepository(logsDir, sp.GetRequiredService<ILogger<JsonLogRepository>>()));

// Operation Tracker (Singleton to share state between scopes)
builder.Services.AddSingleton<IOperationTracker, OperationTracker>();

builder.Services.AddScoped<IWin32ServiceApi, Win32ServiceApi>();
builder.Services.AddScoped<IServiceManager, ServiceManager>();
builder.Services.AddScoped<IServiceRegistrar, ServiceRegistrar>();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddHostedService<BackgroundWindowsServiceMonitor>();

var app = builder.Build();

// ログディレクトリのパスをコンソールに出力
Console.WriteLine($"ログディレクトリ: {logsDirectory}");

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
app.MapPost("/api/login", async (HttpContext context, LoginService loginService, ILogRepository logRepository, IFormCollection form) =>
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

        // ログイン成功をログに記録
        try
        {
            await logRepository.AddLogAsync(new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogType.Access,
                Message = "ログイン成功",
                UserName = principal.Identity?.Name,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                HttpMethod = "POST",
                RequestPath = "/api/login",
                StatusCode = 302
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ログ記録エラー: {ex.Message}");
        }

        var redirectUrl = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/services";

        return Results.Redirect(redirectUrl);
    }
    else
    {
        // ログイン失敗をログに記録
        try
        {
            await logRepository.AddLogAsync(new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogType.Access,
                Message = "ログイン失敗",
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                HttpMethod = "POST",
                RequestPath = "/api/login",
                StatusCode = 302
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ログ記録エラー: {ex.Message}");
        }

        var errorUrl = $"/login?error={Uri.EscapeDataString(message)}";
        if (!string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            errorUrl += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }
        return Results.Redirect(errorUrl);
    }
});

// Logout endpoint
app.MapPost("/logout", async (HttpContext context, ILogRepository logRepository) =>
{
    var userName = context.User?.Identity?.Name;
    var ipAddress = context.Connection.RemoteIpAddress?.ToString();

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // ログアウトをログに記録
    try
    {
        await logRepository.AddLogAsync(new LogEntry
        {
            Timestamp = DateTime.Now,
            Type = LogType.Access,
            Message = "ログアウト",
            UserName = userName,
            IpAddress = ipAddress,
            HttpMethod = "POST",
            RequestPath = "/logout",
            StatusCode = 302
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ログ記録エラー: {ex.Message}");
    }

    return Results.Redirect("/");
});

app.MapHub<WindowsServiceMonitorHub>("/windowsservicemonitorhub");

app.Run();
