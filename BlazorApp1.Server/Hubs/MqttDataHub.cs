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
        var success = await _subscriptionManager.SubscribeClientToTopicAsync(Context.ConnectionId, topic);
        if (success)
        {
            _logger.LogInformation("Client {ConnectionId} subscribed to topic: {Topic}", Context.ConnectionId, topic);
            await Clients.Caller.SendAsync("SubscriptionConfirmed", topic);
        }
        else
        {
            _logger.LogWarning("Client {ConnectionId} already subscribed to topic: {Topic}", Context.ConnectionId, topic);
        }
    }

    public async Task UnsubscribeFromTopic(string topic)
    {
        var success = await _subscriptionManager.UnsubscribeClientFromTopicAsync(Context.ConnectionId, topic);
        if (success)
        {
            _logger.LogInformation("Client {ConnectionId} unsubscribed from topic: {Topic}", Context.ConnectionId, topic);
            await Clients.Caller.SendAsync("UnsubscriptionConfirmed", topic);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(Context.ConnectionId);
        _logger.LogInformation("Client {ConnectionId} disconnected and unsubscribed from all topics", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
