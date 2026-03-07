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
    private readonly ILogger<MqttInitializationService>? _logger;
    private bool _initialized = false;

    public MqttInitializationService(
        ApplicationState appState,
        ApplicationStateService appStateService,
        SignalRService signalRService,
        NavigationManager navigationManager,
        ILogger<MqttInitializationService>? logger = null)
    {
        _appState = appState;
        _appStateService = appStateService;
        _signalRService = signalRService;
        _navigationManager = navigationManager;
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

            if (_appState.SignalRService == null)
            {
                _signalRService.OnDataReceived += HandleDataReceived;
                _signalRService.OnSubscriptionConfirmed += HandleSubscriptionConfirmed;
                _signalRService.OnUnsubscriptionConfirmed += HandleUnsubscriptionConfirmed;
                _signalRService.OnReconnected += HandleReconnected;
                _signalRService.OnMqttConnectionStatusChanged += HandleMqttConnectionStatusChanged;

                var hubUrl = _navigationManager.ToAbsoluteUri("mqttdatahub");
                await _signalRService.StartAsync(hubUrl.ToString());

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
