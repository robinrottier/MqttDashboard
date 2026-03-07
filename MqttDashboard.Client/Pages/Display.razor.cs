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

    // Stored handler references for clean unsubscription
    private Action? _onMenuSaveDiagram;
    private Action? _onMenuReloadDiagram;
    private Action? _onMenuEditProperties;
    private Action? _onMenuSaveAs;
    private Action? _onMenuOpen;
    private Action? _onMenuUndo;
    private Action? _onMenuRedo;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();
            AppState.OnToggleEditModeRequested += OnToggleEditModeRequested;
            AppState.OnStateChanged += OnAppStateChanged;

            var savedState = await DiagramService.LoadDiagramAsync();
            if (savedState != null && savedState.Nodes.Count > 0)
            {
                _diagram = AppState.CreateDiagramFromState(savedState, readOnly: true);
                StateHasChanged();
                await Task.Delay(100);
                RefreshAll();
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

    private void RefreshAll()
    {
        if (_diagram == null) return;
        foreach (var node in _diagram.Nodes) node.Refresh();
        foreach (var link in _diagram.Links) link.Refresh();
        _diagram.Refresh();
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
        RefreshAll();
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
        _onMenuSaveAs         = () => InvokeAsync(SaveAsDiagram);
        _onMenuOpen           = () => InvokeAsync(OpenDiagram);
        _onMenuUndo           = () => InvokeAsync(UndoAction);
        _onMenuRedo           = () => InvokeAsync(RedoAction);

        AppState.MenuSaveDiagram    += _onMenuSaveDiagram;
        AppState.MenuReloadDiagram  += _onMenuReloadDiagram;
        AppState.MenuEditProperties += _onMenuEditProperties;
        AppState.MenuSaveAs         += _onMenuSaveAs;
        AppState.MenuOpen           += _onMenuOpen;
        AppState.MenuUndo           += _onMenuUndo;
        AppState.MenuRedo           += _onMenuRedo;
    }

    private void UnsubscribeEditEvents()
    {
        if (_diagram != null) _diagram.Links.Added -= OnLinkAdded;
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
        if (_onMenuSaveAs         != null) AppState.MenuSaveAs         -= _onMenuSaveAs;
        if (_onMenuOpen           != null) AppState.MenuOpen           -= _onMenuOpen;
        if (_onMenuUndo           != null) AppState.MenuUndo           -= _onMenuUndo;
        if (_onMenuRedo           != null) AppState.MenuRedo           -= _onMenuRedo;

        _onMenuSaveDiagram = _onMenuReloadDiagram = _onMenuEditProperties = null;
        _onMenuSaveAs = _onMenuOpen = _onMenuUndo = _onMenuRedo = null;
    }

    // ── Diagram event handlers ────────────────────────────────────────────────

    private void OnSelectionChanged(object model)
    {
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged()
    {
        AppState.MarkDirty();
        InvokeAsync(StateHasChanged);
    }

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
        AppState.UpdateSelectionState(selected.Count > 0, selected.Count == 1);
    }

    // ── Node operations ───────────────────────────────────────────────────────

    private void AddNode()
    {
        if (_diagram == null) return;
        PushUndoSnapshot();
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
        PushUndoSnapshot();
        foreach (var n in _diagram.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void NewDiagram()
    {
        InvokeAsync(async () =>
        {
            if (AppState.IsDirty)
            {
                bool confirmed = await ConfirmDiscardChanges("New diagram");
                if (!confirmed) return;
            }
            PushUndoSnapshot();
            if (_diagram != null)
            {
                _diagram.SelectionChanged -= OnSelectionChanged;
                _diagram.Changed -= OnDiagramChanged;
            }
            AppState.ResetDiagram();
            AppState.SetDiagramName(string.Empty);
            AppState.MarkClean();
            AppState.ClearUndoRedo();
            _diagram = AppState.GetOrCreateDiagram();
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            _nodeCounter = 1;
            UpdateSelectionState();
            Snackbar.Add("New diagram created", Severity.Info);
            StateHasChanged();
        });
    }

    private async Task ReloadDiagram()
    {
        if (AppState.IsDirty)
        {
            bool confirmed = await ConfirmDiscardChanges("Reload diagram");
            if (!confirmed) return;
        }
        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();
        AppState.MarkClean();
        AppState.ClearUndoRedo();
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

    // ── Clipboard ─────────────────────────────────────────────────────────────

    private void CopySelectedNodes()
    {
        if (_diagram == null) return;
        var selected = _diagram.GetSelectedModels().OfType<MudNodeModel>().ToList();
        if (!selected.Any()) return;
        var snapshots = selected.Select(n => new NodeState
        {
            Id = n.Id,
            Title = n.Title ?? string.Empty,
            X = n.Position?.X ?? 0,
            Y = n.Position?.Y ?? 0,
            Width = n.Size?.Width ?? 120,
            Height = n.Size?.Height ?? 90,
            Icon = n.Icon,
            IconName = n.IconName,
            Text = n.Text,
            BackgroundColor = n.BackgroundColor,
            IconColor = n.IconColor,
            Metadata = n.Metadata ?? new Dictionary<string, string>(),
            DataTopic = n.DataTopic,
            DataTopic2 = n.DataTopic2,
            FontSize = n.FontSize,
            LinkAnimation = n.LinkAnimation,
            Ports = n.Ports.Select(p => new PortState { Id = p.Id, Alignment = p.Alignment.ToString() }).ToList()
        }).ToList();
        AppState.SetClipboard(snapshots);
        Snackbar.Add($"Copied {snapshots.Count} node(s)", Severity.Info);
    }

    private void CutSelectedNodes()
    {
        CopySelectedNodes();
        PushUndoSnapshot();
        foreach (var n in _diagram!.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        StateHasChanged();
    }

    private void PasteNodes()
    {
        if (_diagram == null || !AppState.HasClipboard) return;
        PushUndoSnapshot();
        _diagram.UnselectAll();
        const double offset = 30;
        foreach (var ns in AppState.Clipboard)
        {
            var node = new MudNodeModel(new Point(ns.X + offset, ns.Y + offset))
            {
                Title = ns.Title,
                Icon = ns.Icon,
                IconName = ns.IconName,
                Text = ns.Text,
                BackgroundColor = ns.BackgroundColor,
                IconColor = ns.IconColor,
                Metadata = ns.Metadata ?? new Dictionary<string, string>(),
                DataTopic = ns.DataTopic,
                DataTopic2 = ns.DataTopic2,
                FontSize = ns.FontSize,
                LinkAnimation = ns.LinkAnimation,
                Size = new Blazor.Diagrams.Core.Geometry.Size(ns.Width, ns.Height),
            };
            foreach (var ps in ns.Ports)
            {
                if (Enum.TryParse<Blazor.Diagrams.Core.Models.PortAlignment>(ps.Alignment, out var alignment))
                    AppState.AddPortToNode(node, alignment);
            }
            _diagram.Nodes.Add(node);
            _diagram.Controls.AddFor(node).Add(new Blazor.Diagrams.Core.Controls.Default.ResizeControl(new Blazor.Diagrams.Core.Positions.Resizing.BottomRightResizerProvider()));
            _diagram.SelectModel(node, true);
        }
        UpdateSelectionState();
        Snackbar.Add($"Pasted {AppState.Clipboard.Count} node(s)", Severity.Info);
        StateHasChanged();
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    private void PushUndoSnapshot()
    {
        if (_diagram == null) return;
        AppState.PushUndoSnapshot(AppState.GetDiagramState());
    }

    private async Task UndoAction()
    {
        if (_diagram == null || !AppState.CanUndo) return;
        var current = AppState.GetDiagramState();
        var previous = AppState.PopUndo(current);
        if (previous == null) return;
        await ApplyDiagramState(previous);
        Snackbar.Add("Undo", Severity.Info);
    }

    private async Task RedoAction()
    {
        if (_diagram == null || !AppState.CanRedo) return;
        var current = AppState.GetDiagramState();
        var next = AppState.PopRedo(current);
        if (next == null) return;
        await ApplyDiagramState(next);
        Snackbar.Add("Redo", Severity.Info);
    }

    private async Task ApplyDiagramState(DiagramState state)
    {
        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();
        _diagram = AppState.CreateDiagramFromState(state, readOnly: !AppState.IsEditMode);
        if (AppState.IsEditMode)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            UpdateSelectionState();
        }
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    // ── Save As / Open ────────────────────────────────────────────────────────

    private async Task SaveAsDiagram()
    {
        var parameters = new DialogParameters<SimpleInputDialog>
        {
            { d => d.Title, "Save Diagram As" },
            { d => d.Label, "Diagram name" },
            { d => d.Value, AppState.DiagramName }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<SimpleInputDialog>("Save As", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var state = AppState.GetDiagramState();
            state.Name = name;
            var success = await DiagramService.SaveDiagramByNameAsync(name, state);
            if (success)
            {
                AppState.SetDiagramName(name);
                AppState.MarkClean();
                Snackbar.Add($"Saved as '{name}'", Severity.Success);
            }
            else
            {
                Snackbar.Add("Failed to save diagram", Severity.Error);
            }
        }
    }

    private async Task OpenDiagram()
    {
        if (AppState.IsDirty)
        {
            bool confirmed = await ConfirmDiscardChanges("Open diagram");
            if (!confirmed) return;
        }
        var names = await DiagramService.ListDiagramsAsync();
        if (!names.Any())
        {
            Snackbar.Add("No saved diagrams found", Severity.Warning);
            return;
        }
        var parameters = new DialogParameters<DiagramPickerDialog>
        {
            { d => d.DiagramNames, names }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<DiagramPickerDialog>("Open Diagram", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var state = await DiagramService.LoadDiagramByNameAsync(name);
            if (state != null)
            {
                AppState.ClearUndoRedo();
                await ApplyDiagramState(state);
                AppState.MarkClean();
                Snackbar.Add($"Opened '{name}' ({state.Nodes.Count} nodes)", Severity.Info);
            }
            else
            {
                Snackbar.Add($"Failed to load '{name}'", Severity.Error);
            }
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private async Task SaveDiagram()
    {
        try
        {
            var state = AppState.GetDiagramState();
            var success = await DiagramService.SaveDiagramAsync(state);
            if (success)
            {
                AppState.MarkClean();
                Snackbar.Add($"Diagram saved ({state.Nodes.Count} nodes, {state.Links.Count} links)", Severity.Success);
            }
            else
            {
                Snackbar.Add("Failed to save diagram", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving diagram: {ex.Message}", Severity.Error);
        }
    }

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

    // ── Properties ────────────────────────────────────────────────────────────

    private async Task EditNodeProperties()
    {
        if (_diagram == null) return;
        var node = _diagram.GetSelectedModels().OfType<MudNodeModel>().FirstOrDefault();
        if (node == null) { Snackbar.Add("No node selected", Severity.Warning); return; }
        var parameters = new DialogParameters { { "Node", node } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, BackdropClick = true };
        var dialog = await DialogService.ShowAsync<NodePropertyEditor>("Edit Node Properties", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            StateHasChanged();
            Snackbar.Add("Node properties updated", Severity.Success);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> ConfirmDiscardChanges(string action)
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Unsaved Changes",
            $"You have unsaved changes. Proceed with {action} and discard changes?",
            yesText: "Discard", cancelText: "Cancel");
        return result == true;
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

