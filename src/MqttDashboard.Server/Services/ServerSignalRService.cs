using MqttDashboard.Models;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Services;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process implementation of <see cref="ISignalRService"/> for Blazor Server circuits.
/// Bypasses HTTP/WebSocket entirely: subscribes directly to <see cref="MqttClientService.OnMessagePublished"/>
/// and uses <see cref="MqttTopicSubscriptionManager"/> for broker-level subscription tracking.
/// This eliminates the loopback HTTP connection that was previously needed in server-only mode.
/// </summary>
public sealed class ServerSignalRService : ISignalRService
{
    private readonly MqttClientService _mqttClientService;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private readonly ClientConnectionTracker _connectionTracker;
    private readonly ILogger<ServerSignalRService>? _logger;

    // Unique ID for this circuit instance — used to register subscriptions in the manager.
    private readonly string _connectionId = $"server-circuit-{Guid.NewGuid():N}";
    private bool _wasConnected;

    public event Action<MqttDataMessage>? OnDataReceived;
    public event Action<string>? OnSubscriptionConfirmed;
    public event Action<string>? OnUnsubscriptionConfirmed;
    public event Action? OnReconnected;
    public event Action<string, int>? OnMqttConnectionStatusChanged;

    public ServerSignalRService(
        MqttClientService mqttClientService,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor,
        ClientConnectionTracker connectionTracker,
        ILogger<ServerSignalRService>? logger = null)
    {
        _mqttClientService = mqttClientService;
        _subscriptionManager = subscriptionManager;
        _connectionMonitor = connectionMonitor;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    public Task StartAsync(string hubUrl)
    {
        // Subscribe to in-process events — no HTTP involved.
        _mqttClientService.OnMessagePublished += HandleMessagePublished;
        _connectionMonitor.OnStateChanged += HandleConnectionStateChanged;

        // Fire the current connection state immediately so the UI shows the right status.
        var state = _connectionMonitor.State;
        var attempts = _connectionMonitor.ReconnectAttempts;
        OnMqttConnectionStatusChanged?.Invoke(state.ToString(), attempts);
        _wasConnected = state == MqttConnectionState.Connected;

        _logger?.LogDebug("[ServerSignalRService] Started in-process, id={Id}", _connectionId);
        return Task.CompletedTask;
    }

    public async Task SubscribeToTopicAsync(string topic)
    {
        await _subscriptionManager.SubscribeClientToTopicAsync(_connectionId, topic);
        OnSubscriptionConfirmed?.Invoke(topic);
        _logger?.LogDebug("[ServerSignalRService] Subscribed to {Topic}", topic);
    }

    public async Task UnsubscribeFromTopicAsync(string topic)
    {
        await _subscriptionManager.UnsubscribeClientFromTopicAsync(_connectionId, topic);
        OnUnsubscriptionConfirmed?.Invoke(topic);
        _logger?.LogDebug("[ServerSignalRService] Unsubscribed from {Topic}", topic);
    }

    public Task<string> GetMqttBrokerInfoAsync()
    {
        var broker = _connectionMonitor.Broker;
        return Task.FromResult(string.IsNullOrEmpty(broker) ? "unknown" : broker);
    }

    public Task<int> GetConnectedClientCountAsync()
    {
        return Task.FromResult(_connectionTracker.ConnectedCount);
    }

    public async Task PublishMessageAsync(string topic, string payload)
    {
        await _mqttClientService.PublishMessageAsync(topic, payload);
        _logger?.LogDebug("[ServerSignalRService] Published to {Topic}", topic);
    }

    private Task HandleMessagePublished(string topic, string payload, DateTime timestamp)
    {
        // Use the subscription manager's wildcard-aware matching to check interest.
        var interested = _subscriptionManager.GetInterestedClients(topic);
        if (interested.Contains(_connectionId))
        {
            _logger?.LogTrace("[ServerSignalRService] Dispatching data on {Topic}", topic);
            OnDataReceived?.Invoke(new MqttDataMessage { Topic = topic, Payload = payload, Timestamp = timestamp });
        }
        return Task.CompletedTask;
    }

    private Task HandleConnectionStateChanged(MqttConnectionState state, int reconnectAttempts)
    {
        OnMqttConnectionStatusChanged?.Invoke(state.ToString(), reconnectAttempts);

        if (state == MqttConnectionState.Connected)
        {
            if (_wasConnected)
                OnReconnected?.Invoke(); // Triggers subscription restoration in MqttInitializationService
            _wasConnected = true;
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _mqttClientService.OnMessagePublished -= HandleMessagePublished;
        _connectionMonitor.OnStateChanged -= HandleConnectionStateChanged;
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(_connectionId);
        _logger?.LogDebug("[ServerSignalRService] Disposed, id={Id}", _connectionId);
    }
}
