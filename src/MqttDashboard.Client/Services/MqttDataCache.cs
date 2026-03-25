using System.Collections.Concurrent;
using MqttDashboard.Helpers;

namespace MqttDashboard.Services;

/// <summary>
/// Cache for MQTT message values with change notifications
/// Stores values in a hierarchical structure based on topic path
/// </summary>
public class MqttDataCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Dictionary<string, List<Action<string, object>>> _watchers = new();
    private readonly List<(System.Text.RegularExpressions.Regex pattern, Action<string, object> callback)> _wildcardWatchers = new();
    private readonly object _watcherLock = new();

    /// <summary>
    /// Update a value in the cache and notify watchers.
    /// String values are sanitized to strip invalid XML characters, preventing
    /// InvalidCharacterError when values are rendered into SVG or DOM text nodes.
    /// </summary>
    public void UpdateValue(string topic, object value)
    {
        if (value is string s)
            value = XmlStringHelper.StripInvalidXmlChars(s);
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
    /// Watch a topic for value changes. Supports MQTT wildcards: # and +.
    /// </summary>
    public IDisposable Watch(string topic, Action<string, object> callback)
    {
        if (topic.Contains('#') || topic.Contains('+'))
        {
            // Wildcard topic — convert to regex and store in wildcard list
            var regexPattern = "^" + topic
                .Replace("+", "[^/]+")
                .Replace("#", ".*")
                .Replace("/", "\\/") + "$";
            var regex = new System.Text.RegularExpressions.Regex(regexPattern);
            lock (_watcherLock)
            {
                _wildcardWatchers.Add((regex, callback));
            }
            return new WildcardWatcherHandle(this, regex, callback);
        }

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

        // Notify wildcard watchers
        List<Action<string, object>>? wildcardCallbacks = null;
        lock (_watcherLock)
        {
            wildcardCallbacks = _wildcardWatchers
                .Where(w => w.pattern.IsMatch(topic))
                .Select(w => w.callback)
                .ToList();
        }
        foreach (var cb in wildcardCallbacks)
        {
            try { cb(topic, value); } catch { }
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

    private void RemoveWildcardWatcher(System.Text.RegularExpressions.Regex pattern, Action<string, object> callback)
    {
        lock (_watcherLock)
        {
            var idx = _wildcardWatchers.FindIndex(w => ReferenceEquals(w.pattern, pattern) && ReferenceEquals(w.callback, callback));
            if (idx >= 0) _wildcardWatchers.RemoveAt(idx);
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

    private class WildcardWatcherHandle : IDisposable
    {
        private readonly MqttDataCache _cache;
        private readonly System.Text.RegularExpressions.Regex _pattern;
        private readonly Action<string, object> _callback;
        private bool _disposed;

        public WildcardWatcherHandle(MqttDataCache cache, System.Text.RegularExpressions.Regex pattern, Action<string, object> callback)
        {
            _cache = cache;
            _pattern = pattern;
            _callback = callback;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cache.RemoveWildcardWatcher(_pattern, _callback);
                _disposed = true;
            }
        }
    }
}
