using BlazorApp1.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BlazorApp1.Server.Services;

namespace BlazorApp1.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ApplicationStateController : ControllerBase
{
    private readonly string _filePath;
    private readonly ILogger<ApplicationStateController> _logger;

    public ApplicationStateController(DiagramStorageService diagramStorage, ILogger<ApplicationStateController> logger)
    {
        _filePath = Path.Combine(diagramStorage.StoragePath, "applicationstate.json");
        _logger = logger;

        // Ensure Data directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApplicationStateData>> Get()
    {
        try
        {
            if (!System.IO.File.Exists(_filePath))
            {
                _logger.LogInformation("Application state file not found, returning defaults");
                return Ok(new ApplicationStateData());
            }

            var json = await System.IO.File.ReadAllTextAsync(_filePath);
            var state = JsonSerializer.Deserialize<ApplicationStateData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Ok(state ?? new ApplicationStateData());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application state");
            return StatusCode(500, "Error loading application state");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ApplicationStateData state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation("Application state saved successfully");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application state");
            return StatusCode(500, "Error saving application state");
        }
    }
}
