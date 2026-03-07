using Microsoft.AspNetCore.Mvc;
using MqttDashboard.Models;
using MqttDashboard.Server.Services;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
public class DiagramController : ControllerBase
{
    private readonly DiagramStorageService _storageService;
    private readonly ILogger<DiagramController> _logger;

    public DiagramController(DiagramStorageService storageService, ILogger<DiagramController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DiagramState>> GetDiagram()
    {
        _logger.LogInformation("[DiagramController] GET diagram requested");
        try
        {
            var diagram = await _storageService.LoadDiagramAsync();

            if (diagram == null)
            {
                _logger.LogWarning("[DiagramController] No diagram found, returning 404");
                return NotFound();
            }

            _logger.LogInformation("[DiagramController] Returning diagram with {NodeCount} nodes", diagram.Nodes.Count);
            return Ok(diagram);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiagramController] Error in GET diagram");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult> SaveDiagram([FromBody] DiagramState diagramState)
    {
        _logger.LogInformation("[DiagramController] POST diagram requested with {NodeCount} nodes", 
            diagramState?.Nodes?.Count ?? 0);

        try
        {
            if (diagramState == null)
            {
                _logger.LogWarning("[DiagramController] DiagramState is null");
                return BadRequest("DiagramState cannot be null");
            }

            _logger.LogInformation("[DiagramController] Saving diagram...");
            var success = await _storageService.SaveDiagramAsync(diagramState);

            if (!success)
            {
                _logger.LogError("[DiagramController] Storage service returned false");
                return StatusCode(500, "Failed to save diagram");
            }

            _logger.LogInformation("[DiagramController] Diagram saved successfully");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiagramController] Error in POST diagram: {Message}", ex.Message);
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<string>>> ListDiagrams()
    {
        var names = await _storageService.ListDiagramNamesAsync();
        return Ok(names);
    }

    [HttpGet("{name}")]
    public async Task<ActionResult<DiagramState>> GetDiagramByName(string name)
    {
        var diagram = await _storageService.LoadDiagramByNameAsync(name);
        if (diagram == null) return NotFound();
        return Ok(diagram);
    }

    [HttpPost("{name}")]
    public async Task<ActionResult> SaveDiagramByName(string name, [FromBody] DiagramState diagramState)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required");
        var success = await _storageService.SaveDiagramByNameAsync(name, diagramState);
        return success ? Ok() : StatusCode(500, "Failed to save");
    }
}
