
namespace MqttDashboard.Models
{
    public class MudNodeModel(Blazor.Diagrams.Core.Geometry.Point? position = null) : Blazor.Diagrams.Core.Models.NodeModel(position)
    {
        /// <summary>
        /// Position of the title relative to the main content: "Above", "Below", "Left", "Right". Defaults to "Above".
        /// (Title is inherited from base blazor diagram node model)
        /// </summary>
        public string TitlePosition { get; set; } = "Above";

        /// <summary>
        /// Icon name from MudBlazor Icons (e.g., Icons.Material.Filled.Home)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Human-readable icon name for display
        /// </summary>
        public string? IconName { get; set; }

        /// <summary>
        /// Icon color
        /// </summary>
        public string? IconColor { get; set; }

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
        /// Static background image URL (http/https/data URI).
        /// Displayed as the node background behind all content.
        /// </summary>
        public string? BackgroundImageUrl { get; set; }

        /// <summary>
        /// How the background image fills the node container.
        /// CSS background-size value: "cover", "contain", or "100% 100%" (fill/stretch).
        /// Defaults to "cover".
        /// </summary>
        public string BackgroundObjectFit { get; set; } = "cover";

        /// <summary>
        /// When true, the first MQTT data value is used as the background image URL
        /// instead of <see cref="BackgroundImageUrl"/>.
        /// </summary>


        /// <summary>
        /// Custom metadata dictionary for future extensibility
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// List of MQTT topics for data binding
        /// </summary>
        public List<string> DataTopics { get; set; } = new();

        // Computed convenience accessors — these are read-only; set via DataTopics list.
        public string? DataTopic  => DataTopics.Count > 0 ? DataTopics[0] : null;
        public string? DataTopic2 => DataTopics.Count > 1 ? DataTopics[1] : null;

        // Runtime-only arrays: populated by MQTT watchers, never serialised.
        // Length is set by BaseNodeWithDataWidget to match DataTopics.Count.
        public object?[]   DataValues      { get; set; } = Array.Empty<object?>();
        public DateTime?[] DataUpdatedTimes { get; set; } = Array.Empty<DateTime?>();

        // Convenience compat getters for the first two slots.
        public object?   DataValue        => DataValues.Length       > 0 ? DataValues[0]       : null;
        public object?   DataValue2       => DataValues.Length       > 1 ? DataValues[1]       : null;
        public DateTime? DataLastUpdated  => DataUpdatedTimes.Length > 0 ? DataUpdatedTimes[0] : null;
        public DateTime? DataLastUpdated2 => DataUpdatedTimes.Length > 1 ? DataUpdatedTimes[1] : null;

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
