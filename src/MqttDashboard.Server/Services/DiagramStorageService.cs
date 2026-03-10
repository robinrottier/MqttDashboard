using System.Text.Json;
using MqttDashboard.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace MqttDashboard.Server.Services;

public class DiagramStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<DiagramStorageService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const string DiagramFileName = "diagram.json";

    public string StoragePath => _storagePath;

    public DiagramStorageService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<DiagramStorageService> logger)
    {
        // Priority: environment variable > appsettings.json > default (ContentRoot/Data)
        var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
        var configDir = configuration["DiagramStorage:DataDirectory"];
        _storagePath = !string.IsNullOrWhiteSpace(envDir) ? envDir
                     : !string.IsNullOrWhiteSpace(configDir) ? Path.GetFullPath(configDir, environment.ContentRootPath)
                     : Path.Combine(environment.ContentRootPath, "Data");

        _logger = logger;

        // Ensure the data directory exists
        if (Directory.Exists(_storagePath))
        {
            _logger.LogInformation("Using data directory at {Path}", _storagePath);
        }
        else
        {
            Directory.CreateDirectory(_storagePath);
            _logger.LogInformation("Created data directory at {Path}", _storagePath);
        }
    }

    public async Task<DiagramState?> LoadDiagramAsync()
    {
        var filePath = Path.Combine(_storagePath, DiagramFileName);

        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogInformation("No saved diagram found at {Path}", filePath);
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var diagramState = JsonSerializer.Deserialize<DiagramState>(json);
                _logger.LogInformation("Loaded diagram from {Path}", filePath);
                return diagramState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load diagram from {Path}", filePath);
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> SaveDiagramAsync(DiagramState diagramState)
    {
        var filePath = Path.Combine(_storagePath, DiagramFileName);

        await _lock.WaitAsync();
        try
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(diagramState, options);
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation("Saved diagram to {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save diagram to {Path}", filePath);
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> ListDiagramNamesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_storagePath, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();
            return files!;
        }
        finally { _lock.Release(); }
    }

    public async Task<DiagramState?> LoadDiagramByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var filePath = Path.Combine(_storagePath, $"{name}.json");
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<DiagramState>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load diagram '{Name}'", name);
            return null;
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> SaveDiagramByNameAsync(string name, DiagramState diagramState)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) return false;
        var filePath = Path.Combine(_storagePath, $"{safeName}.json");
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(diagramState, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Saved diagram '{Name}' to {Path}", safeName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save diagram '{Name}'", name);
            return false;
        }
        finally { _lock.Release(); }
    }
}
