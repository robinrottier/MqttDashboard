using System.Text.Json.Serialization;

namespace MqttDashboard.Models;

public class NodeColorRule
{
    public string Operator { get; set; } = "<"; // <, >, <=, >=, ==, !=
    public double Threshold { get; set; } = 0;
    public string Color { get; set; } = "red"; // CSS color, e.g. "red", "#FF0000"
}

/// <summary>A single page within a multi-page dashboard.</summary>
public class PageState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Page 1";
    public List<NodeState> Nodes { get; set; } = new();
    public List<LinkState> Links { get; set; } = new();
    public int GridSize { get; set; } = 20;
    public string BackgroundColor { get; set; } = string.Empty;
}

public class DiagramState
{
    public string Name { get; set; } = string.Empty;
    public bool ShowDiagramName { get; set; } = false;

    /// <summary>
    /// User-managed list of MQTT topic subscriptions for this dashboard.
    /// Stored here so subscriptions travel with the dashboard file.
    /// Null means the field was absent (old file); empty means explicitly no subscriptions.
    /// </summary>
    public HashSet<string>? MqttSubscriptions { get; set; }

    public List<NodeState> Nodes { get; set; } = new();
    public List<LinkState> Links { get; set; } = new();
    public int GridSize { get; set; } = 20; // Default 20px grid; 0 for no grid
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Multi-page support. When populated, each page has its own node/link canvas.
    /// When null, this is a legacy single-page file — Nodes/Links/GridSize/BackgroundColor
    /// at the top level represent the single page.
    /// </summary>
    public List<PageState>? Pages { get; set; }
}

public class NodeState
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [JsonIgnore]
    public double X { get; set; }

    [JsonIgnore]
    public double Y { get; set; }

    [JsonIgnore]
    public double Width { get; set; }

    [JsonIgnore]
    public double Height { get; set; }

    // Formatted properties for JSON serialization (5 significant digits)
    [JsonPropertyName("X")]
    public double XFormatted
    {
        get => Math.Round(X, 5);
        set => X = value;
    }

    [JsonPropertyName("Y")]
    public double YFormatted
    {
        get => Math.Round(Y, 5);
        set => Y = value;
    }

    [JsonPropertyName("Width")]
    public double WidthFormatted
    {
        get => Math.Round(Width, 5);
        set => Width = value;
    }

    [JsonPropertyName("Height")]
    public double HeightFormatted
    {
        get => Math.Round(Height, 5);
        set => Height = value;
    }

    public string? Icon { get; set; }
    public string? IconName { get; set; }
    public string? Text { get; set; }
    public string? BackgroundColor { get; set; }
    public string? IconColor { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();

    // MQTT Data Binding
    public string? DataTopic { get; set; }
    public string? DataTopic2 { get; set; }
    public int? FontSize { get; set; }
    public string? LinkAnimation { get; set; }

    // Node type discriminator (null/"Text" = existing text node, backward-compatible)
    public string NodeType { get; set; } = "Text";

    // Gauge-specific
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public string? Unit { get; set; }
    // Kept for backward compat reading of old files (not written by new code)
    public double? MidPoint { get; set; }
    public string? NegativeColor { get; set; }
    public string? PositiveColor { get; set; }

    // Switch-specific
    public string? PublishTopic { get; set; }
    public string? OnValue { get; set; }
    public string? OffValue { get; set; }
    public string? SwitchStyle { get; set; }
    public string? OnText { get; set; }
    public string? OffText { get; set; }
    public bool? SwitchIsReadOnly { get; set; }

    // Battery-specific (kept for backward compat reading of old files)
    public string? LowColor { get; set; }
    public string? MedColor { get; set; }
    public string? HighColor { get; set; }
    public bool? BatteryShowPercent { get; set; }

    // Gauge ArcOrigin and shared color thresholds (used by Gauge and Battery)
    public double? ArcOrigin { get; set; }
    public List<GaugeColorThresholdState>? ColorThresholds { get; set; }

    // Title position (all node types)
    public string? TitlePosition { get; set; }

    // Log-specific
    public int? MaxEntries { get; set; }
    public bool? ShowTime { get; set; }
    public bool? ShowDate { get; set; }

    // TreeView-specific
    public string? RootTopic { get; set; }
    public bool? ShowValues { get; set; }

    public List<PortState> Ports { get; set; } = new();
}

public class PortState
{
    public string Id { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
}

public class GaugeColorThresholdState
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
    public string Direction { get; set; } = ">=";
}

public class LinkState
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string? SourcePortAlignment { get; set; }
    public string TargetNodeId { get; set; } = string.Empty;
    public string? TargetPortAlignment { get; set; }
}
