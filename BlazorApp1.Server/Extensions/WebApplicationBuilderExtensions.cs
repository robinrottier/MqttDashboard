using BlazorApp1.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorApp1.Server.Extensions;

public enum BlazorRenderMode
{
    InteractiveServer,
    InteractiveWebAssembly,
    InteractiveAuto
}

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Adds all services required for BlazorApp1 with the specified render mode
    /// </summary>
    public static WebApplicationBuilder AddBlazorApp1(
        this WebApplicationBuilder builder, 
        BlazorRenderMode renderMode)
    {
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
        builder.Services.AddBlazorApp1Services();
        builder.Services.AddBlazorApp1ServerServices();

        // Add SignalR
        builder.Services.AddSignalR();

        // Add Controllers for API endpoints
        builder.Services.AddControllers(options =>
        {
            // Disable antiforgery validation for API controllers
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        })
        .AddApplicationPart(typeof(BlazorApp1.Server.Controllers.DiagramController).Assembly)
        .AddControllersAsServices();

        return builder;
    }
}
