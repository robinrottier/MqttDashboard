using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models;

public class ImageNodeModel : MudNodeModel
{
    public ImageNodeModel(Point? position = null) : base(position)
    {
        NodeType = "Image";
        Size = new Size(150, 150);
        MinimumDimensions = new Size(60, 60);
    }

    public string StaticImageUrl { get; set; } = string.Empty;
    public string ObjectFit { get; set; } = "contain"; // contain, cover, fill, scale-down
    public bool ShowTitle { get; set; } = true;
}
