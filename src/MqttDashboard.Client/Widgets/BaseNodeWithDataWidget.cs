using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components;
using MqttDashboard.Models;

namespace MqttDashboard.Widgets;

/// <summary>
/// Extends <see cref="BaseNodeWidget{TNode}"/> with automatic MQTT data watcher
/// setup for <see cref="MudNodeModel.DataTopic"/> and <see cref="MudNodeModel.DataTopic2"/>.
/// Override <see cref="OnData1Updated"/> / <see cref="OnData2Updated"/> to react to new values.
/// </summary>
public abstract class BaseNodeWithDataWidget<TNode> : BaseNodeWidget<TNode>
    where TNode : MudNodeModel
{
    private readonly List<IDisposable> _dataWatchers = new();
    private bool _disposed = false;
    // Track the last topics key so we skip SetupDataWatchers when nothing has changed.
    private string? _watcherTopicsKey = null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SetupDataWatchers();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        SetupDataWatchers();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Ensure link animations are applied once the SVG is in the DOM.
            // Node widgets only mount after IsInteractive = true (guarded by @if in Display.razor),
            // so firstRender reliably fires when the diagram canvas is rendered.
            TriggerLinkAnimation();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected void SetupDataWatchers()
    {
        // Skip rebuild if topics haven't changed — prevents redundant re-runs
        // from every OnParametersSet (triggered by RefreshAll, StateHasChanged, etc.).
        var topicsKey = string.Join(",", Node.DataTopics);
        if (topicsKey == _watcherTopicsKey) return;
        _watcherTopicsKey = topicsKey;

        foreach (var w in _dataWatchers) w.Dispose();
        _dataWatchers.Clear();

        var topics = Node.DataTopics.Count > 0
            ? Node.DataTopics.Cast<string?>().ToList()
            : new List<string?>();

        // Size the runtime arrays to match the topic list.
        Node.DataValues       = new object?[topics.Count];
        Node.DataUpdatedTimes = new DateTime?[topics.Count];

        for (int i = 0; i < topics.Count; i++)
        {
            var topic = topics[i];
            if (string.IsNullOrEmpty(topic)) continue;
            var idx = i;
            var capturedTopic = topic;

            var v = AppState.DataCache.GetValue(capturedTopic);
            if (v != null)
            {
                Node.DataValues[idx]       = v;
                Node.DataUpdatedTimes[idx] = DateTime.Now;
                if (idx == 0) { OnData1Updated(); TriggerLinkAnimation(); }
                else if (idx == 1) OnData2Updated();
            }

            var watcher = AppState.DataCache.Watch(capturedTopic, (t, value) =>
            {
                if (_disposed) return;
                Node.DataValues[idx]       = value;
                Node.DataUpdatedTimes[idx] = DateTime.Now;
                if (idx == 0)
                {
                    OnData1ReceivedCore(t, value);
                    TriggerLinkAnimation();
                }
                else if (idx == 1) OnData2Updated();
                OnDataReceivedCore(idx, t, value);
                try { InvokeAsync(StateHasChanged); } catch { /* circuit may be disconnected */ }
            });
            _dataWatchers.Add(watcher);
        }
    }

    /// <summary>
    /// Called when DataValue (topic 1) is received. Override to also use the actual
    /// <paramref name="topic"/> that fired (useful for wildcard subscriptions).
    /// </summary>
    protected virtual void OnData1ReceivedCore(string topic, object? rawValue)
    {
        OnData1Updated();
    }

    /// <summary>
    /// Called for every topic index when a value is received. Override to react to
    /// any topic by index without replacing <see cref="OnData1ReceivedCore"/>.
    /// </summary>
    protected virtual void OnDataReceivedCore(int index, string topic, object? rawValue) { }

    /// <summary>
    /// Updates link animation direction on all outgoing links based on the current DataValue
    /// and the node's LinkAnimation setting. Runs automatically on every data update.
    /// </summary>
    protected void TriggerLinkAnimation()
    {
        if (Node.LinkAnimation == null || Node.LinkAnimation == "None") return;
        if (Node.DataValue == null || !double.TryParse(Node.DataValue.ToString(), out var d)) return;

        if (Node.LinkAnimation == "Reverse") d = -d;

        foreach (var port in Node.Ports)
        {
            foreach (var link in port.Links)
            {
                if (link is not LinkModel l || l.Animations == null || l.Animations[0] == null) continue;
                var ani = l.Animations[0];
                var anchor = link.Source as SinglePortAnchor;
                if (anchor?.Port != port) continue;

                var to = d > 0 ? "-10" : d < 0 ? "10" : "0";
                if (to != ani.To)
                {
                    ani.To = to;
                    l.Refresh();
                }
            }
        }
    }

    /// <summary>Called after DataValue (topic 1) is updated. Override to react.</summary>
    protected virtual void OnData1Updated() { }

    /// <summary>Called after DataValue2 (topic 2) is updated. Override to react.</summary>
    protected virtual void OnData2Updated() { }

    /// <summary>
    /// Formats <see cref="MudNodeModel.Text"/> using data values as positional args:
    /// {0} = DataValues[0], {1} = DataValues[1], etc. Supports C# format specifiers
    /// e.g. "Temp: {0:F1}°C". Returns the raw Text if no format tokens are present or on error.
    /// </summary>
    protected string FormatText()
    {
        if (string.IsNullOrEmpty(Node.Text)) return string.Empty;
        try
        {
            return string.Format(Node.Text,
                new FormattableValue(Node.DataValue),
                new FormattableValue(Node.DataValue2));
        }
        catch { return Node.Text; }
    }

    /// <summary>Wraps an arbitrary MQTT value for use with string.Format numeric format specifiers.</summary>
    private sealed class FormattableValue : IFormattable
    {
        private readonly object? _value;
        public FormattableValue(object? value) => _value = value;

        public string ToString(string? format, IFormatProvider? provider)
        {
            try
            {
                if (format != null)
                {
                    switch (format[0])
                    {
                        case 'E': case 'F': case 'G': case 'N': case '0':
                            if (_value?.GetType() == typeof(string))
                            { if (double.TryParse(_value.ToString(), out double d)) return d.ToString(format, provider); }
                            else if (_value is int iv) return ((double)iv).ToString(format, provider);
                            break;
                        case 'I': case 'X':
                            if (_value?.GetType() == typeof(string))
                            { if (int.TryParse(_value.ToString(), out int i)) return i.ToString(format, provider); }
                            else if (_value is double dv) return ((int)dv).ToString(format, provider);
                            break;
                    }
                }
            }
            catch { }
            if (_value == null) return "";
            return (_value as IFormattable)?.ToString(format, provider) ?? (_value.ToString() ?? "");
        }
    }

    public override void Dispose()
    {
        _disposed = true;
        _watcherTopicsKey = null;
        foreach (var w in _dataWatchers) w.Dispose();
        _dataWatchers.Clear();
        base.Dispose();
    }
}
