using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class GaugeNodeModel : MudNodeModel
{
    public GaugeNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Gauge";
    }

    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;
    public string? Unit { get; set; }

    /// <summary>
    /// Optional midpoint value. If set, the arc is coloured NegativeColor below this value
    /// and PositiveColor at or above it. If null, uses threshold-based colouring (primary/warning/error).
    /// </summary>
    public double? MidPoint { get; set; }

    /// <summary>CSS colour used when value is below MidPoint. Default: MudBlazor error (red).</summary>
    public string? NegativeColor { get; set; }

    /// <summary>CSS colour used when value is at or above MidPoint. Default: MudBlazor success (green).</summary>
    public string? PositiveColor { get; set; }
}
