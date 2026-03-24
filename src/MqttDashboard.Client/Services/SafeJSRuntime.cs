using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MqttDashboard.Services;

/// <summary>
/// Wraps the framework-registered <see cref="IJSRuntime"/> to silently handle two
/// failure cases that would otherwise crash the Blazor Server circuit:
/// <list type="bullet">
///   <item><see cref="InvalidOperationException"/> — thrown when a component (or a
///   third-party library such as MudBlazor or Blazor.Diagrams) calls JS interop during
///   server-side prerendering or before the circuit is fully interactive.</item>
///   <item><see cref="JSDisconnectedException"/> — thrown when the SignalR connection
///   has already been torn down but an in-flight JS call still tries to respond.</item>
/// </list>
/// Registered as the <see cref="IJSRuntime"/> in the circuit-scoped DI container so
/// ALL components — including third-party libraries — automatically receive the guard
/// with no changes to individual call sites.
/// </summary>
public sealed class SafeJSRuntime : IJSRuntime
{
    private readonly IJSRuntime _inner;
    private readonly ILogger<SafeJSRuntime> _log;

    public SafeJSRuntime(IJSRuntime inner, ILogger<SafeJSRuntime> log)
    {
        _inner = inner;
        _log = log;
    }

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        try
        {
            return await _inner.InvokeAsync<TValue>(identifier, args);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogDebug(ex, "JS interop '{Identifier}' skipped — circuit not ready", identifier);
            return default!;
        }
        catch (JSDisconnectedException)
        {
            return default!;
        }
    }

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        try
        {
            return await _inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogDebug(ex, "JS interop '{Identifier}' skipped — circuit not ready", identifier);
            return default!;
        }
        catch (JSDisconnectedException)
        {
            return default!;
        }
        catch (OperationCanceledException)
        {
            return default!;
        }
    }
}

public static class SafeJSRuntimeExtensions
{
    /// <summary>
    /// Replaces the framework-registered <see cref="IJSRuntime"/> with
    /// <see cref="SafeJSRuntime"/>, which catches <see cref="InvalidOperationException"/>
    /// (circuit not yet interactive / prerender) and <see cref="JSDisconnectedException"/>
    /// silently, preventing circuit crashes caused by premature or post-disconnect JS calls.
    /// <para>
    /// Must be called <b>after</b> <c>AddRazorComponents()</c> /
    /// <c>WebAssemblyHostBuilder.CreateDefault()</c> so the original descriptor already
    /// exists to capture. Applies to all components in the circuit scope, including
    /// MudBlazor and Blazor.Diagrams.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSafeJSRuntime(this IServiceCollection services)
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(IJSRuntime));
        if (original is null) return services;

        services.Remove(original);

        services.Add(new ServiceDescriptor(
            typeof(IJSRuntime),
            sp =>
            {
                // Resolve the original concrete implementation without going through
                // IJSRuntime (which is now us) — avoids circular dependency.
                var inner = original switch
                {
                    { ImplementationFactory: not null } =>
                        (IJSRuntime)original.ImplementationFactory(sp),
                    { ImplementationType: not null } =>
                        (IJSRuntime)ActivatorUtilities.CreateInstance(sp, original.ImplementationType),
                    { ImplementationInstance: not null } =>
                        (IJSRuntime)original.ImplementationInstance,
                    _ => throw new InvalidOperationException(
                        "Unrecognised IJSRuntime service descriptor — cannot wrap with SafeJSRuntime.")
                };
                return new SafeJSRuntime(inner, sp.GetRequiredService<ILogger<SafeJSRuntime>>());
            },
            original.Lifetime));

        return services;
    }
}
