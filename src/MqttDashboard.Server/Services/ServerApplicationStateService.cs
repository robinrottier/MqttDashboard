using MqttDashboard.Models;
using MqttDashboard.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IApplicationStateService for server-only deployments.
/// Bypasses HTTP by reading and writing the state file directly via DiagramStorageService,
/// since the controller and this service share the same process.
/// </summary>
public class ServerApplicationStateService : IApplicationStateService
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private readonly ILogger<ServerApplicationStateService>? _logger;

    public ServerApplicationStateService(DiagramStorageService diagramStorage, ILogger<ServerApplicationStateService>? logger = null)
    {
        _filePath = Path.Combine(diagramStorage.StoragePath, "applicationstate.json");
        _logger = logger;

        var directory = Path.GetDirectoryName(_filePath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task<ApplicationStateData?> LoadStateAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger?.LogInformation("Application state file not found, returning defaults");
                return new ApplicationStateData();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var state = JsonSerializer.Deserialize<ApplicationStateData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger?.LogInformation("Application state loaded successfully");
            return state ?? new ApplicationStateData();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load application state, using defaults");
            return new ApplicationStateData();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> SaveStateAsync(ApplicationStateData state)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
            _logger?.LogInformation("Application state saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save application state");
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
