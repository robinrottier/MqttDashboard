using BlazorApp1.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace BlazorApp1.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline for BlazorApp1 with the specified render mode
    /// </summary>
    public static WebApplication UseBlazorApp1<TApp>(
        this WebApplication app,
        BlazorRenderMode renderMode) where TApp : IComponent
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            if (renderMode == BlazorRenderMode.InteractiveWebAssembly || 
                renderMode == BlazorRenderMode.InteractiveAuto)
            {
                app.UseWebAssemblyDebugging();
            }
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        // Optional: Enable status code pages for not found routes
        // app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        app.UseHttpsRedirection();

        // Add antiforgery middleware (required by Blazor components)
        // API controllers are exempt via the IgnoreAntiforgeryTokenAttribute global filter
        app.UseAntiforgery();

        app.MapStaticAssets();

        // Map Controllers
        app.MapControllers();

        // Map SignalR Hub
        app.MapHub<MqttDataHub>("/mqttdatahub");

        // Map Razor Components with appropriate render mode
        var razorComponentsEndpoint = app.MapRazorComponents<TApp>();

        switch (renderMode)
        {
            case BlazorRenderMode.InteractiveServer:
                razorComponentsEndpoint.AddInteractiveServerRenderMode();
                break;
            case BlazorRenderMode.InteractiveWebAssembly:
                razorComponentsEndpoint.AddInteractiveWebAssemblyRenderMode();
                break;
            case BlazorRenderMode.InteractiveAuto:
                razorComponentsEndpoint
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode();
                break;
        }

        // Add additional assemblies
        razorComponentsEndpoint.AddAdditionalAssemblies(typeof(BlazorApp1._Imports).Assembly);

        return app;
    }
}
