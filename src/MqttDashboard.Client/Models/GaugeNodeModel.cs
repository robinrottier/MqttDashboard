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
    /// Value at which the arc originates (the "zero" point of the arc).
    /// When set, the arc draws from this value to the current value.
    /// When null, defaults to MinValue (arc always starts from the left end).
    /// </summary>
    public double? ArcOrigin { get; set; }

    /// <summary>
    /// Ordered list of threshold color stops. Last matching rule wins.
    /// Each threshold has a Direction ("&gt;=" or "&lt;=") and a Value.
    /// Matching is based on absolute distance from ArcOrigin.
    /// </summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; } = new();
}

public class GaugeColorThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
    /// <summary>Direction of the threshold comparison. Valid values: "&gt;=" (default) or "&lt;=".</summary>
    public string Direction { get; set; } = ">=";
}
