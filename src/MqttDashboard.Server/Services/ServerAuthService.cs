using MqttDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of IAuthService for server-only deployments.
/// Reads auth status directly from IConfiguration and the current user principal,
/// eliminating the loopback HTTP call that caused "Failed to get auth status" warnings.
/// During SSR pre-render, HttpContext is available and carries the real user identity.
/// During Blazor Server circuits, AuthenticationStateProvider is resolved lazily via
/// IServiceProvider so that missing auth configuration does not break DI resolution.
/// </summary>
public class ServerAuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerAuthService>? _logger;

    public ServerAuthService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<ServerAuthService>? logger = null)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
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
        // Try to resolve AuthenticationStateProvider lazily — it may not be available
        // if authentication services were not configured (no Auth:AdminPasswordHash).
        try
        {
            var asp = _serviceProvider.GetService<AuthenticationStateProvider>();
            if (asp != null)
            {
                var state = await asp.GetAuthenticationStateAsync();
                return (state.User.Identity?.IsAuthenticated == true, true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "AuthenticationStateProvider unavailable in circuit; defaulting to unauthenticated");
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
