using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Controls.Default;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Positions.Resizing;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using BlazorApp1.Models;
using BlazorApp1.Widgets;
using System.Collections.Concurrent;

namespace BlazorApp1.Services;

public enum ThemeMode { Light, Dark, Auto }

public class ApplicationState
{
    public string DisplayName => GetType().Assembly.GetName().Name ?? "BlazorApp1";
    public int Counter { get; set; } = 0;
    public bool IsInteractive { get; private set; } = false;

    private BlazorDiagram? _diagram;

    // MQTT State
    public SignalRService? SignalRService { get; private set; }
    public List<MqttDataMessage> Messages { get; private set; } = new();
    public HashSet<string> SubscribedTopics { get; private set; } = new();
    public bool IsMqttConnected { get; set; } = false;
    public string MqttConnectionStatus { get; set; } = "Disconnected";

    // MQTT Data Cache
    public MqttDataCache DataCache { get; } = new();

    // Application State Persistence
    private ApplicationStateService? _stateService;

    // Theme & UI preferences
    public ThemeMode ThemeMode { get; private set; } = ThemeMode.Auto;
    public bool ShowDiagramName { get; private set; } = false;
    public string DiagramName { get; private set; } = string.Empty;
    public int GridSize { get; private set; } = 20;

    // Edit mode state (set by Edit page)
    public bool IsEditMode { get; private set; } = false;
    public bool HasSelectedNode { get; private set; } = false;
    public bool HasSingleSelectedNode { get; private set; } = false;

    // Edit mode toggle event — fired by MainLayout, handled by Display page
    public event Action? OnToggleEditModeRequested;
    public void RequestToggleEditMode() => OnToggleEditModeRequested?.Invoke();

    // Menu action events — the Display page subscribes to these when in edit mode
    public event Action? MenuAddNode;
    public event Action? MenuDeleteNode;
    public event Action? MenuCutSelected;
    public event Action? MenuCopySelected;
    public event Action? MenuPasteSelected;
    public event Action<PortAlignment>? MenuAddPort;
    public event Action<PortAlignment>? MenuDeletePort;
    public event Action? MenuEditProperties;
    public event Action? MenuSaveDiagram;
    public event Action? MenuNewDiagram;
    public event Action? MenuReloadDiagram;

    public event Action? OnStateChanged;

    public void SetInteractive() => IsInteractive = true;

    public void SetEditMode(bool editMode)
    {
        IsEditMode = editMode;
        NotifyStateChangedAsync();
    }

    public void UpdateSelectionState(bool hasSelected, bool hasSingleSelected)
    {
        HasSelectedNode = hasSelected;
        HasSingleSelectedNode = hasSingleSelected;
        NotifyStateChangedAsync();
    }

    public void SetTheme(ThemeMode mode)
    {
        ThemeMode = mode;
        NotifyStateChangedAsync();
    }

    public void ToggleShowDiagramName()
    {
        ShowDiagramName = !ShowDiagramName;
        NotifyStateChangedAsync();
    }

    public void SetDiagramName(string name)
    {
        DiagramName = name;
        NotifyStateChangedAsync();
    }

    public void SetGridSize(int size)
    {
        GridSize = size;
        if (_diagram != null)
        {
            _diagram.Options.GridSize = size == 0 ? null : size;
            _diagram.Refresh();
        }
        NotifyStateChangedAsync();
    }

    // Menu trigger methods — called by AppMenu
    public void TriggerAddNode() => MenuAddNode?.Invoke();
    public void TriggerDeleteNode() => MenuDeleteNode?.Invoke();
    public void TriggerCutSelected() => MenuCutSelected?.Invoke();
    public void TriggerCopySelected() => MenuCopySelected?.Invoke();
    public void TriggerPasteSelected() => MenuPasteSelected?.Invoke();
    public void TriggerAddPort(PortAlignment alignment) => MenuAddPort?.Invoke(alignment);
    public void TriggerDeletePort(PortAlignment alignment) => MenuDeletePort?.Invoke(alignment);
    public void TriggerEditProperties() => MenuEditProperties?.Invoke();
    public void TriggerSaveDiagram() => MenuSaveDiagram?.Invoke();
    public void TriggerNewDiagram() => MenuNewDiagram?.Invoke();
    public void TriggerReloadDiagram() => MenuReloadDiagram?.Invoke();

    public BlazorDiagram GetOrCreateDiagram()
    {
        if (_diagram == null)
            _diagram = CreateDiagramFromState(null, false);
        return _diagram;
    }

    public BlazorDiagram CreateDiagramFromState(DiagramState? state, bool readOnly)
    {
        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = !readOnly,
            Zoom = { Enabled = false, },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator()
            }
        };
        if (!readOnly)
        {
            // if grid size is 0 then no grid
            // if grid size is -ve then grid is snaptocenter
            // if grid size is +ve then snap to corner (default)
            if (state == null)
                options.GridSize = 20; // Default grid size for new diagrams
            else
                options.GridSize = state.GridSize == 0 ? null : int.Abs(state.GridSize); // Use saved grid size or default to 20px
            options.GridSnapToCenter = options.GridSize < 0;
        };

        var diagram = new BlazorDiagram(options);
        diagram.RegisterComponent<MudNodeModel, MudNodeWidget>();

        if (state != null)
        {
            // Create nodes from state
            var nodeMap = new Dictionary<string, NodeModel>();
            foreach (var nodeState in state.Nodes)
            {
                var node = new MudNodeModel(position: new Point(nodeState.X, nodeState.Y))
                {
                    Locked = readOnly,
                    Title = nodeState.Title,
                    Size = new Blazor.Diagrams.Core.Geometry.Size(nodeState.Width, nodeState.Height),
                    Icon = nodeState.Icon,
                    IconName = nodeState.IconName,
                    Description = nodeState.Description,
                    BackgroundColor = nodeState.BackgroundColor,
                    IconColor = nodeState.IconColor,
                    Metadata = nodeState.Metadata ?? new Dictionary<string, string>(),
                    DataTopic = nodeState.DataTopic,
                    DataTopic2 = nodeState.DataTopic2,
                    DataFormat = nodeState.DataFormat,
                    DataFormat2 = nodeState.DataFormat2,
                    FontSize = nodeState.FontSize,
                    DataColorRules = nodeState.DataColorRules ?? new(),
                    DataColorRules2 = nodeState.DataColorRules2 ?? new(),
                    LinkAnimation = nodeState.LinkAnimation,
                };

                diagram.Nodes.Add(node);
                nodeMap[nodeState.Id] = node;

                // Add ports
                foreach (var portState in nodeState.Ports)
                {
                    var alignment = Enum.Parse<PortAlignment>(portState.Alignment);
                    AddPortToNode(node, alignment);
                }

                // add resize in bottom left
                if (!readOnly)
                {
                    diagram.Controls.AddFor(node).Add(new ResizeControl(new BottomRightResizerProvider()));
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

                    var link = diagram.Links.Add(new LinkModel(sourceAnchor, targetAnchor));
                    link.Locked = readOnly;
                    CheckForLinkAnimation(sourceNode, link);
                }
            }
        }

        // Update diagram name from state
        if (state != null)
            DiagramName = state.Name;

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
        state.Name = DiagramName;

        // map diagram grid and gridsnaptocenter to diagramstate saved value for grid size
        // if grid size is 0 then no grid
        // if grid size is -ve then grid is snaptocenter
        // if grid size is +ve then snap to corner (default)
        if (_diagram.Options.GridSize == null)
        {
            state.GridSize = 0;
        }
        else
        {
            if (_diagram.Options.GridSnapToCenter)
                state.GridSize = -_diagram.Options.GridSize.Value;
            else
                state.GridSize = _diagram.Options.GridSize.Value;
        }

        // Rebase node positions to current view by baking in the pan offset,
        // then reset pan to zero so the diagram looks identical after save.
        var panX = _diagram.Pan.X;
        var panY = _diagram.Pan.Y;

        // Save nodes
        foreach (var node in _diagram.Nodes.OfType<MudNodeModel>())
        {
            var nodeState = new NodeState
            {
                Id = node.Id,
                Title = node.Title ?? string.Empty,
                X = (node.Position?.X ?? 0) + panX,
                Y = (node.Position?.Y ?? 0) + panY,
                Width = node.Size?.Width ?? 120,
                Height = node.Size?.Height ?? 90,
                Icon = node.Icon,
                IconName = node.IconName,
                Description = node.Description,
                BackgroundColor = node.BackgroundColor,
                IconColor = node.IconColor,
                Metadata = node.Metadata ?? new Dictionary<string, string>(),
                DataTopic = node.DataTopic,
                DataTopic2 = node.DataTopic2,
                DataFormat = node.DataFormat,
                DataFormat2 = node.DataFormat2,
                FontSize = node.FontSize,
                DataColorRules = node.DataColorRules,
                DataColorRules2 = node.DataColorRules2,
                LinkAnimation = node.LinkAnimation,
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

        // Reset pan to zero — positions are now in view space
        if (panX != 0 || panY != 0)
            _diagram.SetPan(0, 0);

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

    public void SetApplicationStateService(ApplicationStateService service)
    {
        _stateService = service;
    }

    public async Task LoadSubscriptionsAsync()
    {
        if (_stateService == null) return;

        var state = await _stateService.LoadStateAsync();
        if (state != null && state.MqttSubscriptions.Any())
        {
            SubscribedTopics = new HashSet<string>(state.MqttSubscriptions);
            NotifyStateChangedAsync();
        }
    }

    private async Task SaveSubscriptionsAsync()
    {
        if (_stateService == null) return;

        var state = new ApplicationStateData
        {
            MqttSubscriptions = SubscribedTopics
        };

        await _stateService.SaveStateAsync(state);
    }

    public void AddMessage(MqttDataMessage message)
    {
        lock (Messages)
        {
            Messages.Add(message);
            while (Messages.Count > 100)
            {
                Messages.RemoveAt(0);
            }
        }

        // Update the data cache
        DataCache.UpdateValue(message.Topic, message.Payload);

        NotifyStateChangedAsync();
    }

    public List<MqttDataMessage> RecentMessages(int n)
    {
        lock (Messages)
        {
            return Messages.TakeLast(n).ToList();
        }
    }
    public async Task AddSubscriptionAsync(string topic)
    {
        SubscribedTopics.Add(topic);
        await SaveSubscriptionsAsync();
        NotifyStateChangedAsync();
    }

    public async Task RemoveSubscriptionAsync(string topic)
    {
        SubscribedTopics.Remove(topic);
        await SaveSubscriptionsAsync();
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

    internal async Task AddTopicToDiagram(string topicPath, string nodeName)
    {
        var diagram = GetOrCreateDiagram();

        var node = new BlazorApp1.Models.MudNodeModel(
            new Blazor.Diagrams.Core.Geometry.Point(100 + diagram.Nodes.Count * 20, 100))
        {
            Title = nodeName,
            DataTopic = topicPath,
        };
        // try be clever with formatting...
        string? format = null;
        switch (nodeName.ToLower())
        {
            case "soc":     format = "{0:0}%"; break;
            case "power":
            case "solar":
            case "pv":
            case "load":
            case "grid":    format = "{0:0}W"; break;
        }
        if (format != null)
        {
            node.DataFormat = format;
        }

        diagram.Nodes.Add(node);
        diagram.Controls.AddFor(node).Add(new ResizeControl(new BottomRightResizerProvider()));
    }

    public void CheckForLinkAnimation(NodeModel sourceNode, LinkModel link)
    {
        if (sourceNode is MudNodeModel mudSource &&
            !string.IsNullOrEmpty(mudSource.LinkAnimation) &&
            mudSource.LinkAnimation != "None")
        {
            link.DashPattern = "5,5";
        }
    }

    internal void AddPortToNode(NodeModel node, PortAlignment alignment)
    {
        if (node != null)
        {
            //node.AddPort(alignment);
            node.AddPort(new MudPortModel(node, alignment));
        }
    }


}
