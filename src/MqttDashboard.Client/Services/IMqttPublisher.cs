namespace MqttDashboard.Services;

/// <summary>
/// Publish-only contract: sends a message upstream to the MQTT broker.
/// Implemented by <c>SignalRDataServer</c> (WASM/browser) and <c>InProcessDataServer</c> (Blazor Server).
/// Kept separate from <see cref="MqttDashboard.Data.IDataServer"/> because publishing is a command
/// concern (fire-and-forget write), not part of the pub/sub data-subscription contract.
/// </summary>
public interface IMqttPublisher
{
    Task PublishMessageAsync(string topic, string payload, bool retain = false, int qos = 0);
}
