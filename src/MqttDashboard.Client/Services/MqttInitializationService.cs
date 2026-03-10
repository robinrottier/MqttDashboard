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
    private readonly IApplicationStateService _appStateService;
    private readonly ISignalRService _signalRService;
    private readonly NavigationManager _navigationManager;
    private readonly IAuthService _authService;
    private readonly RenderModeOptions? _renderModeOptions;
    private readonly ILogger<MqttInitializationService>? _logger;
    private bool _initialized = false;

    public MqttInitializationService(
        ApplicationState appState,
        IApplicationStateService appStateService,
        ISignalRService signalRService,
        NavigationManager navigationManager,
        IAuthService authService,
        RenderModeOptions? renderModeOptions = null,
        ILogger<MqttInitializationService>? logger = null)
    {
        _appState = appState;
        _appStateService = appStateService;
        _signalRService = signalRService;
        _navigationManager = navigationManager;
        _authService = authService;
        _renderModeOptions = renderModeOptions;
        _logger = logger;
    }

    /// <summary>
    /// Initializes MQTT. Pass <c>RendererInfo.IsInteractive</c> from the calling component
    /// so the service can reliably distinguish SSR pre-render from interactive circuit/WASM mode.
    /// </summary>
    public async Task InitializeAsync(bool isInteractive = false)
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
                // On the server, !isInteractive means we're in SSR pre-render, not an interactive circuit.
                // RendererInfo.IsInteractive is the reliable signal; HttpContext.IsNull is NOT reliable here.
                if (!OperatingSystem.IsBrowser() && !isInteractive)
                {
                    if (_renderModeOptions?.IsWasmCapable == true)
                    {
                        // SSR pre-render for WASM/Auto mode: WASM will connect SignalR in the browser.
                        _initialized = true;
                        _logger?.LogInformation("MQTT initialization deferred: SignalR will connect in browser");
                        return;
                    }

                    // SSR pre-render for Blazor Server mode: the Blazor Server circuit hasn't been
                    // established yet. Let the circuit reinitialize fully.
                    _logger?.LogInformation("MQTT initialization deferred: SSR pre-render, circuit will initialize");
                    return; // Don't set _initialized = true — circuit scope will re-run this

                    // (fall-through when isInteractive=true: Blazor Server interactive circuit)
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
    /// Builds the SignalR hub URL for WASM clients. In Blazor Server, ServerSignalRService
    /// is used instead and this URL is ignored.
    /// </summary>
    private string BuildHubUrl()
    {
        return _navigationManager.ToAbsoluteUri("mqttdatahub").ToString();
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

