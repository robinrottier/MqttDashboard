using MqttDashboard.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MqttDashboard.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline for MqttDashboard with the specified render mode
    /// </summary>
    public static WebApplication UseMqttDashboard<TApp>(
        this WebApplication app,
        BlazorRenderMode renderMode) where TApp : IComponent
    {
        // Cache the local port on the first request so Blazor Server circuits can build loopback
        // URLs (IHttpContextAccessor.HttpContext is null during circuit rendering because rendering
        // happens in a different async context from the HTTP request handler).
        app.Use(async (ctx, next) =>
        {
            ctx.RequestServices.GetService<MqttDashboard.Services.RenderModeOptions>()
                ?.CacheLoopbackPort(ctx.Connection.LocalPort);
            await next();
        });

        // Apply X-Forwarded-Prefix as the request path base, but only when the header value
        // exactly matches the configured AllowedPathBase (e.g. "/rr-dev").
        // This allows the app to be reached directly (no path base) OR via a reverse proxy
        // sub-path without accepting arbitrary values from untrusted clients.
        var allowedPathBase = app.Configuration["AllowedPathBase"]?.Trim('/');
        if (!string.IsNullOrEmpty(allowedPathBase))
        {
            var canonicalPathBase = new PathString("/" + allowedPathBase);
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var prefix))
                {
                    var prefixValue = "/" + prefix.ToString().Trim('/');
                    if (string.Equals(prefixValue, canonicalPathBase, StringComparison.OrdinalIgnoreCase))
                        context.Request.PathBase = canonicalPathBase;
                }
                return next(context);
            });
        }

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

        // Add authentication/authorization if configured
        if (!string.IsNullOrEmpty(app.Configuration["Auth:AdminPasswordHash"]))
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapStaticAssets();

        // Map Controllers
        app.MapControllers();

        app.MapHealthChecks("/healthz");

        // Map SignalR Hub — disable antiforgery since SignalR manages its own security
        // (WebSocket same-origin policy protects against CSRF; antiforgery tokens don't apply here)
        app.MapHub<MqttDataHub>("/mqttdatahub").DisableAntiforgery();

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
        razorComponentsEndpoint.AddAdditionalAssemblies(typeof(MqttDashboard._Imports).Assembly);

        return app;
    }
}
