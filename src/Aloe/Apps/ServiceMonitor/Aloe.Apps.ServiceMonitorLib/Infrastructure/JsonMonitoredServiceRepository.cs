using System.Text.Json;
using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Aloe.Apps.ServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.ServiceMonitorLib.Infrastructure;

/// <summary>
/// Repository implementation for persisting monitored services to a JSON file.
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
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("Monitored services file not found at {FilePath}. Returning empty list.", _filePath);
                return [];
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var data = JsonSerializer.Deserialize<MonitoredServicesData>(json, JsonOptions);
            return data?.Services ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing monitored services JSON file at {FilePath}", _filePath);
            return [];
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading monitored services file at {FilePath}", _filePath);
            return [];
        }
        finally
        {
            _semaphore.Release();
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
            var services = await GetAllAsync();

            // Check if service already exists
            if (services.Any(s => s.ServiceName.Equals(service.ServiceName, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Service {ServiceName} already exists in monitored list.", service.ServiceName);
                return;
            }

            services.Add(service);
            await SaveAsync(services);
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
            var services = await GetAllAsync();
            var originalCount = services.Count;
            services.RemoveAll(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

            if (services.Count < originalCount)
            {
                await SaveAsync(services);
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
            var data = new MonitoredServicesData { Services = services };
            var json = JsonSerializer.Serialize(data, JsonOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Atomic write: write to temp file, then rename
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);

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
        finally
        {
            _semaphore.Release();
        }
    }
}
