using Blazor.Diagrams.Core.Geometry;
using MqttDashboard.Components;

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

    [NpCustom("Range", typeof(NumericRangeEditor), Category = "Gauge", Order = 1)]
    public NumericRangeSettings Range { get; set; } = new();

    [NpText("Unit", Category = "Gauge", Order = 2, Placeholder = "°C, W…")]
    public string? Unit { get; set; }

    [NpSelect("Text Position", "Below", "Above", Category = "Gauge", Order = 3, Labels = ["Below", "Above"])]
    public string TextPosition { get; set; } = "Below";

    [NpCustom("Color Transitions", typeof(ColorTransitionGroupEditor), Category = "Gauge", Order = 4)]
    public ColorTransition GaugeColor { get; set; } = new();

    // Backward-compatible convenience accessors used by the widget rendering code.
    public double MinValue => Range.Min;
    public double MaxValue => Range.Max;
    public double? Origin => Range.Origin;
    public int DataTopicIndex => Range.DataTopicIndex;
}
