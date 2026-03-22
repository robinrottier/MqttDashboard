using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

/// <summary>A single row in a Grid node.</summary>
public class GridRowDefinition
{
    /// <summary>Optional label shown in the first column.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>One MQTT topic per column (may be empty string for no binding).</summary>
    public List<string> Topics { get; set; } = new();
}

/// <summary>
/// Node model for the Grid widget — a table whose cells each bind to an MQTT topic.
/// </summary>
public class GridNodeModel : MudNodeModel
{
    public GridNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Grid";
    }

    /// <summary>Header text for each data column.</summary>
    public List<string> ColumnHeaders { get; set; } = ["Value"];

    /// <summary>One entry per table row.</summary>
    public List<GridRowDefinition> Rows { get; set; } =
    [
        new GridRowDefinition { Label = "Row 1", Topics = [""] }
    ];
}
