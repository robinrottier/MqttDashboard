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
        Node.DataValue = value;
        Node.DataLastUpdated = DateTime.Now;
        OnData1Updated();
        InvokeAsync(StateHasChanged);
    }

    private void OnData2Received(string topic, object value)
    {
        Node.DataValue2 = value;
        Node.DataLastUpdated2 = DateTime.Now;
        OnData2Updated();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called after DataValue (topic 1) is updated. Override to react.</summary>
    protected virtual void OnData1Updated() { }

    /// <summary>Called after DataValue2 (topic 2) is updated. Override to react.</summary>
    protected virtual void OnData2Updated() { }

    public override void Dispose()
    {
        _dataWatcher1?.Dispose();
        _dataWatcher2?.Dispose();
        base.Dispose();
    }
}
