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

    /// <summary>
    /// Value at which the arc originates (the "zero" point of the arc).
    /// When set, the arc draws from this value to the current value.
    /// When null, defaults to MinValue (arc always starts from the left end).
    /// </summary>
    public double? ArcOrigin { get; set; }

    /// <summary>
    /// Ordered list of threshold color stops. Arc takes color of the highest threshold not exceeded
    /// by the absolute distance from ArcOrigin.
    /// </summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; } = new();
}

public class GaugeColorThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
}
