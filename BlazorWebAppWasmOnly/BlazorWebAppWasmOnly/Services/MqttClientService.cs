using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using BlazorWebAppWasmOnly.Hubs;

namespace BlazorWebAppWasmOnly.Services;

public class MqttClientService : BackgroundService
{
    private readonly IHubContext<MqttDataHub> _hubContext;
    private readonly ILogger<MqttClientService> _logger;
    private readonly IConfiguration _configuration;
    private IMqttClient? _mqttClient;

    public MqttClientService(
        IHubContext<MqttDataHub> hubContext,
        ILogger<MqttClientService> logger,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            var mqttBroker = _configuration["MqttSettings:Broker"] ?? "localhost";
            var mqttPort = int.Parse(_configuration["MqttSettings:Port"] ?? "1883");
            var mqttTopic = _configuration["MqttSettings:Topic"] ?? "#";
            var mqttUsername = _configuration["MqttSettings:Username"];
            var mqttPassword = _configuration["MqttSettings:Password"];

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBroker, mqttPort)
                .WithClientId($"BlazorMqttClient_{Guid.NewGuid()}")
                .WithCleanSession();

            // Add credentials if username is provided
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

                await _hubContext.Clients.All.SendAsync("ReceiveMqttData", topic, payload, timestamp, stoppingToken);
            };

            await _mqttClient.ConnectAsync(options, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker at {Broker}:{Port}", mqttBroker, mqttPort);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(mqttTopic))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, stoppingToken);
            _logger.LogInformation("Subscribed to MQTT topic: {Topic}", mqttTopic);

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
