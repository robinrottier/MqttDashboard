using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class GaugeNodeModel : MudNodeModel
{
    public GaugeNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Gauge";
        ColorThresholds =
        [
            new GaugeColorThreshold { Value = 0, Direction = "<=", Color = "var(--mud-palette-error)" },
            new GaugeColorThreshold { Value = 0, Direction = ">=", Color = "var(--mud-palette-success)" },
        ];
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
    /// 0-based index of the data topic whose value drives the gauge arc and label.
    /// 0 = first topic (DataValue), 1 = second topic (DataValue2).
    /// </summary>
    public int DataTopicIndex { get; set; } = 0;

    /// <summary>
    /// Where the static Text label is displayed relative to the gauge arc: "Above" or "Below" (default).
    /// </summary>
    public string TextPosition { get; set; } = "Below";

    /// <summary>
    /// Ordered list of color thresholds. First matching rule wins.
    /// Each threshold has a Direction ("&gt;=" or "&lt;="), a Value, and an optional TopicIndex.
    /// </summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; }
}

public class GaugeColorThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
    /// <summary>Direction of the threshold comparison: ">=" or "<=".</summary>
    public string Direction { get; set; } = ">=";
    /// <summary>0-based topic index whose value is compared. 0 = DataValue, 1 = DataValue2.</summary>
    public int TopicIndex { get; set; } = 0;
}
