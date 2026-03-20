using MqttDashboard.Models;

namespace MqttDashboard.Services;

/// <summary>
/// Abstraction over the MQTT data transport layer.
/// In WASM (browser), implemented by <see cref="SignalRService"/> (HTTP WebSocket to the server hub).
/// In Blazor Server, implemented by <c>ServerSignalRService</c> (in-process, no HTTP loopback).
/// </summary>
public interface ISignalRService : IAsyncDisposable
{
    event Action<MqttDataMessage>? OnDataReceived;
    event Action<string>? OnSubscriptionConfirmed;
    event Action<string>? OnUnsubscriptionConfirmed;
    event Action? OnReconnected;
    event Action<string, int>? OnMqttConnectionStatusChanged;

    /// <summary>Starts the service. For SignalRService, connects to the hub at <paramref name="hubUrl"/>. For ServerSignalRService, the URL is unused.</summary>
    Task StartAsync(string hubUrl);
    Task SubscribeToTopicAsync(string topic);
    Task UnsubscribeFromTopicAsync(string topic);
    Task<string> GetMqttBrokerInfoAsync();
    Task<int> GetConnectedClientCountAsync();
    Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0);
}
