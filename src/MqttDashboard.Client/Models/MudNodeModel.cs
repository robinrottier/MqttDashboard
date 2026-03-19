using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models
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
        /// Format string for the body text. Use {0} for first data value, {1} for second, etc.
        /// Supports C# format specifiers e.g. "Temp: {0:F2}°C\nHumidity: {1:F1}%"
        /// </summary>
        public string? Text { get; set; }

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

        // Runtime-only: populated by MQTT watchers, not serialized
        public object? DataValue { get; set; }
        public object? DataValue2 { get; set; }
        public DateTime? DataLastUpdated { get; set; }
        public DateTime? DataLastUpdated2 { get; set; }

        /// <summary>
        /// Optional font size in pixels for data values
        /// </summary>
        public int? FontSize { get; set; }

        /// <summary>
        /// Link animation style for links sourced from this node: "None", "Forward", "Reverse"
        /// </summary>
        public string? LinkAnimation { get; set; }

        /// <summary>Node type discriminator. Defaults to "Text" (existing text/display node).</summary>
        public string NodeType { get; set; } = "Text";
    }
}
