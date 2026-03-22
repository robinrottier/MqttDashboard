using Blazor.Diagrams.Core.Geometry;

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

    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;

    /// <summary>0-based index into DataTopics that drives the fill level.</summary>
    public int DataTopicIndex { get; set; } = 0;

    /// <summary>Colour transition settings (topic index + threshold rules) for this battery.</summary>
    public ColorTransition BatteryColor { get; set; } = new();

    /// <summary>Show percentage text inside the battery icon.</summary>
    public bool ShowPercent { get; set; } = true;
}

