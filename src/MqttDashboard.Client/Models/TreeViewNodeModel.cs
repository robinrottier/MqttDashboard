using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class TreeViewNodeModel : MudNodeModel
{
    public TreeViewNodeModel(Point? position = null) : base(position) { NodeType = "TreeView"; }

    [NpText("Root Topic", Category = "Tree View", Order = 1,
        Placeholder = "e.g. home/sensors",
        HelperText = "Displays all subtopics under this prefix.")]
    public string RootTopic { get; set; } = string.Empty;

    [NpCheckbox("Show Values", Category = "Tree View", Order = 2)]
    public bool ShowValues { get; set; } = true;
}
