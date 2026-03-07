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
        .AddApplicationPart(typeof(MqttDashboard.Server.Controllers.DiagramController).Assembly)
        .AddControllersAsServices();

        return builder;
    }
}
