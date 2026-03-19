using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MqttDashboard.Server.Hubs;
using System.Collections.Concurrent;

namespace MqttDashboard.Server.Services;

public class MqttClientService : BackgroundService
{
    private readonly IHubContext<MqttDataHub> _hubContext;
    private readonly ILogger<MqttClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttTopicSubscriptionManager _subscriptionManager;
    private readonly MqttConnectionMonitor _connectionMonitor;
    private IMqttClient? _mqttClient;
    private MqttClientOptions? _mqttOptions;
    private readonly ConcurrentDictionary<string, bool> _subscribedTopics = new();
    private CancellationToken _stoppingToken;

    /// <summary>
    /// Fires when an MQTT message is received from the broker. Used by in-process subscribers
    /// (e.g. ServerSignalRService) to receive data without going through the SignalR hub over HTTP.
    /// </summary>
    public event Func<string, string, DateTime, Task>? OnMessagePublished;

    public MqttClientService(
        IHubContext<MqttDataHub> hubContext,
        ILogger<MqttClientService> logger,
        IConfiguration configuration,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor)
    {
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;
        _subscriptionManager = subscriptionManager;
        _connectionMonitor = connectionMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
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

            _connectionMonitor.SetBroker($"{mqttBroker}:{mqttPort}");

            _connectionMonitor.OnStateChanged += async (state, attempts) =>
            {
                await _hubContext.Clients.All.SendAsync("MqttConnectionStatus", state.ToString(), attempts, stoppingToken);
            };

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBroker, mqttPort)
                .WithClientId($"MqttDashboard_{Guid.NewGuid()}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(mqttUsername))
            {
                optionsBuilder.WithCredentials(mqttUsername, mqttPassword);
                _logger.LogInformation("MQTT client configured with username: {Username}", mqttUsername);
            }

            _mqttOptions = optionsBuilder.Build();

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT client disconnected. Reason: {Reason}, Was clean: {WasClean}",
                    e.Reason, e.ClientWasConnected);
                if (e.Exception != null)
                    _logger.LogError(e.Exception, "MQTT disconnection error");

                await ReconnectWithBackoffAsync();
            };

            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("MQTT client connected. Session present: {SessionPresent}",
                    e.ConnectResult.IsSessionPresent);
                await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connected);
                // Re-subscribe all tracked topics after reconnect
                foreach (var topic in _subscribedTopics.Keys)
                {
                    try
                    {
                        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                            .WithTopicFilter(f => f.WithTopic(topic))
                            .Build();
                        await _mqttClient!.SubscribeAsync(subscribeOptions);
                        _logger.LogDebug("Re-subscribed to MQTT topic after reconnect: {Topic}", topic);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to re-subscribe to topic {Topic} after reconnect", topic);
                    }
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                var timestamp = DateTime.UtcNow;

                _logger.LogTrace("Received MQTT message on topic {Topic}: {Payload}", topic, payload);

                var interestedClients = _subscriptionManager.GetInterestedClients(topic);

                _logger.LogTrace("Found {Count} interested clients for topic {Topic}", interestedClients.Count, topic);

                if (interestedClients.Any())
                {
                    try
                    {
                        await _hubContext.Clients.Clients(interestedClients.ToList())
                            .SendAsync("ReceiveMqttData", topic, payload, timestamp, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending MQTT message to clients for topic {Topic}", topic);
                    }
                }
                else
                {
                    _logger.LogTrace("No interested clients found for topic {Topic}", topic);
                }

                // Notify in-process subscribers (e.g. ServerSignalRService) regardless of SignalR clients.
                var inProcessHandler = OnMessagePublished;
                if (inProcessHandler != null)
                {
                    try { await inProcessHandler.Invoke(topic, payload, timestamp); }
                    catch (Exception ex) { _logger.LogError(ex, "Error notifying in-process subscriber for topic {Topic}", topic); }
                }
            };

            _logger.LogInformation("Attempting to connect to MQTT broker at {Broker}:{Port}...", mqttBroker, mqttPort);
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connecting);
            var connectResult = await _mqttClient.ConnectAsync(_mqttOptions, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker. Result: {ResultCode}", connectResult.ResultCode);

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
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MQTT Client Service stopping (cancellation requested)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MQTT client service");
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Failed);
        }
    }

    private async Task ReconnectWithBackoffAsync()
    {
        var delay = TimeSpan.FromSeconds(2);
        var attempt = 0;

        while (!_stoppingToken.IsCancellationRequested && _mqttClient != null)
        {
            attempt++;
            await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Connecting, attempt);
            _logger.LogInformation("MQTT reconnect attempt {Attempt}, waiting {Delay}s...", attempt, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, _stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await _mqttClient.ConnectAsync(_mqttOptions!, _stoppingToken);
                // ConnectedAsync handler will update state to Connected
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT reconnect attempt {Attempt} failed", attempt);
                // Exponential backoff capped at 60s
                delay = delay.TotalSeconds >= 60
                    ? TimeSpan.FromSeconds(60)
                    : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }

        await _connectionMonitor.UpdateStateAsync(MqttConnectionState.Disconnected);
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
            var result = await _mqttClient.SubscribeAsync(subscribeOptions);
            var resultCode = result.Items.FirstOrDefault()?.ResultCode;
            _logger.LogDebug("Subscribed to MQTT topic: {Topic}. Result: {ResultCode}", topic, resultCode);
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
            await _mqttClient.UnsubscribeAsync(topic);
            _logger.LogDebug("Unsubscribed from MQTT topic: {Topic}", topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }

    public async Task PublishMessageAsync(string topic, string payload)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger.LogWarning("Cannot publish to {Topic}: MQTT client is not connected", topic);
            return;
        }
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            var result = await _mqttClient.PublishAsync(message);
            _logger.LogInformation("Published to {Topic}: {Payload} (ReasonCode: {ReasonCode})", topic, payload, result.ReasonCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to {Topic}", topic);
            throw;
        }
    }
}
