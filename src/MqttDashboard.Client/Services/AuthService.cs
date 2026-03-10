using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(HttpClient httpClient, ILogger<AuthService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool isAdmin, bool authEnabled)> GetStatusAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<AuthStatusResponse>("api/auth/status");
            return (result?.IsAdmin ?? true, result?.AuthEnabled ?? false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get auth status, assuming admin");
            return (true, false);
        }
    }

    public async Task<bool> LoginAsync(string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { Password = password });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Login failed");
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try { await _httpClient.PostAsync("api/auth/logout", null); }
        catch (Exception ex) { _logger?.LogError(ex, "Logout failed"); }
    }

    private record AuthStatusResponse(bool IsAdmin, bool AuthEnabled);
}
