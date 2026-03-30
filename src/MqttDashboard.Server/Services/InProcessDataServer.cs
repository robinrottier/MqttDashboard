using MqttDashboard.Data;
using MqttDashboard.Models;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Services;
using Microsoft.Extensions.Logging;

namespace MqttDashboard.Server.Services;

/// <summary>
/// In-process <see cref="IDataServer"/> implementation for Blazor Server circuits.
/// Bypasses HTTP/WebSocket entirely: hooks <see cref="MqttClientService.OnMessagePublished"/>
/// directly and uses <see cref="MqttTopicSubscriptionManager"/> for broker-level subscription tracking.
/// Also implements <see cref="IMqttPublisher"/> and <see cref="IMqttDiagnostics"/>.
/// </summary>
public sealed class InProcessDataServer : IDataServer, IMqttPublisher, IMqttDiagnostics
{
    private readonly MqttClientService _mqttClientService;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private readonly ClientConnectionTracker _connectionTracker;
    private readonly ILogger<InProcessDataServer>? _logger;

    private readonly string _connectionId = $"server-circuit-{Guid.NewGuid():N}";
    private bool _wasConnected;

    public event Action<string, object>? ValueUpdated;
    public event Action? Reconnected;
    public event Action<string, bool>? StatusChanged;

    public InProcessDataServer(
        MqttClientService mqttClientService,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor,
        ClientConnectionTracker connectionTracker,
        ILogger<InProcessDataServer>? logger = null)
    {
        _mqttClientService = mqttClientService;
        _subscriptionManager = subscriptionManager;
        _connectionMonitor = connectionMonitor;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    public Task StartAsync(string serverUrl = "")
    {
        _mqttClientService.OnMessagePublished += HandleMessagePublished;
        _connectionMonitor.OnStateChanged += HandleConnectionStateChanged;

        // Fire initial state so the UI reflects the current MQTT connection.
        var state = _connectionMonitor.State;
        var attempts = _connectionMonitor.ReconnectAttempts;
        var connected = state == MqttConnectionState.Connected;
        var broker = _connectionMonitor.Broker ?? "unknown";
        StatusChanged?.Invoke(connected ? $"MQTT Connected ({broker})" : $"MQTT {state}", connected);
        _wasConnected = connected;

        _logger?.LogDebug("[InProcessDataServer] Started, id={Id}", _connectionId);
        return Task.CompletedTask;
    }

    public async Task SubscribeAsync(string topic)
    {
        await _subscriptionManager.SubscribeClientToTopicAsync(_connectionId, topic);
        _logger?.LogDebug("[InProcessDataServer] Subscribed to {Topic}", topic);
    }

    public async Task UnsubscribeAsync(string topic)
    {
        await _subscriptionManager.UnsubscribeClientFromTopicAsync(_connectionId, topic);
        _logger?.LogDebug("[InProcessDataServer] Unsubscribed from {Topic}", topic);
    }

    public async Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0)
    {
        await _mqttClientService.PublishMessageAsync(topic, payload, retain, qos);
        _logger?.LogDebug("[InProcessDataServer] Published to {Topic}", topic);
    }

    public Task<string> GetMqttBrokerInfoAsync()
    {
        var broker = _connectionMonitor.Broker;
        return Task.FromResult(string.IsNullOrEmpty(broker) ? "unknown" : broker);
    }

    public Task<int> GetConnectedClientCountAsync()
        => Task.FromResult(_connectionTracker.ConnectedCount);

    private Task HandleMessagePublished(string topic, string payload, DateTime timestamp)
    {
        var interested = _subscriptionManager.GetInterestedClients(topic);
        if (interested.Contains(_connectionId))
        {
            _logger?.LogTrace("[InProcessDataServer] Dispatching data on {Topic}", topic);
            ValueUpdated?.Invoke(topic, payload);
        }
        return Task.CompletedTask;
    }

    private Task HandleConnectionStateChanged(MqttConnectionState state, int reconnectAttempts)
    {
        var connected = state == MqttConnectionState.Connected;
        var broker = _connectionMonitor.Broker ?? "unknown";
        var status = connected
            ? $"MQTT Connected ({broker})"
            : reconnectAttempts > 0
                ? $"MQTT reconnecting (attempt {reconnectAttempts})..."
                : $"MQTT {state}";
        StatusChanged?.Invoke(status, connected);

        if (connected)
        {
            if (_wasConnected)
                Reconnected?.Invoke();
            _wasConnected = true;
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _mqttClientService.OnMessagePublished -= HandleMessagePublished;
        _connectionMonitor.OnStateChanged -= HandleConnectionStateChanged;
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(_connectionId);
        _logger?.LogDebug("[InProcessDataServer] Disposed, id={Id}", _connectionId);
    }
}
