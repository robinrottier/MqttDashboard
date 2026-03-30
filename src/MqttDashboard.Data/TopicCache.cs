using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MqttDashboard.Data;

/// <summary>
/// Thread-safe in-memory topic cache. Implements <see cref="ITopicCache"/>.
/// String values are sanitized to strip invalid XML characters before storage so that
/// widgets can safely inject values into SVG / DOM text nodes.
/// </summary>
public class TopicCache : ITopicCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Dictionary<string, List<Action<string, object>>> _watchers = new();
    private readonly List<(Regex pattern, Action<string, object> callback)> _wildcardWatchers = new();
    private readonly object _watcherLock = new();

    /// <inheritdoc/>
    public void UpdateValue(string topic, object value)
    {
        if (value is string s)
            value = XmlPayloadHelper.StripInvalidXmlChars(s);
        _cache[topic] = value;
        NotifyWatchers(topic, value);
    }

    /// <inheritdoc/>
    public object? GetValue(string topic) =>
        _cache.TryGetValue(topic, out var v) ? v : null;

    /// <inheritdoc/>
    public bool TryGetValue<T>(string topic, out T? value)
    {
        if (_cache.TryGetValue(topic, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public IDisposable Watch(string topic, Action<string, object> callback)
    {
        if (topic.Contains('#') || topic.Contains('+'))
        {
            var regex = new Regex(TopicMatcher.ToRegexPattern(topic));
            lock (_watcherLock)
                _wildcardWatchers.Add((regex, callback));
            return new WildcardWatcherHandle(this, regex, callback);
        }

        lock (_watcherLock)
        {
            if (!_watchers.TryGetValue(topic, out var list))
                _watchers[topic] = list = new List<Action<string, object>>();
            list.Add(callback);
        }
        return new WatcherHandle(this, topic, callback);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetAllTopics() => _cache.Keys;

    /// <inheritdoc/>
    public Dictionary<string, object> GetValuesByPattern(string pattern)
    {
        var regex = new Regex(TopicMatcher.ToRegexPattern(pattern));
        var result = new Dictionary<string, object>();
        foreach (var kvp in _cache)
            if (regex.IsMatch(kvp.Key))
                result[kvp.Key] = kvp.Value;
        return result;
    }

    /// <inheritdoc/>
    public void Clear() => _cache.Clear();

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void NotifyWatchers(string topic, object value)
    {
        List<Action<string, object>>? exact = null;
        List<Action<string, object>>? wildcards = null;

        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var list))
                exact = new List<Action<string, object>>(list);

            wildcards = _wildcardWatchers
                .Where(w => w.pattern.IsMatch(topic))
                .Select(w => w.callback)
                .ToList();
        }

        foreach (var cb in exact ?? [])
            try { cb(topic, value); } catch { }

        foreach (var cb in wildcards ?? [])
            try { cb(topic, value); } catch { }
    }

    private void RemoveWatcher(string topic, Action<string, object> callback)
    {
        lock (_watcherLock)
        {
            if (_watchers.TryGetValue(topic, out var list))
            {
                list.Remove(callback);
                if (list.Count == 0) _watchers.Remove(topic);
            }
        }
    }

    private void RemoveWildcardWatcher(Regex pattern, Action<string, object> callback)
    {
        lock (_watcherLock)
        {
            var idx = _wildcardWatchers.FindIndex(
                w => ReferenceEquals(w.pattern, pattern) && ReferenceEquals(w.callback, callback));
            if (idx >= 0) _wildcardWatchers.RemoveAt(idx);
        }
    }

    // ── Inner handle classes ──────────────────────────────────────────────────────

    private sealed class WatcherHandle(TopicCache cache, string topic, Action<string, object> callback) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            cache.RemoveWatcher(topic, callback);
            _disposed = true;
        }
    }

    private sealed class WildcardWatcherHandle(TopicCache cache, Regex pattern, Action<string, object> callback) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            cache.RemoveWildcardWatcher(pattern, callback);
            _disposed = true;
        }
    }
}
