using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorApp1.Server.Hubs;

public class MqttDataHub : Hub
{
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly ILogger<MqttDataHub> _logger;

    public MqttDataHub(
        MqttTopicSubscriptionManager subscriptionManager,
        ILogger<MqttDataHub> logger)
    {
        _subscriptionManager = subscriptionManager;
        _logger = logger;
    }

    public async Task SubscribeToTopic(string topic)
    {
        _logger.LogInformation("Client {ConnectionId} requesting subscription to topic: {Topic}", Context.ConnectionId, topic);

        var success = await _subscriptionManager.SubscribeClientToTopicAsync(Context.ConnectionId, topic);
        if (success)
        {
            _logger.LogInformation("Client {ConnectionId} successfully subscribed to topic: {Topic}", Context.ConnectionId, topic);
            await Clients.Caller.SendAsync("SubscriptionConfirmed", topic);
        }
        else
        {
            _logger.LogWarning("Client {ConnectionId} already subscribed to topic: {Topic}", Context.ConnectionId, topic);
            // Still send confirmation even if already subscribed
            await Clients.Caller.SendAsync("SubscriptionConfirmed", topic);
        }
    }

    public async Task UnsubscribeFromTopic(string topic)
    {
        _logger.LogInformation("Client {ConnectionId} requesting unsubscription from topic: {Topic}", Context.ConnectionId, topic);

        var success = await _subscriptionManager.UnsubscribeClientFromTopicAsync(Context.ConnectionId, topic);
        if (success)
        {
            _logger.LogInformation("Client {ConnectionId} successfully unsubscribed from topic: {Topic}", Context.ConnectionId, topic);
            await Clients.Caller.SendAsync("UnsubscriptionConfirmed", topic);
        }
        else
        {
            _logger.LogWarning("Client {ConnectionId} was not subscribed to topic: {Topic}", Context.ConnectionId, topic);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to MQTT Hub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(Context.ConnectionId);
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with exception", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected and unsubscribed from all topics", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
