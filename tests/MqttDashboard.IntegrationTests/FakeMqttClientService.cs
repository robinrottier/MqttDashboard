using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MqttDashboard.Server.Hubs;
using MqttDashboard.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace MqttDashboard.IntegrationTests;

/// <summary>
/// Test double that replaces <see cref="MqttClientService"/> in Tier A integration tests.
/// Does not connect to any MQTT broker. Use <see cref="TriggerIncomingMessageAsync"/>
/// to inject fake MQTT messages directly into the SignalR dispatch path.
/// </summary>
public class FakeMqttClientService : MqttClientService
{
    public FakeMqttClientService(
        IHubContext<MqttDataHub> hubContext,
        ILogger<MqttClientService> logger,
        IConfiguration configuration,
        MqttTopicSubscriptionManager subscriptionManager,
        MqttConnectionMonitor connectionMonitor)
        : base(hubContext, logger, configuration, subscriptionManager, connectionMonitor) { }

    /// <summary>Does nothing — no broker connection is made in tests.</summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    /// <summary>
    /// Simulates an incoming MQTT message: caches the value and dispatches
    /// <c>ReceiveMqttData</c> to all interested SignalR clients, exactly as the real
    /// service would when a message arrives from the broker.
    /// </summary>
    public Task TriggerIncomingMessageAsync(string topic, string payload)
        => HandleIncomingMessageAsync(topic, payload, DateTime.UtcNow);

    /// <summary>
    /// Seeds the last-known values cache without notifying any clients.
    /// Useful for pre-populating <c>GetCurrentValuesForTopics</c> in tests.
    /// </summary>
    public void SeedLastKnownValue(string topic, string value)
        => _lastKnownValues[topic] = value;
}
