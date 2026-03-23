namespace MqttDashboard.Models;

/// <summary>
/// Groups the colour-transition settings that can be applied to any node that shows
/// a colour derived from a live data value: a list of threshold rules and the index
/// of the data topic whose value is tested against those rules.
/// </summary>
public class ColorTransition
{
    /// <summary>
    /// 0-based index into the node's DataTopics list whose value is used when
    /// evaluating all threshold rules. 0 = first topic (DataValue).
    /// </summary>
    public int ColorTopicIndex { get; set; } = 0;

    /// <summary>
    /// Ordered list of threshold rules. First matching rule wins.
    /// </summary>
    public List<GaugeColorThreshold> ColorThresholds { get; set; } = new();
}

/// <summary>A single colour-threshold rule: apply <see cref="Color"/> when the data value
/// satisfies the <see cref="Direction"/> comparison against <see cref="Value"/>.</summary>
public class GaugeColorThreshold
{
    public double Value { get; set; }
    public string Color { get; set; } = "var(--mud-palette-primary)";
    /// <summary>Comparison direction: ">=" or "&lt;=".</summary>
    public string Direction { get; set; } = ">=";
}
