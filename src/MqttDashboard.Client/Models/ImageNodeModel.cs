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

    [NpText("Static Image URL", Category = "Image", Order = 1,
        HelperText = "Used when no data topic. Supports http/https/data URLs")]
    public string StaticImageUrl { get; set; } = string.Empty;

    [NpSelect("Object Fit", "contain", "cover", "fill", "scale-down",
        Category = "Image", Order = 2,
        Labels = ["Contain (fit inside)", "Cover (fill, crop edges)", "Fill (stretch)", "Scale Down"])]
    public string ObjectFit { get; set; } = "contain";

    [NpCheckbox("Show Title", Category = "Image", Order = 3)]
    public bool ShowTitle { get; set; } = true;
}
