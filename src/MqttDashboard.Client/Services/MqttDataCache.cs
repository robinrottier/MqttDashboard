using System.Collections.Concurrent;

namespace MqttDashboard.Services;

/// <summary>
/// Cache for MQTT message values with change notifications
/// Stores values in a hierarchical structure based on topic path
/// </summary>
public class MqttDataCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Dictionary<string, List<Action<string, object>>> _watchers = new();
    private readonly object _watcherLock = new();

    /// <summary>
    /// Update a value in the cache and notify watchers
    /// </summary>
    public void UpdateValue(string topic, object value)
    {
        _cache[topic] = value;
        NotifyWatchers(topic, value);
    }

    /// <summary>
    /// Get a value from the cache
    /// </summary>
    public object? GetValue(string topic)
    {
        return _cache.TryGetValue(topic, out var value) ? value : null;
    }

    /// <summary>
    /// Try to get a typed value from the cache
    /// </summary>
    public bool TryGetValue<T>(string topic, out T? value)
    {
        if (_cache.TryGetValue(topic, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Watch a topic for value changes
    /// </summary>
    public IDisposable Watch(string topic, Action<string, object> callback)
    {
        lock (_watcherLock)
        {
            if (!_watchers.ContainsKey(topic))
            {
                _watchers[topic] = new List<Action<string, object>>();
            }
            _watchers[topic].Add(callback);
        }

        // Return disposable to unwatch
        return new WatcherHandle(this, topic, callback);
    }

    /// <summary>
    /// Get all cached topics
    /// </summary>
    public IEnumerable<string> GetAllTopics()
    {
        return _cache.Keys;
    }

    /// <summary>
    /// Get all values for topics matching a pattern (simple wildcard support)
    /// </summary>
    public Dictionary<string, object> GetValuesByPattern(string pattern)
    {
        var results = new Dictionary<string, object>();
        
        // Convert MQTT wildcard pattern to regex
        var regexPattern = "^" + pattern
            .Replace("+", "[^/]+")
            .Replace("#", ".*")
            .Replace("/", "\\/") + "$";
        
        var regex = new System.Text.RegularExpressions.Regex(regexPattern);
        
        foreach (var kvp in _cache)
        {
            if (regex.IsMatch(kvp.Key))
            {
                results[kvp.Key] = kvp.Value;
            }
        }
        
        return results;
    }

    /// <summary>
    /// Clear the cache
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    private void NotifyWatchers(string topic, object value)
    {
        List<Action<string, object>>? callbacks = null;
        
        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var watchers))
            {
                callbacks = new List<Action<string, object>>(watchers);
            }
        }

        if (callbacks != null)
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(topic, value);
                }
                catch
                {
                    // Ignore callback errors
                }
            }
        }
    }

    private void RemoveWatcher(string topic, Action<string, object> callback)
    {
        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var watchers))
            {
                watchers.Remove(callback);
                if (watchers.Count == 0)
                {
                    _watchers.Remove(topic);
                }
            }
        }
    }

    private class WatcherHandle : IDisposable
    {
        private readonly MqttDataCache _cache;
        private readonly string _topic;
        private readonly Action<string, object> _callback;
        private bool _disposed;

        public WatcherHandle(MqttDataCache cache, string topic, Action<string, object> callback)
        {
            _cache = cache;
            _topic = topic;
            _callback = callback;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cache.RemoveWatcher(_topic, _callback);
                _disposed = true;
            }
        }
    }
}
