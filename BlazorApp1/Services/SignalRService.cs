using Microsoft.AspNetCore.SignalR.Client;
using BlazorApp1.Models;

namespace BlazorApp1.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    public event Action<MqttDataMessage>? OnDataReceived;

    public async Task StartAsync(string hubUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string, string, DateTime>("ReceiveMqttData", (topic, payload, timestamp) =>
        {
            var message = new MqttDataMessage
            {
                Topic = topic,
                Payload = payload,
                Timestamp = timestamp
            };
            OnDataReceived?.Invoke(message);
        });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
