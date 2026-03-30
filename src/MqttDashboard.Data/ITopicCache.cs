namespace MqttDashboard.Data;

/// <summary>
/// In-memory pub/sub topic cache. Stores the most recent value per topic and
/// notifies registered watchers (exact and wildcard) on every update.
/// </summary>
public interface ITopicCache
{
    /// <summary>Store a value and notify all matching watchers.</summary>
    void UpdateValue(string topic, object value);

    /// <summary>Retrieve the last cached value for a topic, or <c>null</c> if not yet received.</summary>
    object? GetValue(string topic);

    /// <summary>Type-safe retrieval. Returns <c>false</c> if the topic is absent or has the wrong type.</summary>
    bool TryGetValue<T>(string topic, out T? value);

    /// <summary>
    /// Subscribe to value changes for <paramref name="topic"/>.
    /// Supports MQTT wildcards: <c>+</c> (single level) and <c>#</c> (multi-level).
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    IDisposable Watch(string topic, Action<string, object> callback);

    /// <summary>All topics currently in the cache.</summary>
    IEnumerable<string> GetAllTopics();

    /// <summary>All cached values whose topic matches the given MQTT wildcard pattern.</summary>
    Dictionary<string, object> GetValuesByPattern(string pattern);

    /// <summary>Remove all cached values and watchers.</summary>
    void Clear();
}
