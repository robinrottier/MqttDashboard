using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MqttDashboard.Data;

namespace MqttDashboard.Services;

/// <summary>
/// Service to initialize MQTT connection and subscriptions on application startup
/// </summary>
public class MqttInitializationService
{
    private readonly ApplicationState _appState;
    private readonly IDashboardService _dashboardService;
    private readonly IDataServer _dataServer;
    private readonly IMqttDiagnostics _diagnostics;
    private readonly NavigationManager _navigationManager;
    private readonly IAuthService _authService;
    private readonly RenderModeOptions? _renderModeOptions;
    private readonly ILogger<MqttInitializationService>? _logger;
    private bool _initialized = false;

    public MqttInitializationService(
        ApplicationState appState,
        IDashboardService dashboardService,
        IDataServer dataServer,
        IMqttDiagnostics diagnostics,
        NavigationManager navigationManager,
        IAuthService authService,
        RenderModeOptions? renderModeOptions = null,
        ILogger<MqttInitializationService>? logger = null)
    {
        _appState = appState;
        _dashboardService = dashboardService;
        _dataServer = dataServer;
        _diagnostics = diagnostics;
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

            var (isAdmin, authEnabled, readOnly) = await _authService.GetStatusAsync();
            _appState.SetAuthState(isAdmin, authEnabled, readOnly);
            _logger?.LogInformation("Auth state: IsAdmin={IsAdmin}, AuthEnabled={AuthEnabled}, ReadOnly={ReadOnly}", isAdmin, authEnabled, readOnly);

            // Load subscriptions from the default dashboard file
            var defaultDashboard = await _dashboardService.LoadDashboardAsync();
            if (defaultDashboard?.MqttSubscriptions?.Count > 0)
            {
                _appState.SetSubscribedTopics(defaultDashboard.MqttSubscriptions);
            }
            _logger?.LogInformation("Loaded {Count} saved subscriptions", _appState.SubscribedTopics.Count);

            if (_appState.DataServer == null)
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

                // Wire up server events before starting
                _dataServer.StatusChanged += HandleStatusChanged;
                _dataServer.Reconnected += HandleReconnected;
                _dataServer.ValueUpdated += HandleValueUpdated;

                var hubUrl = BuildHubUrl();
                await _dataServer.StartAsync(hubUrl);

                _appState.SetDataServer(_dataServer);
                _appState.DataCache.RegisterServer(_dataServer);

                var serverHost = new Uri(_navigationManager.Uri).Host;
                var mqttBroker = await _diagnostics.GetMqttBrokerInfoAsync();
                _appState.SetMqttConnectionStatus($"Connected to {serverHost} (MQTT: {mqttBroker})", true);

                _logger?.LogInformation("DataServer started successfully");

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
                await _dataServer.SubscribeAsync(topic);
                _logger?.LogDebug("Restored subscription to: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to restore subscription to {Topic}", topic);
            }
        }
    }

    private void HandleValueUpdated(string topic, object value)
    {
        _appState.AddMessage(new Models.MqttDataMessage
        {
            Topic = topic,
            Payload = value?.ToString() ?? string.Empty,
            Timestamp = DateTime.UtcNow
        });
    }

    private void HandleReconnected() => _ = RestoreSubscriptionsAsync();

    private void HandleStatusChanged(string status, bool connected)
        => _appState.SetMqttConnectionStatus(status, connected);

    public bool IsInitialized => _initialized;
}