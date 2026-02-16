using BlazorApp1.Server.Hubs;
using BlazorApp1.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BlazorApp1.Services;

namespace BlazorApp1.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorApp1ServerServices(this IServiceCollection services)
    {
        // Add MQTT Topic Subscription Manager as singleton
        services.AddSingleton<MqttTopicSubscriptionManager>();

        // Add MQTT Client Service as hosted service
        services.AddHostedService<MqttClientService>();

        // Add Diagram Storage Service
        services.AddSingleton<DiagramStorageService>();

        // Add HttpContextAccessor for DiagramService
        services.AddHttpContextAccessor();

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

        return services;
    }
}
