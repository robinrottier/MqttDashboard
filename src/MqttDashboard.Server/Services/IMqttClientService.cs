namespace MqttDashboard.Server.Services;

/// <summary>
/// Interface for the MQTT client service, exposing the functionality needed by
/// <see cref="Hubs.MqttDataHub"/> and test doubles.
/// </summary>
public interface IMqttClientService
{
    /// <summary>Last-known payload for each topic received since server start.</summary>
    IReadOnlyDictionary<string, string> LastKnownValues { get; }

    /// <summary>Publishes a message to the MQTT broker.</summary>
    Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0);
}
