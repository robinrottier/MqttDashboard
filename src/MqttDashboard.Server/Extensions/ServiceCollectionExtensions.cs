using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Services;
using MqttDashboard.Server.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using MqttDashboard.Services;

namespace MqttDashboard.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMqttDashboardServerServices(this IServiceCollection services)
    {
        // Add MQTT Topic Subscription Manager as singleton
        services.AddSingleton<MqttTopicSubscriptionManager>();

        // Add connected client tracker as singleton
        services.AddSingleton<ClientConnectionTracker>();

        // Add MQTT Connection Monitor as singleton
        services.AddSingleton<MqttConnectionMonitor>();

        // Register MqttClientService as both a singleton (injectable) and a hosted service.
        // The singleton registration allows ServerSignalRService to subscribe to its events in-process.
        services.AddSingleton<MqttClientService>();
        services.AddHostedService(sp => sp.GetRequiredService<MqttClientService>());

        // Add Diagram Storage Service
        services.AddSingleton<DiagramStorageService>();

        // Add HttpContextAccessor for DiagramService
        services.AddHttpContextAccessor();

        // Register a scoped HttpClient for use in Blazor components (server-side rendering)
        services.AddScoped<HttpClient>(sp => CreateLoopbackHttpClient(sp));

        // Add SignalR data service for server-side (in-process, no HTTP loopback)
        services.AddScoped<ISignalRService, ServerSignalRService>();

        // Add DiagramService for server-side (in-process, no loopback HTTP)
        services.AddScoped<IDiagramService, ServerDiagramService>();

        // Add ApplicationStateService for server-side (in-process, no loopback HTTP)
        services.AddScoped<IApplicationStateService, ServerApplicationStateService>();

        // Add AuthService for server-side (in-process, no loopback HTTP)
        services.AddScoped<IAuthService, ServerAuthService>();

        // Add RequireAdminFilter as scoped service
        services.AddScoped<RequireAdminFilter>();

        // Update check service
        services.AddHttpClient("UpdateCheck");
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        return services;
    }

    /// <summary>
    /// Creates an HttpClient whose base address points to the local server port.
    /// Using Connection.LocalPort bypasses any reverse proxy so server-side self-calls
    /// always reach the app directly, regardless of public hostname or subpath.
    /// Falls back to the cached port in RenderModeOptions for Blazor Server circuits
    /// where IHttpContextAccessor.HttpContext is null (no active HTTP request).
    /// </summary>
    private static HttpClient CreateLoopbackHttpClient(IServiceProvider sp)
    {
        var ctx = sp.GetService<IHttpContextAccessor>()?.HttpContext;
        var port = ctx?.Connection.LocalPort ?? 0;
        if (port == 0)
            port = sp.GetService<MqttDashboard.Services.RenderModeOptions>()?.LoopbackPort ?? 0;
        return new HttpClient
        {
            BaseAddress = port > 0 ? new Uri($"http://localhost:{port}/") : null
        };
    }
}
