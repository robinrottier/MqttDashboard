using System.Text.Json;
using BlazorApp1.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace BlazorApp1.Server.Services;

public class DiagramStorageService
{
    private readonly string _storagePath;
    private readonly ILogger<DiagramStorageService> _logger;
    private const string DiagramFileName = "diagram.json";

    public DiagramStorageService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<DiagramStorageService> logger)
    {
        // Priority: environment variable > appsettings.json > default (ContentRoot/Data)
        var envDir = Environment.GetEnvironmentVariable("DIAGRAM_DATA_DIR");
        var configDir = configuration["DiagramStorage:DataDirectory"];
        _storagePath = !string.IsNullOrWhiteSpace(envDir) ? envDir
                     : !string.IsNullOrWhiteSpace(configDir) ? configDir
                     : Path.Combine(environment.ContentRootPath, "Data");

        _logger = logger;

        // Ensure the data directory exists
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
            _logger.LogInformation("Created data directory at {Path}", _storagePath);
        }
    }

    public async Task<DiagramState?> LoadDiagramAsync()
    {
        var filePath = Path.Combine(_storagePath, DiagramFileName);

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

    public async Task<bool> SaveDiagramAsync(DiagramState diagramState)
    {
        var filePath = Path.Combine(_storagePath, DiagramFileName);

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
}
