using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MqttDashboard.Server.Services;

namespace MqttDashboard.Server.Hubs;

public class MqttDataHub : Hub
{
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly ILogger<MqttDataHub> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private readonly ClientConnectionTracker _connectionTracker;
    private readonly MqttClientService _mqttClientService;

    public MqttDataHub(
        MqttTopicSubscriptionManager subscriptionManager,
        ILogger<MqttDataHub> logger,
        IConfiguration configuration,
        MqttConnectionMonitor connectionMonitor,
        ClientConnectionTracker connectionTracker,
        MqttClientService mqttClientService)
    {
        _subscriptionManager = subscriptionManager;
        _logger = logger;
        _configuration = configuration;
        _connectionMonitor = connectionMonitor;
        _connectionTracker = connectionTracker;
        _mqttClientService = mqttClientService;
    }

    public async Task SubscribeToTopic(string topic)
    {
        _logger.LogInformation("Client {ConnectionId} requesting subscription to topic: {Topic}", Context.ConnectionId, topic);
        var success = await _subscriptionManager.SubscribeClientToTopicAsync(Context.ConnectionId, topic);
        if (success)
            _logger.LogInformation("Client {ConnectionId} successfully subscribed to topic: {Topic}", Context.ConnectionId, topic);
        else
            _logger.LogWarning("Client {ConnectionId} already subscribed to topic: {Topic}", Context.ConnectionId, topic);

        // Always send confirmation
        await Clients.Caller.SendAsync("SubscriptionConfirmed", topic);
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

    public Task<string> GetMqttBrokerInfo()
    {
        var broker = _configuration["MqttSettings:Broker"] ?? "unknown";
        var port = _configuration["MqttSettings:Port"] ?? "1883";
        return Task.FromResult($"{broker}:{port}");
    }

    public Task<string> GetMqttConnectionStatus()
    {
        return Task.FromResult(_connectionMonitor.State.ToString());
    }

    public Task<int> GetConnectedClientCount() => Task.FromResult(_connectionTracker.ConnectedCount);

    public async Task PublishMessage(string topic, string payload)
    {
        _logger.LogInformation("Client {ConnectionId} publishing to topic: {Topic}", Context.ConnectionId, topic);
        await _mqttClientService.PublishMessageAsync(topic, payload);
    }

    public override async Task OnConnectedAsync()
    {
        _connectionTracker.Increment();
        _logger.LogInformation("Client {ConnectionId} connected to MQTT Hub", Context.ConnectionId);
        // Push current MQTT state to the newly connected client
        await Clients.Caller.SendAsync("MqttConnectionStatus",
            _connectionMonitor.State.ToString(),
            _connectionMonitor.ReconnectAttempts);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionTracker.Decrement();
        await _subscriptionManager.UnsubscribeClientFromAllTopicsAsync(Context.ConnectionId);
        if (exception != null)
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with exception", Context.ConnectionId);
        else
            _logger.LogInformation("Client {ConnectionId} disconnected and unsubscribed from all topics", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
