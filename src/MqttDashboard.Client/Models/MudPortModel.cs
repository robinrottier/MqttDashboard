using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models
{
    public class NodePortModel : PortModel
    {
        public NodePortModel(NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(parent, alignment, position, size)
        {
            Init();
        }

        public NodePortModel(string id, NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(id, parent, alignment, position, size)
        {
            Init();
        }

        void Init()
        {
            Size = new Size(10,10);
        }
    }

    // Backward-compat alias — use NodePortModel in new code
    [Obsolete("Use NodePortModel")]
    public class MudPortModel : NodePortModel
    {
        public MudPortModel(NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(parent, alignment, position, size) { }
        public MudPortModel(string id, NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(id, parent, alignment, position, size) { }
    }
}
