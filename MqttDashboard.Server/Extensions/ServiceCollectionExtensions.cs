using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Services;
using MqttDashboard.Server.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

        // Add MQTT Client Service as hosted service
        services.AddHostedService<MqttClientService>();

        // Add Diagram Storage Service
        services.AddSingleton<DiagramStorageService>();

        // Add HttpContextAccessor for DiagramService
        services.AddHttpContextAccessor();

        // Register a scoped HttpClient for use in Blazor components (server-side rendering)
        services.AddScoped<HttpClient>(sp =>
        {
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var httpClient = new HttpClient();
            if (httpContextAccessor?.HttpContext != null)
            {
                var request = httpContextAccessor.HttpContext.Request;
                httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
            }
            return httpClient;
        });

        // Add DiagramService for server-side (calls its own API)
        services.AddScoped<DiagramService>(sp =>
        {
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var httpClient = new HttpClient();

            // Use the current request's base address if available (for server-side)
            if (httpContextAccessor?.HttpContext != null)
            {
                var request = httpContextAccessor.HttpContext.Request;
                httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
            }

            return new DiagramService(httpClient, sp.GetService<ILogger<DiagramService>>());
        });

        // Add ApplicationStateService for server-side (calls its own API)
        services.AddScoped<ApplicationStateService>(sp =>
        {
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var httpClient = new HttpClient();

            // Use the current request's base address if available (for server-side)
            if (httpContextAccessor?.HttpContext != null)
            {
                var request = httpContextAccessor.HttpContext.Request;
                httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
            }

            return new ApplicationStateService(httpClient, sp.GetService<ILogger<ApplicationStateService>>());
        });

        // Add AuthService for server-side (calls its own API)
        services.AddScoped<AuthService>(sp =>
        {
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var httpClient = new HttpClient();
            if (httpContextAccessor?.HttpContext != null)
            {
                var request = httpContextAccessor.HttpContext.Request;
                httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
            }
            return new AuthService(httpClient, sp.GetService<ILogger<AuthService>>());
        });

        // Add RequireAdminFilter as scoped service
        services.AddScoped<RequireAdminFilter>();

        // Update check service
        services.AddHttpClient("UpdateCheck");
        services.AddSingleton<UpdateCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateCheckService>());

        return services;
    }
}
