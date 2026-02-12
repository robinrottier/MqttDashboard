using Microsoft.AspNetCore.SignalR;

namespace BlazorWebAppWasmOnly.Hubs;

public class MqttDataHub : Hub
{
    public async Task SendMqttData(string topic, string payload, DateTime timestamp)
    {
        await Clients.All.SendAsync("ReceiveMqttData", topic, payload, timestamp);
    }
}
