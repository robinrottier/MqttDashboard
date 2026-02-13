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
        try
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var mqttBroker = _configuration["MqttSettings:Broker"] ?? "localhost";
            var mqttPort = int.Parse(_configuration["MqttSettings:Port"] ?? "1883");
            var mqttUsername = _configuration["MqttSettings:Username"];
            var mqttPassword = _configuration["MqttSettings:Password"];

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

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                var timestamp = DateTime.UtcNow;

                _logger.LogInformation("Received MQTT message on topic {Topic}: {Payload}", topic, payload);

                // Get clients interested in this topic
                var interestedClients = _subscriptionManager.GetInterestedClients(topic);

                if (interestedClients.Any())
                {
                    // Send only to interested clients
                    await _hubContext.Clients.Clients(interestedClients.ToList())
                        .SendAsync("ReceiveMqttData", topic, payload, timestamp, stoppingToken);

                    _logger.LogInformation("Sent MQTT message to {Count} interested clients", interestedClients.Count);
                }
            };

            await _mqttClient.ConnectAsync(options, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker at {Broker}:{Port}", mqttBroker, mqttPort);

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
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions);
            _logger.LogInformation("Subscribed to MQTT topic: {Topic}", topic);
        }
    }

    private async Task UnsubscribeFromMqttTopicAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            return;
        }

        if (_subscribedTopics.TryRemove(topic, out _))
        {
            await _mqttClient.UnsubscribeAsync(topic);
            _logger.LogInformation("Unsubscribed from MQTT topic: {Topic}", topic);
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
