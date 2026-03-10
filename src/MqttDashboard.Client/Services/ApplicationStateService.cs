using MqttDashboard.Models;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Services;

public class ApplicationStateService : IApplicationStateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplicationStateService>? _logger;

    public ApplicationStateService(HttpClient httpClient, ILogger<ApplicationStateService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApplicationStateData?> LoadStateAsync()
    {
        try
        {
            var state = await _httpClient.GetFromJsonAsync<ApplicationStateData>("api/applicationstate");
            _logger?.LogInformation("Application state loaded successfully");
            return state;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load application state, using defaults");
            return new ApplicationStateData();
        }
    }

    public async Task<bool> SaveStateAsync(ApplicationStateData state)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/applicationstate", state);
            response.EnsureSuccessStatusCode();
            _logger?.LogInformation("Application state saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save application state");
            return false;
        }
    }
}
