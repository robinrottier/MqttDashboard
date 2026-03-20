using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class LogNodeModel : MudNodeModel
{
    public LogNodeModel(Point? position = null) : base(position) { NodeType = "Log"; }
    public int MaxEntries { get; set; } = 20;
    public bool ShowTime { get; set; } = true;
    public bool ShowDate { get; set; } = false;
}
