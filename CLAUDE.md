# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```bash
# Build the server project
dotnet build "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitorServer/Aloe.Apps.ServiceMonitorServer.csproj"

# Run the server (HTTP: localhost:5298, HTTPS: localhost:7147)
dotnet run --project "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitorServer/Aloe.Apps.ServiceMonitorServer.csproj"

# Build the WPF client
dotnet build "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitor/Aloe.Apps.ServiceMonitorClient/Aloe.Apps.ServiceMonitorClient.csproj"
```

No solution file exists. Build individual projects directly. No test projects exist.

## Architecture

Three-project structure targeting .NET 10.0:

- **ServiceMonitorServer** - Blazor Server web app (interactive server rendering). Hosts Razor pages, SignalR hub, cookie authentication, and background monitoring service.
- **ServiceMonitorLib** - Core library. Windows service management via `System.ServiceProcess.ServiceController` and Win32 P/Invoke (`advapi32.dll`). JSON file persistence for monitored service config.
- **ServiceMonitorClient** - WPF desktop client (.NET 10.0-windows).

### Key Interfaces

| Interface | Implementation | Purpose |
|---|---|---|
| `IServiceManager` | `ServiceManager` | Orchestrates all service operations (start/stop/restart/delete), combines config sources |
| `IServiceRegistrar` | `ServiceRegistrar` | OS-level service registration/unregistration via `sc.exe` |
| `IWin32ServiceApi` | `Win32ServiceApi` | P/Invoke to `advapi32.dll` for PID, uptime, memory usage |
| `IMonitoredServiceRepository` | `JsonMonitoredServiceRepository` | CRUD for `appsettings.services.json` with semaphore-based thread safety |

### Data Flow

`appsettings.services.json` defines which Windows services to monitor. `ServiceManager.GetAllServicesAsync()` merges this JSON list with `ServiceMonitorOptions.MonitoredServices` from `appsettings.json`, then queries each service's live status via `ServiceController` and `Win32ServiceApi`.

### Configuration Files

- `appsettings.json` - Authentication settings (`AuthOptions`), `ServiceMonitor` section (`ServiceMonitorOptions`), logging
- `appsettings.services.json` - Monitored service list with `serviceDefaults` (account/password) and `services[]` array. Each entry: `serviceName`, `displayName`, `description`, `binaryPath`, `critical`

### Authentication

Cookie-based authentication. Login endpoint: `POST /api/login`. Password configured in `appsettings.json` under `Authentication`. Session: 60-minute sliding expiration, HttpOnly, SameSite=Strict.

### Real-time Updates

`BackgroundServiceMonitor` (hosted service) polls service status. `ServiceMonitorHub` (SignalR at `/servicemonitorhub`) pushes updates. Blazor pages also use `PeriodicTimer` (3-second interval) for client-side polling.

## UI

Two versions of the service monitoring page exist:

- `/services` - Uses custom CSS classes defined in `app.css` with shared components (`ServiceCard`, `ServiceControlButtons`, `ConfirmationModal`)
- `/services2` - Pure Pico.css approach using semantic HTML (`<article>`, `<header>`, `<footer>`, `<mark>`, `<details>`, `role="group"`, `aria-busy`)

CSS framework is **Pico.css** (classless/minimal). Custom styles in `wwwroot/app.css` add button color variants (success/danger/warning), badge styles, and layout helpers. Prefer semantic HTML over custom classes.

## Conventions

- UI text and log messages are in Japanese
- Code identifiers and type names are in English
- Button colors follow IEC 60073: green=start, red=stop, yellow=restart
- `#if DEBUG` should not be used (per project preference)
- Service "登録/解除" means OS-level `sc create`/`sc delete`, not JSON list management
