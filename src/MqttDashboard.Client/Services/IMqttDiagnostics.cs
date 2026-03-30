namespace MqttDashboard.Services;

/// <summary>
/// Thin diagnostics contract: exposes read-only server/broker info for the About dialog
/// and connection-status display.
/// Implemented by the concrete data-server classes alongside <see cref="MqttDashboard.Data.IDataServer"/>.
/// </summary>
public interface IMqttDiagnostics
{
    Task<string> GetMqttBrokerInfoAsync();
    Task<int> GetConnectedClientCountAsync();
}
