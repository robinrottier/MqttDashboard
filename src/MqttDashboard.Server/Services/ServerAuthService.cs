using MqttDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IAuthService for server-only deployments.
/// Reads auth status directly from IConfiguration and the current user principal,
/// eliminating the loopback HTTP call that caused "Failed to get auth status" warnings.
/// During SSR pre-render, HttpContext is available and carries the real user identity.
/// During Blazor Server circuits, AuthenticationStateProvider supplies the circuit user.
/// </summary>
public class ServerAuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider? _authStateProvider;
    private readonly ILogger<ServerAuthService>? _logger;

    public ServerAuthService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider? authStateProvider = null,
        ILogger<ServerAuthService>? logger = null)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task<(bool isAdmin, bool authEnabled)> GetStatusAsync()
    {
        var authEnabled = !string.IsNullOrEmpty(_configuration["Auth:AdminPasswordHash"]);
        if (!authEnabled)
            return (true, false);

        // During SSR pre-render: HttpContext is available with the real user identity.
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
            return (httpContext.User.Identity?.IsAuthenticated == true, true);

        // During Blazor Server interactive circuit: HttpContext is null.
        // AuthenticationStateProvider tracks the circuit user identity, initialized
        // from the SSR HTTP request's authenticated principal.
        if (_authStateProvider != null)
        {
            var state = await _authStateProvider.GetAuthenticationStateAsync();
            return (state.User.Identity?.IsAuthenticated == true, true);
        }

        return (false, true);
    }

    public async Task<bool> LoginAsync(string password)
    {
        var hash = _configuration["Auth:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash)) return true;
        if (string.IsNullOrEmpty(password)) return false;

        bool valid;
        try { valid = BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }

        if (!valid) return false;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            // Blazor Server circuit mode: cookies cannot be set without an active HTTP response.
            // Login requires a server-side form POST rather than an interactive component call.
            _logger?.LogWarning("Login cannot set auth cookie: no active HTTP context (Blazor Server circuit). Use a server-side form endpoint for login.");
            return false;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return true;
    }

    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        else
            _logger?.LogWarning("Logout cannot clear auth cookie: no active HTTP context (Blazor Server circuit).");
    }
}
