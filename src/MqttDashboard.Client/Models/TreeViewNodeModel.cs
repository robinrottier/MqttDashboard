using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class TreeViewNodeModel : TextNodeModel
{
    public TreeViewNodeModel(Point? position = null) : base(position) { NodeType = "TreeView"; }

    [NpText("Root Topic", Category = "Tree View", Order = 1,
        Placeholder = "e.g. home/sensors",
        HelperText = "Displays all subtopics under this prefix.")]
    public string RootTopic { get; set; } = string.Empty;

    [NpCheckbox("Show Values", Category = "Tree View", Order = 2)]
    public bool ShowValues { get; set; } = true;

    public override NodeData ToData(double panX = 0, double panY = 0)
    {
        var data = new TreeViewNodeData
        {
            RootTopic = string.IsNullOrEmpty(RootTopic) ? null : RootTopic,
            ShowValues = ShowValues ? null : false,   // default true; only store when false
        };
        FillBaseData(data, panX, panY);
        return data;
    }

    public static TreeViewNodeModel FromData(TreeViewNodeData data)
    {
        var node = new TreeViewNodeModel(new Point(data.X, data.Y))
        {
            RootTopic = data.RootTopic ?? string.Empty,
            ShowValues = data.ShowValues ?? true,
        };
        return ApplyBaseData(node, data);
    }
}
