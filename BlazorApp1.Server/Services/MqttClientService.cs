using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using BlazorApp1.Server.Hubs;
using System.Collections.Concurrent;

namespace BlazorApp1.Server.Services;

public class MqttClientService : BackgroundService
{
    private readonly IHubContext<MqttDataHub> _hubContext;
    private readonly ILogger<MqttClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private IMqttClient? _mqttClient;
    private readonly ConcurrentDictionary<string, bool> _subscribedTopics = new();

    public MqttClientService(
        IHubContext<MqttDataHub> hubContext,
        ILogger<MqttClientService> logger,
        IConfiguration configuration,
        MqttTopicSubscriptionManager subscriptionManager)
    {
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;
        _subscriptionManager = subscriptionManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MQTT Client Service starting...");
        try
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var mqttBroker = _configuration["MqttSettings:Broker"] ?? "localhost";
            var mqttPort = int.Parse(_configuration["MqttSettings:Port"] ?? "1883");
            var mqttUsername = _configuration["MqttSettings:Username"];
            var mqttPassword = _configuration["MqttSettings:Password"];

            _logger.LogInformation("MQTT Configuration - Broker: {Broker}, Port: {Port}, Username: {Username}", 
                mqttBroker, mqttPort, string.IsNullOrEmpty(mqttUsername) ? "<none>" : mqttUsername);

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBroker, mqttPort)
                .WithClientId($"BlazorMqttClient_{Guid.NewGuid()}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(mqttUsername))
            {
                optionsBuilder.WithCredentials(mqttUsername, mqttPassword);
                _logger.LogInformation("MQTT client configured with username: {Username}", mqttUsername);
            }

            var options = optionsBuilder.Build();

            // Add connection event handlers
            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT client disconnected. Reason: {Reason}, Was clean: {WasClean}", 
                    e.Reason, e.ClientWasConnected);
                if (e.Exception != null)
                {
                    _logger.LogError(e.Exception, "MQTT disconnection error");
                }
            };

            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("MQTT client connected successfully. Session present: {SessionPresent}", 
                    e.ConnectResult.IsSessionPresent);
            };

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                var timestamp = DateTime.UtcNow;

                _logger.LogInformation("Received MQTT message on topic {Topic}: {Payload}", topic, payload);

                // Get clients interested in this topic
                var interestedClients = _subscriptionManager.GetInterestedClients(topic);

                _logger.LogInformation("Found {Count} interested clients for topic {Topic}", interestedClients.Count, topic);

                if (interestedClients.Any())
                {
                    try
                    {
                        // Send only to interested clients
                        await _hubContext.Clients.Clients(interestedClients.ToList())
                            .SendAsync("ReceiveMqttData", topic, payload, timestamp, stoppingToken);

                        _logger.LogTrace("Sent MQTT message to {Count} interested clients for topic {Topic}", interestedClients.Count, topic);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending MQTT message to clients for topic {Topic}", topic);
                    }
                }
                else
                {
                    _logger.LogWarning("No interested clients found for topic {Topic}", topic);
                }
            };

            _logger.LogInformation("Attempting to connect to MQTT broker at {Broker}:{Port}...", mqttBroker, mqttPort);
            var connectResult = await _mqttClient.ConnectAsync(options, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker at {Broker}:{Port}. Result: {ResultCode}, Session: {Session}", 
                mqttBroker, mqttPort, connectResult.ResultCode, connectResult.IsSessionPresent);
            // Wire up subscription manager events
            _subscriptionManager.OnTopicSubscribeRequested += async topic =>
            {
                await SubscribeToMqttTopicAsync(topic);
            };

            _subscriptionManager.OnTopicUnsubscribeRequested += async topic =>
            {
                await UnsubscribeFromMqttTopicAsync(topic);
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MQTT client service");
        }
    }

    private async Task SubscribeToMqttTopicAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot subscribe to topic {Topic}: MQTT client not connected", topic);
            return;
        }

        if (_subscribedTopics.TryAdd(topic, true))
        {
            _logger.LogDebug("Attempting to subscribe to MQTT topic: {Topic}", topic);
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();

            var result = await _mqttClient.SubscribeAsync(subscribeOptions);
            var resultCode = result.Items.FirstOrDefault()?.ResultCode;
            _logger.LogInformation("Subscribed to MQTT topic: {Topic}. Result: {ResultCode}", 
                topic, resultCode);
        }
        else
        {
            _logger.LogDebug("Already subscribed to MQTT topic: {Topic}", topic);
        }
    }

    private async Task UnsubscribeFromMqttTopicAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logger.LogWarning("Cannot unsubscribe from topic {Topic}: MQTT client not connected", topic);
            return;
        }

        if (_subscribedTopics.TryRemove(topic, out _))
        {
            _logger.LogDebug("Attempting to unsubscribe from MQTT topic: {Topic}", topic);
            await _mqttClient.UnsubscribeAsync(topic);
            _logger.LogInformation("Unsubscribed from MQTT topic: {Topic}", topic);
        }
        else
        {
            _logger.LogDebug("Topic {Topic} was not in subscribed topics list", topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Disconnected from MQTT broker");
        }

        await base.StopAsync(cancellationToken);
    }
}
