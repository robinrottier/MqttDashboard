using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using BlazorApp1.Models;

namespace BlazorApp1.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ILogger<SignalRService> _logger;

    public event Action<MqttDataMessage>? OnDataReceived;
    public event Action<string>? OnSubscriptionConfirmed;
    public event Action<string>? OnUnsubscriptionConfirmed;

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
            .ConfigureLogging(logging =>
            {
                // Enable detailed SignalR client logging
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        _hubConnection.On<string, string, DateTime>("ReceiveMqttData", (topic, payload, timestamp) =>
        {
            _logger.LogDebug("[SignalR] Received MQTT data: Topic={Topic}, Payload={Payload}, Timestamp={Timestamp}", 
                topic, payload, timestamp);
            //Console.WriteLine($"[SignalR] Received MQTT data: Topic={topic}, Payload={payload}");

            var message = new MqttDataMessage
            {
                Topic = topic,
                Payload = payload,
                Timestamp = timestamp
            };
            OnDataReceived?.Invoke(message);
        });

        _hubConnection.On<string>("SubscriptionConfirmed", (topic) =>
        {
            _logger.LogInformation("[SignalR] Subscription confirmed: {Topic}", topic);
            //Console.WriteLine($"[SignalR] Subscription confirmed: {topic}");
            OnSubscriptionConfirmed?.Invoke(topic);
        });

        _hubConnection.On<string>("UnsubscriptionConfirmed", (topic) =>
        {
            _logger.LogInformation("[SignalR] Unsubscription confirmed: {Topic}", topic);
            //Console.WriteLine($"[SignalR] Unsubscription confirmed: {topic}");
            OnUnsubscriptionConfirmed?.Invoke(topic);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "[SignalR] Connection lost. Reconnecting...");
            Console.WriteLine($"[SignalR] Connection lost. Reconnecting... Error: {error?.Message}");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("[SignalR] Reconnected. ConnectionId: {ConnectionId}", connectionId);
            Console.WriteLine($"[SignalR] Reconnected. ConnectionId: {connectionId}");
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogError(error, "[SignalR] Connection closed");
            Console.WriteLine($"[SignalR] Connection closed. Error: {error?.Message}");
            return Task.CompletedTask;
        };

        try
        {
            _logger.LogDebug("[SignalR] Initiating connection...");
            await _hubConnection.StartAsync();
            _logger.LogInformation("[SignalR] Connected successfully. ConnectionId: {ConnectionId}", _hubConnection.ConnectionId);
            Console.WriteLine($"[SignalR] Connected. ConnectionId: {_hubConnection.ConnectionId}");
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
            _logger.LogDebug("[SignalR] Invoking SubscribeToTopic for: {Topic}", topic);
            Console.WriteLine($"[SignalR] Subscribing to topic: {topic}");
            try
            {
                await _hubConnection.InvokeAsync("SubscribeToTopic", topic);
                _logger.LogDebug("[SignalR] SubscribeToTopic invocation completed for: {Topic}", topic);
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
            _logger.LogDebug("[SignalR] Invoking UnsubscribeFromTopic for: {Topic}", topic);
            Console.WriteLine($"[SignalR] Unsubscribing from topic: {topic}");
            try
            {
                await _hubConnection.InvokeAsync("UnsubscribeFromTopic", topic);
                _logger.LogDebug("[SignalR] UnsubscribeFromTopic invocation completed for: {Topic}", topic);
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

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            _logger.LogInformation("[SignalR] Disposing connection. State: {State}", _hubConnection.State);
            Console.WriteLine("[SignalR] Disposing connection");
            await _hubConnection.DisposeAsync();
        }
    }
}
