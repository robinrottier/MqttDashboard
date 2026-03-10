using System.Collections.Concurrent;

namespace MqttDashboard.Server.Hubs;

public class MqttTopicSubscriptionManager
{
    private readonly ConcurrentDictionary<string, TopicSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _clientTopics = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event Func<string, Task>? OnTopicSubscribeRequested;
    public event Func<string, Task>? OnTopicUnsubscribeRequested;

    public async Task<bool> SubscribeClientToTopicAsync(string connectionId, string topic)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Track topics for this client
            if (!_clientTopics.TryGetValue(connectionId, out var clientTopics))
            {
                clientTopics = new HashSet<string>();
                _clientTopics[connectionId] = clientTopics;
            }

            // Check if client is already subscribed to this topic
            if (clientTopics.Contains(topic))
            {
                return false; // Already subscribed
            }

            clientTopics.Add(topic);

            // Get or create topic subscription
            var subscription = _subscriptions.GetOrAdd(topic, _ => new TopicSubscription(topic));
            
            subscription.AddClient(connectionId);

            // If this is the first client for this topic, subscribe to MQTT broker
            if (subscription.RefCount == 1)
            {
                if (OnTopicSubscribeRequested != null)
                {
                    await OnTopicSubscribeRequested.Invoke(topic);
                }
            }

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> UnsubscribeClientFromTopicAsync(string connectionId, string topic)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_clientTopics.TryGetValue(connectionId, out var clientTopics))
            {
                return false;
            }

            if (!clientTopics.Remove(topic))
            {
                return false; // Not subscribed
            }

            if (!_subscriptions.TryGetValue(topic, out var subscription))
            {
                return false;
            }

            subscription.RemoveClient(connectionId);

            // If no more clients are interested, unsubscribe from MQTT broker
            if (subscription.RefCount == 0)
            {
                _subscriptions.TryRemove(topic, out _);
                if (OnTopicUnsubscribeRequested != null)
                {
                    await OnTopicUnsubscribeRequested.Invoke(topic);
                }
            }

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UnsubscribeClientFromAllTopicsAsync(string connectionId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_clientTopics.TryRemove(connectionId, out var clientTopics))
            {
                return;
            }

            foreach (var topic in clientTopics)
            {
                if (_subscriptions.TryGetValue(topic, out var subscription))
                {
                    subscription.RemoveClient(connectionId);

                    if (subscription.RefCount == 0)
                    {
                        _subscriptions.TryRemove(topic, out _);
                        if (OnTopicUnsubscribeRequested != null)
                        {
                            await OnTopicUnsubscribeRequested.Invoke(topic);
                        }
                    }
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public HashSet<string> GetInterestedClients(string topic)
    {
        var interestedClients = new HashSet<string>();

        foreach (var subscription in _subscriptions.Values)
        {
            if (TopicMatches(subscription.Topic, topic))
            {
                foreach (var client in subscription.GetClients())
                {
                    interestedClients.Add(client);
                }
            }
        }

        return interestedClients;
    }

    private bool TopicMatches(string filter, string topic)
    {
        // Handle exact match
        if (filter == topic)
            return true;

        // Handle wildcard matching
        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');

        // Multi-level wildcard '#' must be the last character
        if (filterParts.Length > 0 && filterParts[^1] == "#")
        {
            // Match all remaining levels
            for (int i = 0; i < filterParts.Length - 1; i++)
            {
                if (i >= topicParts.Length)
                    return false;
                if (filterParts[i] != "+" && filterParts[i] != topicParts[i])
                    return false;
            }
            return true;
        }

        // Must have same number of levels for single-level wildcard matching
        if (filterParts.Length != topicParts.Length)
            return false;

        // Check each level
        for (int i = 0; i < filterParts.Length; i++)
        {
            if (filterParts[i] == "+")
                continue; // Single-level wildcard matches any value at this level
            if (filterParts[i] != topicParts[i])
                return false;
        }

        return true;
    }

    public List<string> GetClientSubscriptions(string connectionId)
    {
        if (_clientTopics.TryGetValue(connectionId, out var topics))
        {
            return topics.ToList();
        }
        return new List<string>();
    }

    private class TopicSubscription
    {
        private readonly HashSet<string> _clients = new();
        public string Topic { get; }
        public int RefCount => _clients.Count;

        public TopicSubscription(string topic)
        {
            Topic = topic;
        }

        public void AddClient(string connectionId)
        {
            _clients.Add(connectionId);
        }

        public void RemoveClient(string connectionId)
        {
            _clients.Remove(connectionId);
        }

        public HashSet<string> GetClients()
        {
            return new HashSet<string>(_clients);
        }
    }
}
