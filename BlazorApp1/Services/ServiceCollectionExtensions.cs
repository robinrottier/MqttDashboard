using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace BlazorApp1.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorApp1Services(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddScoped<ApplicationState>();
        services.AddScoped<LocalStorageService>();
        services.AddScoped<SignalRService>();

        return services;
    }
}
