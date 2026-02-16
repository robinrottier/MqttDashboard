using System.Text.Json.Serialization;

namespace BlazorApp1.Models;

public class DiagramState
{
    public List<NodeState> Nodes { get; set; } = new();
    public List<LinkState> Links { get; set; } = new();
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

    public List<PortState> Ports { get; set; } = new();
}

public class PortState
{
    public string Id { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
}

public class LinkState
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string? SourcePortAlignment { get; set; }
    public string TargetNodeId { get; set; } = string.Empty;
    public string? TargetPortAlignment { get; set; }
}
