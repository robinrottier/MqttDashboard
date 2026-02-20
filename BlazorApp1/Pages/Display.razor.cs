using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using BlazorApp1.Models;
using BlazorApp1.Services;
using BlazorApp1.Widgets;
using Microsoft.AspNetCore.Components;

namespace BlazorApp1.Pages;

public partial class Display : IDisposable
{
    [Inject] private ApplicationState AppState { get; set; } = default!;
    [Inject] private DiagramService DiagramService { get; set; } = default!;

    private BlazorDiagram? _diagram;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();

            var savedState = await DiagramService.LoadDiagramAsync();
            if (savedState != null && savedState.Nodes.Count > 0)
            {
                _diagram = BuildReadOnlyDiagram(savedState);

                StateHasChanged();

                await Task.Delay(100);

                foreach (var node in _diagram.Nodes)
                    node.Refresh();
                foreach (var link in _diagram.Links)
                    link.Refresh();
                _diagram.Refresh();

                StateHasChanged();
            }
            else
            {
                _diagram = BuildEmptyReadOnlyDiagram();
                StateHasChanged();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private BlazorDiagram BuildReadOnlyDiagram(DiagramState state)
    {
        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = false,
            Zoom = { Enabled = false },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator()
            },
        };

        var diagram = new BlazorDiagram(options);
        diagram.RegisterComponent<MudNodeModel, MudNodeWidget>();

        var nodeMap = new Dictionary<string, NodeModel>();
        foreach (var nodeState in state.Nodes)
        {
            var node = new MudNodeModel(position: new Point(nodeState.X, nodeState.Y))
            {
                Title = nodeState.Title,
                Size = new Blazor.Diagrams.Core.Geometry.Size(nodeState.Width, nodeState.Height),
                Icon = nodeState.Icon,
                IconName = nodeState.IconName,
                Description = nodeState.Description,
                BackgroundColor = nodeState.BackgroundColor,
                IconColor = nodeState.IconColor,
                Metadata = nodeState.Metadata ?? new Dictionary<string, string>(),
                DataTopic = nodeState.DataTopic,
                Locked = true,
            };

            foreach (var portState in nodeState.Ports)
            {
                var alignment = Enum.Parse<PortAlignment>(portState.Alignment);
                node.AddPort(alignment);
            }

            diagram.Nodes.Add(node);
            nodeMap[nodeState.Id] = node;
        }

        foreach (var linkState in state.Links)
        {
            if (nodeMap.TryGetValue(linkState.SourceNodeId, out var sourceNode) &&
                nodeMap.TryGetValue(linkState.TargetNodeId, out var targetNode))
            {
                PortModel? sourcePort = null;
                PortModel? targetPort = null;

                if (!string.IsNullOrEmpty(linkState.SourcePortAlignment))
                {
                    var sourceAlignment = Enum.Parse<PortAlignment>(linkState.SourcePortAlignment);
                    sourcePort = sourceNode.Ports.FirstOrDefault(p => p.Alignment == sourceAlignment);
                }

                if (!string.IsNullOrEmpty(linkState.TargetPortAlignment))
                {
                    var targetAlignment = Enum.Parse<PortAlignment>(linkState.TargetPortAlignment);
                    targetPort = targetNode.Ports.FirstOrDefault(p => p.Alignment == targetAlignment);
                }

                Anchor sourceAnchor = sourcePort != null
                    ? new SinglePortAnchor(sourcePort)
                    : new ShapeIntersectionAnchor(sourceNode);

                Anchor targetAnchor = targetPort != null
                    ? new SinglePortAnchor(targetPort)
                    : new ShapeIntersectionAnchor(targetNode);

                diagram.Links.Add(new LinkModel(sourceAnchor, targetAnchor));
            }
        }

        return diagram;
    }

    private BlazorDiagram BuildEmptyReadOnlyDiagram()
    {
        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = false,
            Zoom = { Enabled = false },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator()
            },
        };

        var diagram = new BlazorDiagram(options);
        diagram.RegisterComponent<MudNodeModel, MudNodeWidget>();
        return diagram;
    }

    public void Dispose() { }
}
