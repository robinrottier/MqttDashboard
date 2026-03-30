namespace MqttDashboard;

/// <summary>
/// Custom SVG icon strings for use with MudBlazor's Icon parameter.
/// Each string is the inner SVG content rendered inside
/// &lt;svg viewBox="0 0 24 24"&gt; by MudIcon/MudIconButton.
/// Use "currentColor" so the Color parameter is respected.
/// The geometry matches mqttdashboard-icon.svg in wwwroot.
/// </summary>
public static class AppIcons
{
    public const string MqttDashboard =
        """
        <rect x="4"    y="4"  width="4" height="4" fill="currentColor" />
        <rect x="16"   y="4"  width="4" height="4" fill="currentColor" />
        <rect x="16"   y="12" width="4" height="4" fill="currentColor" />
        <line x1="8"   y1="6"  x2="16"   y2="6"  stroke="currentColor" stroke-width="1" />
        <line x1="12"  y1="6"  x2="12"   y2="14" stroke="currentColor" stroke-width="1" />
        <line x1="11.5" y1="14" x2="16"  y2="14" stroke="currentColor" stroke-width="1" />
        """;
}
