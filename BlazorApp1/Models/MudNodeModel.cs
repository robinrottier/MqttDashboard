using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;

namespace BlazorApp1.Models
{
    public class MudNodeModel : NodeModel
    {
        public MudNodeModel(Point? position = null) : base(position)
        {
        }

        /// <summary>
        /// Icon name from MudBlazor Icons (e.g., Icons.Material.Filled.Home)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Human-readable icon name for display
        /// </summary>
        public string? IconName { get; set; }

        /// <summary>
        /// Secondary text displayed below the title
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Background color for the node
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Icon color
        /// </summary>
        public string? IconColor { get; set; }

        /// <summary>
        /// Custom metadata dictionary for future extensibility
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// MQTT topic to bind to for live data updates
        /// </summary>
        public string? DataTopic { get; set; }

        /// <summary>
        /// Optional second MQTT topic
        /// </summary>
        public string? DataTopic2 { get; set; }

        /// <summary>
        /// Current data value from MQTT topic
        /// </summary>
        public object? DataValue { get; set; }

        /// <summary>
        /// Current data value from the second MQTT topic
        /// </summary>
        public object? DataValue2 { get; set; }

        /// <summary>
        /// Last time the data value was updated
        /// </summary>
        public DateTime? DataLastUpdated { get; set; }

        /// <summary>
        /// Last time the second data value was updated
        /// </summary>
        public DateTime? DataLastUpdated2 { get; set; }

        /// <summary>
        /// C# format string for data value 1, e.g. "{0:F2} °C"
        /// </summary>
        public string? DataFormat { get; set; }

        /// <summary>
        /// C# format string for data value 2
        /// </summary>
        public string? DataFormat2 { get; set; }

        /// <summary>
        /// Optional font size in pixels for data values
        /// </summary>
        public int? FontSize { get; set; }

        /// <summary>
        /// Color rules for data value 1 (e.g. value &lt; 0 → red)
        /// </summary>
        public List<NodeColorRule> DataColorRules { get; set; } = new();

        /// <summary>
        /// Color rules for data value 2
        /// </summary>
        public List<NodeColorRule> DataColorRules2 { get; set; } = new();

        /// <summary>
        /// Link animation style for links sourced from this node: "None", "Forward", "Reverse"
        /// </summary>
        public string? LinkAnimation { get; set; }
    }
}
