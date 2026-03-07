using Blazor.Diagrams;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using MqttDashboard.Models;
using MqttDashboard.Services;
using MqttDashboard.Widgets;
using MqttDashboard.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MqttDashboard.Pages;

public partial class Display : IDisposable
{
    [Inject] private ApplicationState AppState { get; set; } = default!;
    [Inject] private DiagramService DiagramService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private BlazorDiagram? _diagram;
    private int _nodeCounter = 1;
    private bool _hasSelectedNode;
    private bool _hasSingleSelectedNode;

    // Stored handler references for clean unsubscription
    private Action? _onMenuSaveDiagram;
    private Action? _onMenuReloadDiagram;
    private Action? _onMenuEditProperties;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();

            // Subscribe to toggle event from the layout
            AppState.OnToggleEditModeRequested += OnToggleEditModeRequested;
            AppState.OnStateChanged += OnAppStateChanged;

            var savedState = await DiagramService.LoadDiagramAsync();
            if (savedState != null && savedState.Nodes.Count > 0)
            {
                _diagram = AppState.CreateDiagramFromState(savedState, readOnly: true);

                StateHasChanged();
                await Task.Delay(100);
                foreach (var node in _diagram.Nodes) node.Refresh();
                foreach (var link in _diagram.Links) link.Refresh();
                _diagram.Refresh();
                StateHasChanged();
            }
            else
            {
                _diagram = AppState.CreateDiagramFromState(null, readOnly: true);
                StateHasChanged();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private void OnAppStateChanged() => InvokeAsync(StateHasChanged);

    // ── Mode switching ────────────────────────────────────────────────────────

    private void OnToggleEditModeRequested()
    {
        InvokeAsync(async () => await SwitchMode(!AppState.IsEditMode));
    }

    private async Task SwitchMode(bool enterEditMode)
    {
        if (_diagram == null) return;

        if (AppState.IsEditMode)
            UnsubscribeEditEvents();

        if (AppState.IsEditMode && !enterEditMode)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }

        // Mutate existing nodes in-place rather than rebuilding the diagram
        foreach (var node in _diagram.Nodes)
        {
            node.Locked = !enterEditMode;
            if (enterEditMode)
                _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
            else
                _diagram.Controls.RemoveFor(node);
        }

        foreach (var link in _diagram.Links)
            link.Locked = !enterEditMode;

        if (enterEditMode)
        {
            // Enable grid if not already set
            if (_diagram.Options.GridSize == null)
                _diagram.Options.GridSize = AppState.GridSize > 0 ? AppState.GridSize : 10;
            AppState.SetGridSize(_diagram.Options.GridSize.HasValue ? (int)_diagram.Options.GridSize.Value : 10);

            _diagram.Options.AllowMultiSelection = true;
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            SubscribeEditEvents();
            UpdateSelectionState();
        }
        else
        {
            _diagram.Options.AllowMultiSelection = false;
            _diagram.UnselectAll();
        }

        AppState.SetEditMode(enterEditMode);
        StateHasChanged();

        await Task.Delay(50);
        foreach (var node in _diagram.Nodes) node.Refresh();
        foreach (var link in _diagram.Links) link.Refresh();
        _diagram.Refresh();
        StateHasChanged();
    }

    private void SubscribeEditEvents()
    {
        _diagram!.Links.Added += OnLinkAdded;
        AppState.MenuAddNode       += AddNode;
        AppState.MenuDeleteNode    += DeleteSelectedNode;
        AppState.MenuCutSelected   += CutSelectedNodes;
        AppState.MenuCopySelected  += CopySelectedNodes;
        AppState.MenuPasteSelected += PasteNodes;
        AppState.MenuAddPort       += AddPortToSelectedNode;
        AppState.MenuDeletePort    += DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    += NewDiagram;

        _onMenuSaveDiagram    = () => InvokeAsync(SaveDiagram);
        _onMenuReloadDiagram  = () => InvokeAsync(ReloadDiagram);
        _onMenuEditProperties = () => InvokeAsync(EditNodeProperties);

        AppState.MenuSaveDiagram    += _onMenuSaveDiagram;
        AppState.MenuReloadDiagram  += _onMenuReloadDiagram;
        AppState.MenuEditProperties += _onMenuEditProperties;
    }

    private void UnsubscribeEditEvents()
    {
        if (_diagram != null)
            _diagram.Links.Added -= OnLinkAdded;
        AppState.MenuAddNode       -= AddNode;
        AppState.MenuDeleteNode    -= DeleteSelectedNode;
        AppState.MenuCutSelected   -= CutSelectedNodes;
        AppState.MenuCopySelected  -= CopySelectedNodes;
        AppState.MenuPasteSelected -= PasteNodes;
        AppState.MenuAddPort       -= AddPortToSelectedNode;
        AppState.MenuDeletePort    -= DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    -= NewDiagram;

        if (_onMenuSaveDiagram    != null) AppState.MenuSaveDiagram    -= _onMenuSaveDiagram;
        if (_onMenuReloadDiagram  != null) AppState.MenuReloadDiagram  -= _onMenuReloadDiagram;
        if (_onMenuEditProperties != null) AppState.MenuEditProperties -= _onMenuEditProperties;

        _onMenuSaveDiagram    = null;
        _onMenuReloadDiagram  = null;
        _onMenuEditProperties = null;
    }

    // ── Diagram event handlers ────────────────────────────────────────────────

    private void OnSelectionChanged(object model)
    {
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged() => InvokeAsync(StateHasChanged);

    private void OnLinkAdded(Blazor.Diagrams.Core.Models.Base.BaseLinkModel link)
    {
        if (link is not LinkModel lm) return;
        var sourceNode = (link.Source.Model is PortModel port ? port.Parent : link.Source.Model) as NodeModel;
        if (sourceNode != null)
            AppState.CheckForLinkAnimation(sourceNode, lm);
    }

    private void UpdateSelectionState()
    {
        var selected = _diagram?.GetSelectedModels().OfType<NodeModel>().ToList() ?? [];
        _hasSelectedNode       = selected.Count > 0;
        _hasSingleSelectedNode = selected.Count == 1;
        AppState.UpdateSelectionState(_hasSelectedNode, _hasSingleSelectedNode);
    }

    // ── Node operations ───────────────────────────────────────────────────────

    private void AddNode()
    {
        if (_diagram == null) return;
        var rng = new Random();
        _diagram.UnselectAll();
        var node = _diagram.Nodes.Add(new MudNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))
        {
            Title = $"Node {_nodeCounter++}"
        });
        _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
        _diagram.SelectModel(node, false);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void DeleteSelectedNode()
    {
        if (_diagram == null) return;
        foreach (var n in _diagram.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void NewDiagram()
    {
        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();
        _diagram = AppState.GetOrCreateDiagram();
        _diagram.SelectionChanged += OnSelectionChanged;
        _diagram.Changed += OnDiagramChanged;
        _nodeCounter = 1;
        UpdateSelectionState();
        Snackbar.Add("New diagram created", Severity.Info);
        StateHasChanged();
    }

    private async Task ReloadDiagram()
    {
        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();
        var savedState = await DiagramService.LoadDiagramAsync();
        if (savedState != null && savedState.Nodes.Count > 0)
        {
            _diagram = AppState.CreateDiagramFromState(savedState, readOnly: !AppState.IsEditMode);
            var gs = _diagram.Options.GridSize;
            if (AppState.IsEditMode)
                AppState.SetGridSize(gs.HasValue ? (int)gs.Value : 10);
            Snackbar.Add($"Diagram reloaded ({savedState.Nodes.Count} nodes)", Severity.Info);
        }
        else
        {
            _diagram = AppState.GetOrCreateDiagram();
            Snackbar.Add("No saved diagram found", Severity.Warning);
        }
        if (AppState.IsEditMode)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            UpdateSelectionState();
        }
        StateHasChanged();
    }

    private void CutSelectedNodes()  { /* TODO: clipboard */ }
    private void CopySelectedNodes() { /* TODO: clipboard */ }
    private void PasteNodes()        { /* TODO: clipboard */ }

    // ── Port operations ───────────────────────────────────────────────────────

    private void AddPortToSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<NodeModel>().FirstOrDefault();
        if (node != null && !node.Ports.Any(p => p.Alignment == alignment))
        {
            AppState.AddPortToNode(node, alignment);
            node.Refresh();
            StateHasChanged();
        }
    }

    private void DeletePortFromSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<NodeModel>().FirstOrDefault();
        var port = node?.Ports.FirstOrDefault(p => p.Alignment == alignment);
        if (port != null)
        {
            node!.RemovePort(port);
            node.Refresh();
            StateHasChanged();
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private async Task SaveDiagram()
    {
        try
        {
            var state = AppState.GetDiagramState();
            var success = await DiagramService.SaveDiagramAsync(state);
            Snackbar.Add(success
                ? $"Diagram saved ({state.Nodes.Count} nodes, {state.Links.Count} links)"
                : "Failed to save diagram",
                success ? Severity.Success : Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving diagram: {ex.Message}", Severity.Error);
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    private async Task EditNodeProperties()
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<MudNodeModel>().FirstOrDefault();
        if (node == null) { Snackbar.Add("No node selected", Severity.Warning); return; }

        var parameters = new DialogParameters { { "Node", node } };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            BackdropClick = true
        };
        var dialog = await DialogService.ShowAsync<NodePropertyEditor>("Edit Node Properties", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            StateHasChanged();
            Snackbar.Add("Node properties updated", Severity.Success);
        }
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        AppState.OnToggleEditModeRequested -= OnToggleEditModeRequested;
        AppState.OnStateChanged -= OnAppStateChanged;

        if (AppState.IsEditMode)
        {
            Snackbar.Clear();
            AppState.SetEditMode(false);
            AppState.UpdateSelectionState(false, false);
            UnsubscribeEditEvents();
        }

        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
    }
}
