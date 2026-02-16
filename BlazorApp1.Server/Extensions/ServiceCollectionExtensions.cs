using BlazorApp1.Server.Hubs;
using BlazorApp1.Server.Services;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
