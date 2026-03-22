using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class GaugeNodeModel : MudNodeModel
{
    public GaugeNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Gauge";
        GaugeColor = new ColorTransition
        {
            ColorThresholds =
            [
                new GaugeColorThreshold { Value = 0, Direction = "<=", Color = "var(--mud-palette-error)" },
                new GaugeColorThreshold { Value = 0, Direction = ">=", Color = "var(--mud-palette-success)" },
            ]
        };
    }

    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;
    public string? Unit { get; set; }

    /// <summary>
    /// Value at which the arc originates (the "zero" point of the arc).
    /// When null, defaults to MinValue (arc always starts from the left end).
    /// </summary>
    public double? ArcOrigin { get; set; }

    /// <summary>0-based index of the data topic whose value drives the gauge arc and label.</summary>
    public int DataTopicIndex { get; set; } = 0;

    /// <summary>Where the static Text label is displayed relative to the gauge arc: "Above" or "Below" (default).</summary>
    public string TextPosition { get; set; } = "Below";

    /// <summary>Colour transition settings (topic index + threshold rules) for this gauge.</summary>
    public ColorTransition GaugeColor { get; set; } = new();
}
