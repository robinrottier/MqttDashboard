using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using BlazorApp1.Models;
using BlazorApp1.Widgets;

namespace BlazorApp1.Services;

public class DiagramStateService
{
    public int Counter { get; set; } = 0;
    public bool IsInteractive { get; private set; } = false;

    private BlazorDiagram? _diagram;

    public void SetInteractive()
    {
        IsInteractive = true;
    }

    public BlazorDiagram GetOrCreateDiagram()
    {
        if (_diagram != null)
        {
            return _diagram;
        }

        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = true,
            Zoom =
            {
                Enabled = false,
            },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator()
            },
        };

        var diagram = new BlazorDiagram(options);
        diagram.RegisterComponent<MudNodeModel, MudNodeWidget>();

        var firstNode = diagram.Nodes.Add(new MudNodeModel(position: new Point(50, 50))
        {
            Title = "Node 1"
        });
        var secondNode = diagram.Nodes.Add(new MudNodeModel(position: new Point(200, 100))
        {
            Title = "Node 2"
        });
        var leftPort = secondNode.AddPort(PortAlignment.Left);
        var rightPort = secondNode.AddPort(PortAlignment.Right);

        var sourceAnchor = new ShapeIntersectionAnchor(firstNode);
        var targetAnchor = new SinglePortAnchor(leftPort);
        var link = diagram.Links.Add(new LinkModel(sourceAnchor, targetAnchor));

        _diagram = diagram;
        return _diagram;
    }

    public void ResetDiagram()
    {
        _diagram = null;
    }
}
