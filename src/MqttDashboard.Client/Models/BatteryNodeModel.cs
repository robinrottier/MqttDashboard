using Blazor.Diagrams.Core.Geometry;
using MqttDashboard.Components;

namespace MqttDashboard.Models;

public class BatteryNodeModel : MudNodeModel
{
    public BatteryNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Battery";
        BatteryColor = new ColorTransition
        {
            ColorThresholds =
            [
                new GaugeColorThreshold { Value = 25,  Direction = "<=", Color = "var(--mud-palette-error)" },
                new GaugeColorThreshold { Value = 50,  Direction = "<=", Color = "var(--mud-palette-warning)" },
                new GaugeColorThreshold { Value = 100, Direction = ">=", Color = "var(--mud-palette-success)" },
            ]
        };
    }

    [NpCustom("Range", typeof(NumericRangeEditor), Category = "Battery", Order = 1)]
    public NumericRangeSettings Range { get; set; } = new();

    [NpCheckbox("Show Percentage", Category = "Battery", Order = 2)]
    public bool ShowPercent { get; set; } = true;

    [NpCustom("Color Transitions", typeof(ColorTransitionGroupEditor), Category = "Battery", Order = 3)]
    public ColorTransition BatteryColor { get; set; } = new();

    // Backward-compatible convenience accessors.
    public double MinValue => Range.Min;
    public double MaxValue => Range.Max;
    public int DataTopicIndex => Range.DataTopicIndex;
}

