using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class SwitchNodeModel : MudNodeModel
{
    public SwitchNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Switch";
    }

    /// <summary>Topic to publish to when toggled. Defaults to DataTopic if empty.</summary>
    public string? PublishTopic { get; set; }

    /// <summary>Payload to publish when switched ON.</summary>
    public string OnValue { get; set; } = "1";

    /// <summary>Payload to publish when switched OFF.</summary>
    public string OffValue { get; set; } = "0";
}
