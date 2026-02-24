using Blazor.Diagrams;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using BlazorApp1.Models;
using BlazorApp1.Services;
using BlazorApp1.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorApp1.Pages;

public partial class Edit : IDisposable
{
    [Inject] private ApplicationState AppState { get; set; } = default!;
    [Inject] private DiagramService DiagramService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private BlazorDiagram? _diagram;
    private int _nodeCounter = 1;
    private bool _hasSelectedNode;
    private bool _hasSingleSelectedNode;

    // Stored handler references so we can unsubscribe cleanly
    private Action? _onMenuSaveDiagram;
    private Action? _onMenuReloadDiagram;
    private Action? _onMenuEditProperties;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();
            AppState.SetEditMode(true);

            // Check if diagram already exists in memory (user navigated back)
            _diagram = AppState.GetOrCreateDiagram();

            if (_diagram.Nodes.Count == 0)
            {
                var savedState = await DiagramService.LoadDiagramAsync();
                if (savedState != null && savedState.Nodes.Count > 0)
                {
                    AppState.ResetDiagram();
                    _diagram = AppState.CreateDiagramFromState(savedState, false);

                    // Sync grid size into AppState
                    var gs = _diagram.Options.GridSize;
                    AppState.SetGridSize(gs.HasValue ? (int)gs.Value : 0);

                    Snackbar.Add("Diagram loaded from server", Severity.Info);
                    StateHasChanged();

                    await Task.Delay(100);
                    foreach (var n in _diagram.Nodes) n.Refresh();
                    foreach (var l in _diagram.Links) l.Refresh();
                    _diagram.Refresh();
                    StateHasChanged();
                }
            }
            else
            {
                // Sync grid size from existing diagram
                var gs = _diagram.Options.GridSize;
                AppState.SetGridSize(gs.HasValue ? (int)gs.Value : 0);
            }

            // Subscribe to diagram events
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;

            // Subscribe to AppState events
            AppState.OnStateChanged += OnAppStateChanged;
            AppState.MenuAddNode      += AddNode;
            AppState.MenuDeleteNode   += DeleteSelectedNode;
            AppState.MenuCutSelected  += CutSelectedNodes;
            AppState.MenuCopySelected += CopySelectedNodes;
            AppState.MenuPasteSelected += PasteNodes;
            AppState.MenuAddPort      += AddPortToSelectedNode;
            AppState.MenuDeletePort   += DeletePortFromSelectedNode;

            _onMenuSaveDiagram     = () => InvokeAsync(SaveDiagram);
            _onMenuReloadDiagram   = () => InvokeAsync(ReloadDiagram);
            _onMenuEditProperties  = () => InvokeAsync(EditNodeProperties);

            AppState.MenuSaveDiagram    += _onMenuSaveDiagram;
            AppState.MenuReloadDiagram  += _onMenuReloadDiagram;
            AppState.MenuEditProperties += _onMenuEditProperties;
            AppState.MenuNewDiagram     += NewDiagram;

            UpdateSelectionState();
            StateHasChanged();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private void OnSelectionChanged(object model)
    {
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged() => InvokeAsync(StateHasChanged);

    private void OnAppStateChanged() => InvokeAsync(StateHasChanged);

    private void UpdateSelectionState()
    {
        var selected = _diagram?.GetSelectedModels().OfType<NodeModel>().ToList() ?? [];
        _hasSelectedNode       = selected.Count > 0;
        _hasSingleSelectedNode = selected.Count == 1;
        AppState.UpdateSelectionState(_hasSelectedNode, _hasSingleSelectedNode);
    }

    // ── Node operations ──────────────────────────────────────────────────────

    private void AddNode()
    {
        if (_diagram == null) return;
        var rng = new Random();
        _diagram.UnselectAll();
        var node = _diagram.Nodes.Add(new MudNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))
        {
            Title = $"Node {_nodeCounter++}"
        });
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
            _diagram = AppState.CreateDiagramFromState(savedState, false);
            var gs = _diagram.Options.GridSize;
            AppState.SetGridSize(gs.HasValue ? (int)gs.Value : 0);
            Snackbar.Add($"Diagram reloaded ({savedState.Nodes.Count} nodes)", Severity.Info);
        }
        else
        {
            _diagram = AppState.GetOrCreateDiagram();
            Snackbar.Add("No saved diagram found", Severity.Warning);
        }
        _diagram.SelectionChanged += OnSelectionChanged;
        _diagram.Changed += OnDiagramChanged;
        UpdateSelectionState();
        StateHasChanged();
    }

    private void CutSelectedNodes()  { /* TODO: clipboard */ }
    private void CopySelectedNodes() { /* TODO: clipboard */ }
    private void PasteNodes()        { /* TODO: clipboard */ }

    // ── Port operations ──────────────────────────────────────────────────────

    private bool HasPortAlignment(PortAlignment alignment) =>
        _diagram?.GetSelectedModels().OfType<NodeModel>().FirstOrDefault()
                ?.Ports.Any(p => p.Alignment == alignment) ?? false;

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

    // ── Save ─────────────────────────────────────────────────────────────────

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

    // ── Properties ───────────────────────────────────────────────────────────

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
        Snackbar.Clear();
        AppState.SetEditMode(false);
        AppState.UpdateSelectionState(false, false);

        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }

        AppState.OnStateChanged    -= OnAppStateChanged;
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
    }
}
