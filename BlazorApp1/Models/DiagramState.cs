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
    public double X { get; set; }
    public double Y { get; set; }
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
