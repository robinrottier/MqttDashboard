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

    /// <summary>Display style: "Full" (chip + icon), "Compact" (single row with text+icon), "IconOnly" (icon only).</summary>
    public string SwitchStyle { get; set; } = "Full";

    /// <summary>Text shown when state is ON.</summary>
    public string OnText { get; set; } = "ON";

    /// <summary>Text shown when state is OFF.</summary>
    public string OffText { get; set; } = "OFF";

    /// <summary>If true, the switch cannot be toggled by the user and does not publish to MQTT.</summary>
    public bool IsReadOnly { get; set; } = false;
}
