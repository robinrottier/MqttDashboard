using BlazorApp1.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorApp1.Services;

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

            // Set the application state service for persistence
            _appState.SetApplicationStateService(_appStateService);

            // Load saved subscriptions
            await _appState.LoadSubscriptionsAsync();
            _logger?.LogInformation("Loaded {Count} saved subscriptions", _appState.SubscribedTopics.Count);

            // Only initialize SignalR if not already connected
            if (_appState.SignalRService == null)
            {
                // Setup event handlers
                _signalRService.OnDataReceived += HandleDataReceived;
                _signalRService.OnSubscriptionConfirmed += HandleSubscriptionConfirmed;
                _signalRService.OnUnsubscriptionConfirmed += HandleUnsubscriptionConfirmed;

                // Connect to SignalR hub
                var hubUrl = _navigationManager.ToAbsoluteUri("/mqttdatahub");
                await _signalRService.StartAsync(hubUrl.ToString());

                _appState.SetSignalRService(_signalRService);

                var serverHost = new Uri(_navigationManager.Uri).Host;
                var mqttBroker = await _signalRService.GetMqttBrokerInfoAsync();
                _appState.SetMqttConnectionStatus($"Connected to {serverHost} (MQTT: {mqttBroker})", true);

                _logger?.LogInformation("SignalR connected successfully");

                // Restore subscriptions after connection
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
                    _logger?.LogInformation("Restored subscription to: {Topic}", topic);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to restore subscription to {Topic}", topic);
            }
        }
    }

    private void HandleDataReceived(MqttDataMessage message)
    {
        _appState.AddMessage(message);
    }

    private async void HandleSubscriptionConfirmed(string topic)
    {
        await _appState.AddSubscriptionAsync(topic);
        _appState.SetMqttConnectionStatus($"Connected - Subscribed to: {topic}", true);
    }

    private async void HandleUnsubscriptionConfirmed(string topic)
    {
        await _appState.RemoveSubscriptionAsync(topic);
        _appState.SetMqttConnectionStatus($"Connected - Unsubscribed from: {topic}", true);
    }

    public bool IsInitialized => _initialized;
}
