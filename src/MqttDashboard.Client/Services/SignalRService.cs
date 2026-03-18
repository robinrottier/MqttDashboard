using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MqttDashboard.Models;

namespace MqttDashboard.Services;

public class SignalRService : ISignalRService
{
    private HubConnection? _hubConnection;
    private readonly ILogger<SignalRService> _logger;

    public event Action<MqttDataMessage>? OnDataReceived;
    public event Action<string>? OnSubscriptionConfirmed;
    public event Action<string>? OnUnsubscriptionConfirmed;
    public event Action? OnReconnected;
    public event Action<string, int>? OnMqttConnectionStatusChanged;

    public SignalRService(ILogger<SignalRService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(string hubUrl)
    {
        _logger.LogInformation("Starting SignalR connection to {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
            .Build();

        _hubConnection.On<string, string, DateTime>("ReceiveMqttData", (topic, payload, timestamp) =>
        {
            _logger.LogDebug("[SignalR] Received MQTT data: Topic={Topic}, Payload={Payload}", topic, payload);
            OnDataReceived?.Invoke(new MqttDataMessage
            {
                Topic = topic,
                Payload = payload,
                Timestamp = timestamp
            });
        });

        _hubConnection.On<string>("SubscriptionConfirmed", topic =>
        {
            _logger.LogInformation("[SignalR] Subscription confirmed: {Topic}", topic);
            OnSubscriptionConfirmed?.Invoke(topic);
        });

        _hubConnection.On<string>("UnsubscriptionConfirmed", topic =>
        {
            _logger.LogInformation("[SignalR] Unsubscription confirmed: {Topic}", topic);
            OnUnsubscriptionConfirmed?.Invoke(topic);
        });

        _hubConnection.On<string, int>("MqttConnectionStatus", (state, attempts) =>
        {
            _logger.LogInformation("[SignalR] MQTT connection status: {State}, attempts: {Attempts}", state, attempts);
            OnMqttConnectionStatusChanged?.Invoke(state, attempts);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "[SignalR] Connection lost. Reconnecting...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("[SignalR] Reconnected. ConnectionId: {ConnectionId}", connectionId);
            OnReconnected?.Invoke();
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogError(error, "[SignalR] Connection closed");
            return Task.CompletedTask;
        };

        try
        {
            _logger.LogDebug("[SignalR] Initiating connection...");
            await _hubConnection.StartAsync();
            _logger.LogInformation("[SignalR] Connected successfully. ConnectionId: {ConnectionId}", _hubConnection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalR] Failed to start connection to {HubUrl}", hubUrl);
            throw;
        }
    }

    public async Task SubscribeToTopicAsync(string topic)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("[SignalR] Subscribing to topic: {Topic}", topic);
            try
            {
                await _hubConnection.InvokeAsync("SubscribeToTopic", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Failed to subscribe to topic: {Topic}", topic);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("[SignalR] Cannot subscribe to {Topic}: Hub connection is null", topic);
        }
    }

    public async Task UnsubscribeFromTopicAsync(string topic)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("[SignalR] Unsubscribing from topic: {Topic}", topic);
            try
            {
                await _hubConnection.InvokeAsync("UnsubscribeFromTopic", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Failed to unsubscribe from topic: {Topic}", topic);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("[SignalR] Cannot unsubscribe from {Topic}: Hub connection is null", topic);
        }
    }

    public async Task<string> GetMqttBrokerInfoAsync()
    {
        if (_hubConnection is not null)
        {
            try
            {
                return await _hubConnection.InvokeAsync<string>("GetMqttBrokerInfo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SignalR] Failed to get MQTT broker info");
                return "unknown";
            }
        }
        return "unknown";
    }

    public async Task<int> GetConnectedClientCountAsync()
    {
        if (_hubConnection is not null)
        {
            try { return await _hubConnection.InvokeAsync<int>("GetConnectedClientCount"); }
            catch (Exception ex) { _logger.LogError(ex, "[SignalR] Failed to get client count"); return -1; }
        }
        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            _logger.LogInformation("[SignalR] Disposing connection. State: {State}", _hubConnection.State);
            await _hubConnection.DisposeAsync();
        }
    }
}
