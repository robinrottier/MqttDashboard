using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class LogNodeModel : MudNodeModel
{
    public LogNodeModel(Point? position = null) : base(position) { NodeType = "Log"; }
    public int MaxEntries { get; set; } = 20;
    // Column visibility
    public bool ShowDate { get; set; } = false;
    public bool ShowTime { get; set; } = true;
    public bool ShowTopicFull { get; set; } = false;
    public bool ShowTopicPath { get; set; } = false;
    public bool ShowTopicName { get; set; } = false;
    public bool ShowValue { get; set; } = true;
}
