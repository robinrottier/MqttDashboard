using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class BatteryNodeModel : MudNodeModel
{
    public BatteryNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Battery";
    }

    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 100;

    /// <summary>Threshold-based color stops. Value is compared against percentage (0-100). Last match wins.</summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; } = new();

    /// <summary>CSS colour when charge is low (below 20%). Default: error red.</summary>
    [System.Obsolete("Use ColorThresholds instead.")]
    public string? LowColor { get; set; }

    /// <summary>CSS colour when charge is medium (20-50%). Default: warning orange.</summary>
    [System.Obsolete("Use ColorThresholds instead.")]
    public string? MedColor { get; set; }

    /// <summary>CSS colour when charge is high (above 50%). Default: success green.</summary>
    [System.Obsolete("Use ColorThresholds instead.")]
    public string? HighColor { get; set; }

    /// <summary>Show percentage text below the battery icon.</summary>
    public bool ShowPercent { get; set; } = true;
}
