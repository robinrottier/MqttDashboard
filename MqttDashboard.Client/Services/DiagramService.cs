using System.Net.Http.Json;
using MqttDashboard.Models;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Services;

public class DiagramService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiagramService>? _logger;

    public DiagramService(HttpClient httpClient, ILogger<DiagramService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DiagramState?> LoadDiagramAsync()
    {
        try
        {
            _logger?.LogInformation("Loading diagram from API: {Url}", "api/diagram");
            var result = await _httpClient.GetFromJsonAsync<DiagramState>("api/diagram");
            _logger?.LogInformation("Diagram loaded successfully");
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Diagram doesn't exist yet or request failed. Status: {Status}, Message: {Message}", 
                ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error loading diagram");
            return null;
        }
    }

    public async Task<bool> SaveDiagramAsync(DiagramState diagramState)
    {
        try
        {
            _logger?.LogInformation("Saving diagram to API: {Url} with {NodeCount} nodes", 
                "api/diagram", diagramState.Nodes.Count);

            var response = await _httpClient.PostAsJsonAsync("api/diagram", diagramState);

            _logger?.LogInformation("POST response: Status={Status}, ReasonPhrase={Reason}", 
                (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Save failed. Status: {Status} ({StatusCode}), Reason: {Reason}, Content: {Content}", 
                    response.StatusCode, (int)response.StatusCode, response.ReasonPhrase, content);
                return false;
            }

            _logger?.LogInformation("Diagram saved successfully");
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP request failed. Status: {Status}, Message: {Message}", 
                ex.StatusCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error saving diagram: {Message}", ex.Message);
            return false;
        }
    }
}
