using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class LogNodeModel : MudNodeModel
{
    public LogNodeModel(Point? position = null) : base(position) { NodeType = "Log"; }

    [NpNumeric("Max Entries", Category = "Log", Order = 1, Min = 1, Max = 500)]
    public int MaxEntries { get; set; } = 20;

    [NpCheckbox("Date", Category = "Columns", Order = 2)]
    public bool ShowDate { get; set; } = false;

    [NpCheckbox("Time", Category = "Columns", Order = 3)]
    public bool ShowTime { get; set; } = true;

    [NpCheckbox("Full topic", Category = "Columns", Order = 4)]
    public bool ShowTopicFull { get; set; } = false;

    [NpCheckbox("Topic path", Category = "Columns", Order = 5)]
    public bool ShowTopicPath { get; set; } = false;

    [NpCheckbox("Topic name", Category = "Columns", Order = 6)]
    public bool ShowTopicName { get; set; } = false;

    [NpCheckbox("Value", Category = "Columns", Order = 7)]
    public bool ShowValue { get; set; } = true;
}
