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
    public int GridSize { get; set; } = 10;
    public string BackgroundColor { get; set; } = string.Empty;
}

/// <summary>Metadata written into each dashboard file for traceability.</summary>
public class DiagramFileInfo
{
    public string WrittenAt { get; set; } = string.Empty;
    public string? Filename  { get; set; }
}

public class DiagramState
{
    [JsonPropertyOrder(0)]  public string Name { get; set; } = string.Empty;
    [JsonPropertyOrder(1)]  public bool ShowDiagramName { get; set; } = false;
    [JsonPropertyOrder(2)]  public int GridSize { get; set; } = 10;
    [JsonPropertyOrder(3)]  public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Multi-page support. When populated, each page has its own node/link canvas.
    /// When null, this is a legacy single-page file — Nodes/Links/GridSize/BackgroundColor
    /// at the top level represent the single page.
    /// </summary>
    [JsonPropertyOrder(4)]  public List<PageState>? Pages { get; set; }

    /// <summary>
    /// User-managed list of MQTT topic subscriptions for this dashboard.
    /// Stored here so subscriptions travel with the dashboard file.
    /// Null means the field was absent (old file); empty means explicitly no subscriptions.
    /// </summary>
    [JsonPropertyOrder(5)]  public HashSet<string>? MqttSubscriptions { get; set; }

    [JsonPropertyOrder(6)]  public List<NodeState> Nodes { get; set; } = new();
    [JsonPropertyOrder(7)]  public List<LinkState> Links { get; set; } = new();

    /// <summary>Written last so it doesn't clutter the top of the file.</summary>
    [JsonPropertyOrder(99)] public DiagramFileInfo? FileInfo { get; set; }
}

public class NodeState
{
    // Node type discriminator must always be first for readability
    [JsonPropertyOrder(0)]
    public string NodeType { get; set; } = "Text";

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

    [JsonPropertyName("X")]
    public double XFormatted
    {
        get => Math.Round(X, 2);
        set => X = value;
    }

    [JsonPropertyName("Y")]
    public double YFormatted
    {
        get => Math.Round(Y, 2);
        set => Y = value;
    }

    [JsonPropertyName("Width")]
    public double WidthFormatted
    {
        get => Math.Round(Width, 2);
        set => Width = value;
    }

    [JsonPropertyName("Height")]
    public double HeightFormatted
    {
        get => Math.Round(Height, 2);
        set => Height = value;
    }

    public string? Icon { get; set; }
    public string? IconName { get; set; }
    public string? Text { get; set; }
    public string? BackgroundColor { get; set; }
    public string? IconColor { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    // MQTT Data Binding
    public List<string>? DataTopics { get; set; }
    public int? FontSize { get; set; }
    public string? LinkAnimation { get; set; }

    // Gauge-specific
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public string? Unit { get; set; }
    public double? Origin { get; set; }
    public int? DataTopicIndex { get; set; }
    public string? TextPosition { get; set; }

    // Switch-specific
    public string? PublishTopic { get; set; }
    public string? OnValue { get; set; }
    public string? OffValue { get; set; }
    public string? SwitchStyle { get; set; }
    public string? OnText { get; set; }
    public string? OffText { get; set; }
    public bool? SwitchIsReadOnly { get; set; }
    public bool? SwitchRetain { get; set; }
    public int? SwitchQosLevel { get; set; }

    // Battery-specific
    public bool? BatteryShowPercent { get; set; }

    // Colour transition (Gauge = GaugeColor, Battery = BatteryColor — stored as a nested object)
    public ColorTransitionState? GaugeColor { get; set; }
    public ColorTransitionState? BatteryColor { get; set; }

    // Title position (all node types)
    public string? TitlePosition { get; set; }

    // Log-specific
    public int? MaxEntries { get; set; }
    public bool? ShowTime { get; set; }
    public bool? ShowDate { get; set; }
    public bool? ShowTopicFull { get; set; }
    public bool? ShowTopicPath { get; set; }
    public bool? ShowTopicName { get; set; }
    public bool? ShowValue { get; set; }

    // TreeView-specific
    public string? RootTopic { get; set; }
    public bool? ShowValues { get; set; }

    // Image/background (base — any node type)
    public string? BackgroundImageUrl { get; set; }
    public string? BackgroundObjectFit { get; set; }
    public bool? BackgroundImageFromData { get; set; } // legacy field, ignored on load

    // Legacy image node fields — kept for loading old files; mapped to base properties on load
    public string? StaticImageUrl { get; set; }
    public string? ObjectFit { get; set; }

    public List<PortState>? Ports { get; set; }
}

public class PortState
{
    public string Id { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
}

public class ColorTransitionState
{
    public int? ColorTopicIndex { get; set; }
    public List<GaugeColorThresholdState>? ColorThresholds { get; set; }
    public string? ElseColor { get; set; }
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
