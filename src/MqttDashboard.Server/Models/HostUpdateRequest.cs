namespace MqttDashboard.Server.Models;

public class HostUpdateRequest
{
    public string? Service { get; set; }
    public string? ComposeFile { get; set; }
    public string? WatchtowerContainer { get; set; }
    public string? Workdir { get; set; }
}
