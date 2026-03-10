using MqttDashboard.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Services;

/// <summary>
/// Service to initialize MQTT connection and subscriptions on application startup
/// </summary>
public class MqttInitializationService
{
    private readonly ApplicationState _appState;
    private readonly ApplicationStateService _appStateService;
    private readonly SignalRService _signalRService;
    private readonly NavigationManager _navigationManager;
    private readonly AuthService _authService;
    private readonly RenderModeOptions? _renderModeOptions;
    private readonly IServerContextAccessor? _serverContext;
    private readonly ILogger<MqttInitializationService>? _logger;
    private bool _initialized = false;

    public MqttInitializationService(
        ApplicationState appState,
        ApplicationStateService appStateService,
        SignalRService signalRService,
        NavigationManager navigationManager,
        AuthService authService,
        RenderModeOptions? renderModeOptions = null,
        IServerContextAccessor? serverContext = null,
        ILogger<MqttInitializationService>? logger = null)
    {
        _appState = appState;
        _appStateService = appStateService;
        _signalRService = signalRService;
        _navigationManager = navigationManager;
        _authService = authService;
        _renderModeOptions = renderModeOptions;
        _serverContext = serverContext;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            _logger?.LogInformation("MQTT already initialized, skipping");
            return;
        }

        try
        {
            _logger?.LogInformation("Starting MQTT initialization...");

            _appState.SetApplicationStateService(_appStateService);

            await _appState.LoadSubscriptionsAsync();
            _logger?.LogInformation("Loaded {Count} saved subscriptions", _appState.SubscribedTopics.Count);

            var (isAdmin, authEnabled) = await _authService.GetStatusAsync();
            _appState.SetAuthState(isAdmin, authEnabled);
            _logger?.LogInformation("Auth state: IsAdmin={IsAdmin}, AuthEnabled={AuthEnabled}", isAdmin, authEnabled);

            if (_appState.SignalRService == null)
            {
                if (!OperatingSystem.IsBrowser())
                {
                    if (_renderModeOptions?.IsWasmCapable == true)
                    {
                        // SSR pre-render for WASM/Auto mode: WASM will connect SignalR in the browser.
                        _initialized = true;
                        _logger?.LogInformation("MQTT initialization deferred: SignalR will connect in browser");
                        return;
                    }

                    if (_serverContext?.IsInServerHttpRequest == true)
                    {
                        // SSR pre-render for Blazor Server mode: the Blazor Server circuit hasn't been
                        // established yet. Cache the port now and let the circuit reinitialize fully.
                        _serverContext.CacheLocalPort();
                        _logger?.LogInformation("MQTT initialization deferred: SSR pre-render, circuit will initialize");
                        return; // Don't set _initialized = true — circuit scope will re-run this
                    }

                    // Blazor Server interactive circuit: HttpContext is null (no active HTTP request).
                    // Proceed with server-side SignalR connection using the cached loopback port.
                }

                _signalRService.OnDataReceived += HandleDataReceived;
                _signalRService.OnSubscriptionConfirmed += HandleSubscriptionConfirmed;
                _signalRService.OnUnsubscriptionConfirmed += HandleUnsubscriptionConfirmed;
                _signalRService.OnReconnected += HandleReconnected;
                _signalRService.OnMqttConnectionStatusChanged += HandleMqttConnectionStatusChanged;

                var hubUrl = BuildHubUrl();
                await _signalRService.StartAsync(hubUrl);

                _appState.SetSignalRService(_signalRService);

                var serverHost = new Uri(_navigationManager.Uri).Host;
                var mqttBroker = await _signalRService.GetMqttBrokerInfoAsync();
                _appState.SetMqttConnectionStatus($"Connected to {serverHost} (MQTT: {mqttBroker})", true);

                _logger?.LogInformation("SignalR connected successfully");

                await RestoreSubscriptionsAsync();
            }

            _initialized = true;
            _logger?.LogInformation("MQTT initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during MQTT initialization");
            _appState.SetMqttConnectionStatus($"Error: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Builds the SignalR hub URL. In the browser (WASM) the NavigationManager resolves correctly.
    /// On the server (Blazor Server circuit) we use the loopback port to bypass reverse proxies.
    /// </summary>
    private string BuildHubUrl()
    {
        var externalUri = _navigationManager.ToAbsoluteUri("mqttdatahub");
        if (OperatingSystem.IsBrowser())
            return externalUri.ToString();

        // Blazor Server circuit: build loopback URL using the port cached during SSR pre-render.
        // This avoids routing through any reverse proxy (nginx) from inside the container.
        var port = _renderModeOptions?.LoopbackPort ?? 0;
        if (port > 0)
            return $"http://localhost:{port}{externalUri.AbsolutePath}";

        // Fallback: external URL (works for direct access without a reverse proxy)
        return externalUri.ToString();
    }

    private async Task RestoreSubscriptionsAsync()
    {
        var topics = _appState.SubscribedTopics.ToList();
        _logger?.LogInformation("Restoring {Count} subscriptions", topics.Count);
        foreach (var topic in topics)
        {
            try
            {
                if (_appState.SignalRService != null)
                {
                    await _appState.SignalRService.SubscribeToTopicAsync(topic);
                    _logger?.LogDebug("Restored subscription to: {Topic}", topic);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to restore subscription to {Topic}", topic);
            }
        }
    }

    private void HandleDataReceived(MqttDataMessage message) => _appState.AddMessage(message);

    // Trampoline: event must be void, but we need async work
    private void HandleSubscriptionConfirmed(string topic) => _ = HandleSubscriptionConfirmedAsync(topic);

    private async Task HandleSubscriptionConfirmedAsync(string topic)
    {
        try
        {
            await _appState.AddSubscriptionAsync(topic);
            _appState.SetMqttConnectionStatus($"Connected - Subscribed to: {topic}", true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling subscription confirmation for {Topic}", topic);
        }
    }

    private void HandleUnsubscriptionConfirmed(string topic) => _ = HandleUnsubscriptionConfirmedAsync(topic);

    private async Task HandleUnsubscriptionConfirmedAsync(string topic)
    {
        try
        {
            await _appState.RemoveSubscriptionAsync(topic);
            _appState.SetMqttConnectionStatus($"Connected - Unsubscribed from: {topic}", true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling unsubscription confirmation for {Topic}", topic);
        }
    }

    private void HandleReconnected() => _ = RestoreSubscriptionsAsync();

    private void HandleMqttConnectionStatusChanged(string state, int reconnectAttempts)
    {
        var connected = state == "Connected";
        var status = connected
            ? _appState.MqttConnectionStatus // keep existing connected message
            : reconnectAttempts > 0
                ? $"MQTT reconnecting (attempt {reconnectAttempts})..."
                : $"MQTT {state}";
        _appState.SetMqttConnectionStatus(status, connected);
    }

    public bool IsInitialized => _initialized;
}

