using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class TreeViewNodeModel : MudNodeModel
{
    public TreeViewNodeModel(Point? position = null) : base(position) { NodeType = "TreeView"; }
    /// <summary>Root MQTT topic prefix to display. e.g. "home/sensors". Shows all subtopics.</summary>
    public string RootTopic { get; set; } = string.Empty;
    /// <summary>Show current value next to each leaf topic.</summary>
    public bool ShowValues { get; set; } = true;
}
