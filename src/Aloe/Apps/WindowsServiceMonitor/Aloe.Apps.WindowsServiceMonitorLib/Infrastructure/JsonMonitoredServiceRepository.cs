using System.Text.Json;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

/// <summary>
/// Repository implementation for persisting monitored services to a JSON file.
/// Supports both new (ServiceConfiguration) and legacy (MonitoredServicesData) formats.
/// Provides thread-safe read/write operations with atomic writes.
/// </summary>
public class JsonMonitoredServiceRepository : IMonitoredServiceRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<JsonMonitoredServiceRepository> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonMonitoredServiceRepository(ILogger<JsonMonitoredServiceRepository> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    /// <summary>
    /// Retrieves all monitored services from the JSON file.
    /// Returns empty list if file doesn't exist.
    /// </summary>
    public async Task<List<MonitoredServiceConfig>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await ReadServicesDirectlyAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Reads services directly from file without acquiring semaphore.
    /// For use only within semaphore-locked sections.
    /// </summary>
    private async Task<List<MonitoredServiceConfig>> ReadServicesDirectlyAsync()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Monitored services file not found at {FilePath}. Returning empty list.", _filePath);
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);

            // Try new format first (ServiceConfiguration)
            try
            {
                var config = JsonSerializer.Deserialize<ServiceConfiguration>(json, JsonOptions);
                if (config?.Services != null)
                {
                    return config.Services;
                }
            }
            catch
            {
                // Fall through to legacy format
            }

            // Try legacy format (MonitoredServicesData)
            try
            {
                var data = JsonSerializer.Deserialize<MonitoredServicesData>(json, JsonOptions);
                return data?.Services ?? [];
            }
            catch
            {
                // Fall through
            }

            _logger.LogWarning("Could not deserialize monitored services from {FilePath}", _filePath);
            return [];
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading monitored services file at {FilePath}", _filePath);
            return [];
        }
    }

    /// <summary>
    /// Checks if a service is currently monitored.
    /// </summary>
    public async Task<bool> ExistsAsync(string serviceName)
    {
        var services = await GetAllAsync();
        return services.Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a new service to the monitored services list.
    /// Does nothing if service already exists.
    /// </summary>
    public async Task AddAsync(MonitoredServiceConfig service)
    {
        await _semaphore.WaitAsync();
        try
        {
            var services = await ReadServicesDirectlyAsync();

            // Check if service already exists
            if (services.Any(s => s.ServiceName.Equals(service.ServiceName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Service {ServiceName} already exists in monitored list.", service.ServiceName);
                return;
            }

            services.Add(service);
            await SaveDirectlyAsync(services);
            _logger.LogInformation("Added service {ServiceName} to monitored list.", service.ServiceName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Removes a service from the monitored services list.
    /// </summary>
    public async Task RemoveAsync(string serviceName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var services = await ReadServicesDirectlyAsync();
            var originalCount = services.Count;
            services.RemoveAll(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

            if (services.Count < originalCount)
            {
                await SaveDirectlyAsync(services);
                _logger.LogInformation("Removed service {ServiceName} from monitored list.", serviceName);
            }
            else
            {
                _logger.LogWarning("Service {ServiceName} not found in monitored list for removal.", serviceName);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves all monitored services to the JSON file using atomic write pattern.
    /// Writes to a temporary file first, then renames to ensure data integrity.
    /// </summary>
    public async Task SaveAsync(List<MonitoredServiceConfig> services)
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveDirectlyAsync(services);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves services directly to file without acquiring semaphore.
    /// For use only within semaphore-locked sections.
    /// </summary>
    private async Task SaveDirectlyAsync(List<MonitoredServiceConfig> services)
    {
        try
        {
            // Read current defaults (if any) to preserve them
            ServiceDefaults defaults = new();
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    var config = JsonSerializer.Deserialize<ServiceConfiguration>(json, JsonOptions);
                    if (config?.ServiceDefaults != null)
                    {
                        defaults = config.ServiceDefaults;
                    }
                }
                catch
                {
                    // Use default if can't parse
                }
            }

            var config_new = new ServiceConfiguration
            {
                ServiceDefaults = defaults,
                Services = services
            };

            var json_new = JsonSerializer.Serialize(config_new, JsonOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Atomic write: write to temp file, then rename
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json_new);

            // Rename temp file to actual file (atomic operation on most systems)
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            File.Move(tempPath, _filePath);

            _logger.LogInformation("Saved {ServiceCount} monitored services to {FilePath}", services.Count, _filePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error writing monitored services to file at {FilePath}", _filePath);
            throw;
        }
    }

    /// <summary>
    /// Gets the default service configuration.
    /// </summary>
    public async Task<ServiceDefaults> GetServiceDefaultsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ServiceDefaults();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var config = JsonSerializer.Deserialize<ServiceConfiguration>(json, JsonOptions);
                return config?.ServiceDefaults ?? new ServiceDefaults();
            }
            catch
            {
                _logger.LogWarning("Could not read service defaults from {FilePath}", _filePath);
                return new ServiceDefaults();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves the default service configuration.
    /// </summary>
    public async Task SaveServiceDefaultsAsync(ServiceDefaults defaults)
    {
        await _semaphore.WaitAsync();
        try
        {
            var services = await ReadServicesDirectlyAsync();
            var config = new ServiceConfiguration
            {
                ServiceDefaults = defaults,
                Services = services
            };

            var json = JsonSerializer.Serialize(config, JsonOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Atomic write: write to temp file, then rename
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);

            // Rename temp file to actual file
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            File.Move(tempPath, _filePath);

            _logger.LogInformation("Saved service defaults to {FilePath}", _filePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error writing service defaults to file at {FilePath}", _filePath);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
