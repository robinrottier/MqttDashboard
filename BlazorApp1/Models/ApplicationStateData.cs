namespace BlazorApp1.Models;

/// <summary>
/// Persistent application state (saved separately from diagram data)
/// </summary>
public class ApplicationStateData
{
    /// <summary>
    /// MQTT topic subscriptions to restore on startup
    /// </summary>
    public HashSet<string> MqttSubscriptions { get; set; } = new();

    /// <summary>
    /// Other application settings can be added here in the future
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
