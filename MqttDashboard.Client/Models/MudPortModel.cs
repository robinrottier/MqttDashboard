using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models
{
    public class MudPortModel : PortModel
    {
        public MudPortModel(NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(parent, alignment, position, size)
        {
            Init();
        }

        public MudPortModel(string id, NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(id, parent, alignment, position, size)
        {
            Init();
        }

        void Init()
        {
            Size = new Size(10,10);
        }
    }
}
