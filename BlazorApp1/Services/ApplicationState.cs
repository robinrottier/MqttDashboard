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

public class ApplicationState
{
    public int Counter { get; set; } = 0;
    public bool IsInteractive { get; private set; } = false;

    private BlazorDiagram? _diagram;

    // MQTT State
    public SignalRService? SignalRService { get; private set; }
    public List<MqttDataMessage> Messages { get; private set; } = new();
    public HashSet<string> SubscribedTopics { get; private set; } = new();
    public bool IsMqttConnected { get; set; } = false;
    public string MqttConnectionStatus { get; set; } = "Disconnected";

    public event Action? OnStateChanged;

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

        // Start with empty diagram - will be loaded from server or user will create nodes
        _diagram = diagram;
        return _diagram;
    }

    public BlazorDiagram CreateDiagramFromState(DiagramState state)
    {
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

        // Create nodes from state
        var nodeMap = new Dictionary<string, NodeModel>();
        foreach (var nodeState in state.Nodes)
        {
            var node = new MudNodeModel(position: new Point(nodeState.X, nodeState.Y))
            {
                Title = nodeState.Title
            };

            diagram.Nodes.Add(node);
            nodeMap[nodeState.Id] = node;

            // Add ports
            foreach (var portState in nodeState.Ports)
            {
                var alignment = Enum.Parse<PortAlignment>(portState.Alignment);
                node.AddPort(alignment);
            }
        }

        // Create links from state
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

        _diagram = diagram;
        return _diagram;
    }

    public DiagramState GetDiagramState()
    {
        if (_diagram == null)
        {
            return new DiagramState();
        }

        var state = new DiagramState();

        // Save nodes
        foreach (var node in _diagram.Nodes.OfType<MudNodeModel>())
        {
            var nodeState = new NodeState
            {
                Id = node.Id,
                Title = node.Title ?? string.Empty,
                X = node.Position?.X ?? 0,
                Y = node.Position?.Y ?? 0
            };

            // Save ports
            foreach (var port in node.Ports)
            {
                nodeState.Ports.Add(new PortState
                {
                    Id = port.Id,
                    Alignment = port.Alignment.ToString()
                });
            }

            state.Nodes.Add(nodeState);
        }

        // Save links
        foreach (var link in _diagram.Links)
        {
            var linkState = new LinkState();

            // Get source node and port
            if (link.Source.Model is PortModel sourcePort)
            {
                linkState.SourceNodeId = sourcePort.Parent.Id;
                linkState.SourcePortAlignment = sourcePort.Alignment.ToString();
            }
            else if (link.Source.Model is NodeModel sourceNode)
            {
                linkState.SourceNodeId = sourceNode.Id;
            }

            // Get target node and port
            if (link.Target.Model is PortModel targetPort)
            {
                linkState.TargetNodeId = targetPort.Parent.Id;
                linkState.TargetPortAlignment = targetPort.Alignment.ToString();
            }
            else if (link.Target.Model is NodeModel targetNode)
            {
                linkState.TargetNodeId = targetNode.Id;
            }

            if (!string.IsNullOrEmpty(linkState.SourceNodeId) && !string.IsNullOrEmpty(linkState.TargetNodeId))
            {
                state.Links.Add(linkState);
            }
        }

        return state;
    }

    public void ResetDiagram()
    {
        _diagram = null;
    }

    // MQTT Methods
    public void SetSignalRService(SignalRService service)
    {
        SignalRService = service;
    }

    public void AddMessage(MqttDataMessage message)
    {
        Messages.Add(message);
        if (Messages.Count > 100)
        {
            Messages.RemoveAt(0);
        }
        NotifyStateChangedAsync();
    }

    public void AddSubscription(string topic)
    {
        SubscribedTopics.Add(topic);
        NotifyStateChangedAsync();
    }

    public void RemoveSubscription(string topic)
    {
        SubscribedTopics.Remove(topic);
        NotifyStateChangedAsync();
    }

    public void SetMqttConnectionStatus(string status, bool connected)
    {
        MqttConnectionStatus = status;
        IsMqttConnected = connected;
        NotifyStateChangedAsync();
    }

    public void ClearMessages()
    {
        Messages.Clear();
        NotifyStateChangedAsync();
    }

    private void NotifyStateChangedAsync()
    {
        // Invoke asynchronously to avoid thread issues
        _ = Task.Run(() => OnStateChanged?.Invoke());
    }
}
