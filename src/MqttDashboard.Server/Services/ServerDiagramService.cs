using MqttDashboard.Models;
using MqttDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IDiagramService for server-only deployments.
/// Calls DiagramStorageService directly instead of going through HTTP.
/// Admin checks replicate RequireAdminFilter logic using IHttpContextAccessor.
/// </summary>
public class ServerDiagramService : IDiagramService
{
    private readonly DiagramStorageService _storage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerDiagramService>? _logger;

    public ServerDiagramService(
        DiagramStorageService storage,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ServerDiagramService>? logger = null)
    {
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<DiagramState?> LoadDiagramAsync() =>
        _storage.LoadDiagramAsync();

    public Task<List<string>> ListDiagramsAsync() =>
        _storage.ListDiagramNamesAsync();

    public Task<DiagramState?> LoadDiagramByNameAsync(string name) =>
        _storage.LoadDiagramByNameAsync(name);

    public async Task<bool> SaveDiagramAsync(DiagramState diagramState)
    {
        if (!IsAdminAuthorized()) return false;
        return await _storage.SaveDiagramAsync(diagramState);
    }

    public async Task<bool> SaveDiagramByNameAsync(string name, DiagramState diagramState)
    {
        if (!IsAdminAuthorized()) return false;
        return await _storage.SaveDiagramByNameAsync(name, diagramState);
    }

    private bool IsAdminAuthorized()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled) return true;

        var isAuthenticated = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        if (!isAuthenticated)
            _logger?.LogWarning("Save rejected: admin authentication required");
        return isAuthenticated;
    }
}
