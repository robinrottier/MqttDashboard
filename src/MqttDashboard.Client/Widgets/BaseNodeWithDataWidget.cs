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
    private IDisposable? _dataWatcher1;
    private IDisposable? _dataWatcher2;
    private bool _disposed = false;

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

    protected void SetupDataWatchers()
    {
        _dataWatcher1?.Dispose(); _dataWatcher1 = null;
        _dataWatcher2?.Dispose(); _dataWatcher2 = null;

        if (!string.IsNullOrEmpty(Node.DataTopic))
        {
            var v = AppState.DataCache.GetValue(Node.DataTopic);
            if (v != null) { Node.DataValue = v; Node.DataLastUpdated = DateTime.Now; OnData1Updated(); }
            _dataWatcher1 = AppState.DataCache.Watch(Node.DataTopic, OnData1Received);
        }

        if (!string.IsNullOrEmpty(Node.DataTopic2))
        {
            var v2 = AppState.DataCache.GetValue(Node.DataTopic2);
            if (v2 != null) { Node.DataValue2 = v2; Node.DataLastUpdated2 = DateTime.Now; OnData2Updated(); }
            _dataWatcher2 = AppState.DataCache.Watch(Node.DataTopic2, OnData2Received);
        }
    }

    private void OnData1Received(string topic, object value)
    {
        if (_disposed) return;
        Node.DataValue = value;
        Node.DataLastUpdated = DateTime.Now;
        OnData1ReceivedCore(topic, value);
        TriggerLinkAnimation();
        try { InvokeAsync(StateHasChanged); } catch { /* circuit may be disconnected */ }
    }

    /// <summary>
    /// Called when DataValue (topic 1) is received. The default stores the value and calls
    /// <see cref="OnData1Updated"/>. Override to also use the actual <paramref name="topic"/> that fired
    /// (useful for wildcard subscriptions).
    /// </summary>
    protected virtual void OnData1ReceivedCore(string topic, object? rawValue)
    {
        OnData1Updated();
    }

    /// <summary>
    /// Updates link animation direction on all outgoing links based on the current DataValue
    /// and the node's LinkAnimation setting. Runs automatically on every data update.
    /// </summary>
    private void TriggerLinkAnimation()
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

    private void OnData2Received(string topic, object value)
    {
        if (_disposed) return;
        Node.DataValue2 = value;
        Node.DataLastUpdated2 = DateTime.Now;
        OnData2Updated();
        try { InvokeAsync(StateHasChanged); } catch { /* circuit may be disconnected */ }
    }

    /// <summary>Called after DataValue (topic 1) is updated. Override to react.</summary>
    protected virtual void OnData1Updated() { }

    /// <summary>Called after DataValue2 (topic 2) is updated. Override to react.</summary>
    protected virtual void OnData2Updated() { }

    /// <summary>
    /// Formats <see cref="MudNodeModel.Text"/> using <see cref="MudNodeModel.DataValue"/> as {0}
    /// and <see cref="MudNodeModel.DataValue2"/> as {1}, supporting C# format specifiers
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
        _dataWatcher1?.Dispose();
        _dataWatcher2?.Dispose();
        base.Dispose();
    }
}
