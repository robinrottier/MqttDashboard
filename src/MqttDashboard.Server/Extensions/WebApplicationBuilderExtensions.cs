using MqttDashboard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace MqttDashboard.Server.Extensions;

public enum BlazorRenderMode
{
    InteractiveServer,
    InteractiveWebAssembly,
    InteractiveAuto
}

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Adds all services required for MqttDashboard with the specified render mode
    /// </summary>
    public static WebApplicationBuilder AddMqttDashboard(
        this WebApplicationBuilder builder, 
        BlazorRenderMode renderMode)
    {
        // Persist Data Protection keys so antiforgery tokens survive container restarts.
        // Configure DataProtection:KeysDirectory in appsettings/env (e.g. /app/data/keys in Docker).
        // If not set, keys are stored in the default location (user profile on dev, ephemeral in containers).
        var keysDir = builder.Configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysDir))
        {
            var keysDirInfo = new DirectoryInfo(keysDir);
            keysDirInfo.Create(); // ensure it exists
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(keysDirInfo)
                .SetApplicationName("MqttDashboard");
        }

        // Cookie authentication — always registered so that setting the admin password for the
        // first time (via the Setup page) takes effect without requiring a restart.
        // When no AdminPasswordHash is configured, all users are treated as admin (no login needed),
        // but the auth middleware is present and ready the moment a hash is saved.
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "MqttDashboard.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.Events.OnRedirectToLogin = ctx =>
                {
                    // For API requests, return 401 instead of redirecting to login page
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        ctx.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                    ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();

        // Add Razor Components with appropriate render mode
        var razorComponentsBuilder = builder.Services.AddRazorComponents();

        switch (renderMode)
        {
            case BlazorRenderMode.InteractiveServer:
                razorComponentsBuilder.AddInteractiveServerComponents();
                break;
            case BlazorRenderMode.InteractiveWebAssembly:
                razorComponentsBuilder.AddInteractiveWebAssemblyComponents();
                break;
            case BlazorRenderMode.InteractiveAuto:
                razorComponentsBuilder
                    .AddInteractiveServerComponents()
                    .AddInteractiveWebAssemblyComponents();
                break;
        }

        // Register render mode options so client services can distinguish SSR from Blazor Server circuits
        // and can find the loopback port when IHttpContextAccessor is unavailable.
        builder.Services.AddSingleton(new MqttDashboard.Services.RenderModeOptions
        {
            IsWasmCapable = renderMode is BlazorRenderMode.InteractiveAuto or BlazorRenderMode.InteractiveWebAssembly
        });

        // Add core services
        builder.Services.AddMqttDashboardServices();
        builder.Services.AddMqttDashboardServerServices();

        // Add SignalR
        builder.Services.AddSignalR();

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddCheck<MqttDashboard.Server.Health.MqttConnectionHealthCheck>("mqtt");

        // Add Controllers for API endpoints
        builder.Services.AddControllers(options =>
        {
            // Disable antiforgery validation for API controllers
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        })
        .AddApplicationPart(typeof(MqttDashboard.Server.Controllers.DashboardController).Assembly)
        .AddControllersAsServices();

        return builder;
    }
}
