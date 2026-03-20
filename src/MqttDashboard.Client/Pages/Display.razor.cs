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
using Microsoft.JSInterop;
using MudBlazor;

namespace MqttDashboard.Pages;

public partial class Display : IDisposable
{
    [Inject] private ApplicationState AppState { get; set; } = default!;
    [Inject] private IDashboardService DashboardService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    // Multi-page diagram state
    private List<BlazorDiagram?> _diagrams = [null];
    private List<DiagramState> _pageStates = [new DiagramState()];
    private int _activePageIndex = 0;
    private BlazorDiagram? _diagram => _diagrams.Count > _activePageIndex ? _diagrams[_activePageIndex] : null;

    // Pre-edit snapshot for discard revert
    private DiagramState? _editSnapshot;

    // Suppress dirty tracking during mode switches and diagram loading
    private bool _suppressDirty = false;

    // Inline tab rename state
    private int _renamingPageIndex = -1;
    private string _renameValue = string.Empty;

    private int _nodeCounter = 1;
    private int _pasteGeneration = 0;

    // Stored handler references for clean unsubscription
    private Action? _onMenuSaveDiagram;
    private Action? _onMenuReloadDiagram;
    private Action? _onMenuEditProperties;
    private Action? _onMenuSaveAs;
    private Action? _onMenuOpen;
    private Action? _onMenuUndo;
    private Action? _onMenuRedo;
    private Action? _onMenuDiagramProperties;
    private Action? _onMenuPaste;
    private Action? _onMenuAddPage;
    private Action<int>? _onMenuSetActivePage;
    private DateTimeOffset _lastUndoPushByMove = DateTimeOffset.MinValue;
    private readonly List<(NodeModel Node, Action<Blazor.Diagrams.Core.Models.Base.Model> Handler)> _nodeChangedSubscriptions = new();

    private const string LastDiagramKey = "mqttdashboard_lastDiagram";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();
            AppState.OnToggleEditModeRequested += OnToggleEditModeRequested;
            AppState.OnStateChanged += OnAppStateChanged;

            // Subscribe Open / OpenRecent for all modes (not just edit mode)
            _onMenuOpen = () => InvokeAsync(OpenDiagram);
            AppState.MenuOpen += _onMenuOpen;

            // Subscribe page navigation for all modes
            _onMenuSetActivePage = idx => { _ = InvokeAsync(() => SwitchToPageAsync(idx)); };
            AppState.MenuSetActivePage += _onMenuSetActivePage;

            var savedState = await DashboardService.LoadDashboardAsync();
            if (savedState != null && (savedState.Nodes.Count > 0 || savedState.Pages?.Count > 0))
            {
                LoadFullState(savedState, readOnly: true);
                AppState.MarkSaved();
                StateHasChanged();
                await Task.Delay(100);
                RefreshAll();
                StateHasChanged();
            }
            else
            {
                LoadFullState(null, readOnly: true);
                AppState.MarkSaved();
                StateHasChanged();
            }

            // Try to auto-open the last named diagram if none was loaded by name
            if (string.IsNullOrEmpty(AppState.DiagramName))
            {
                var lastName = await GetLastDiagramName();
                if (!string.IsNullOrEmpty(lastName))
                {
                    var lastState = await DashboardService.LoadDashboardByNameAsync(lastName);
                    if (lastState != null)
                    {
                        LoadFullState(lastState, readOnly: true);
                        AppState.MarkSaved();
                        StateHasChanged();
                    }
                }
            }
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    // ── Full state loading (replaces all pages) ───────────────────────────────

    private void LoadFullState(DiagramState? state, bool readOnly)
    {
        // Unsubscribe selection/change from all existing diagrams
        foreach (var d in _diagrams.OfType<BlazorDiagram>())
        {
            d.SelectionChanged -= OnSelectionChanged;
            d.Changed -= OnDiagramChanged;
        }
        AppState.ResetDiagram();

        _suppressDirty = true;
        try
        {
            if (state?.Pages != null && state.Pages.Count > 0)
            {
                var pageNames = state.Pages.Select(p => p.Name).ToList();
                _pageStates = state.Pages.Select(p => FromPageState(p, state)).ToList();
                _diagrams = new List<BlazorDiagram?>(Enumerable.Repeat<BlazorDiagram?>(null, _pageStates.Count));
                _activePageIndex = 0;
                _diagrams[0] = AppState.CreateDiagramFromState(_pageStates[0], readOnly);
                AppState.SetPageNames(pageNames, 0);
            }
            else if (state != null)
            {
                _pageStates = [state];
                _diagrams = [null];
                _activePageIndex = 0;
                _diagrams[0] = AppState.CreateDiagramFromState(state, readOnly);
                AppState.SetPageNames(["Page 1"], 0);
            }
            else
            {
                _pageStates = [new DiagramState { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 20 }];
                _diagrams = [null];
                _activePageIndex = 0;
                _diagrams[0] = AppState.GetOrCreateDiagram();
                AppState.SetPageNames(["Page 1"], 0);
            }
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private static DiagramState FromPageState(PageState page, DiagramState template) => new DiagramState
    {
        Name = template.Name,
        ShowDiagramName = template.ShowDiagramName,
        MqttSubscriptions = template.MqttSubscriptions,
        GridSize = page.GridSize,
        BackgroundColor = page.BackgroundColor,
        Nodes = page.Nodes,
        Links = page.Links,
    };

    private DiagramState BuildFullState()
    {
        // Capture current page state
        var currentState = AppState.GetDiagramState();
        _pageStates[_activePageIndex] = currentState;

        if (_pageStates.Count > 1)
        {
            // Multi-page: serialize as Pages list
            return new DiagramState
            {
                Name = AppState.DiagramDisplayName,
                ShowDiagramName = AppState.ShowDiagramName,
                MqttSubscriptions = new HashSet<string>(AppState.SubscribedTopics),
                BackgroundColor = AppState.CanvasBackgroundColor,
                GridSize = currentState.GridSize,
                Pages = _pageStates.Select((ps, i) => new PageState
                {
                    Name = i < AppState.PageNames.Count ? AppState.PageNames[i] : $"Page {i + 1}",
                    Nodes = ps.Nodes,
                    Links = ps.Links,
                    GridSize = ps.GridSize,
                    BackgroundColor = ps.BackgroundColor,
                }).ToList(),
            };
        }

        // Single page: return flat format for backward compat
        return currentState;
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

        if (!enterEditMode && AppState.IsEdited)
        {
            var confirm = await DialogService.ShowMessageBoxAsync(
                "Unsaved Changes",
                "You have unsaved changes. Save before leaving edit mode?",
                yesText: "Save",
                noText: "Discard",
                cancelText: "Cancel");
            if (confirm == null) return; // Cancel — stay in edit mode
            if (confirm == true)
            {
                var saved = await SaveDashboard();
                if (!saved) return; // Stay in edit mode if save failed
            }
            else
            {
                // Discard — revert to pre-edit snapshot
                AppState.MarkSaved();
                if (_editSnapshot != null)
                {
                    _suppressDirty = true;
                    try { LoadFullState(_editSnapshot, readOnly: true); }
                    finally { _suppressDirty = false; }
                    AppState.MarkSaved();
                    StateHasChanged();
                    return;
                }
            }
        }

        if (enterEditMode)
        {
            // Snapshot current state before any edit-mode changes
            _editSnapshot = BuildFullState();
        }

        if (AppState.IsEditMode)
            UnsubscribeEditEvents();

        if (AppState.IsEditMode && !enterEditMode)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }

        _suppressDirty = true;
        try
        {
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
        }
        finally
        {
            _suppressDirty = false;
        }

        AppState.SetEditMode(enterEditMode);
        // Clear any dirty flag spuriously raised during mode-switch setup
        if (enterEditMode) AppState.MarkSaved();
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
        _onMenuPaste = () => InvokeAsync(PasteNodesAsync);
        AppState.MenuPasteSelected += _onMenuPaste;
        AppState.MenuAddPort       += AddPortToSelectedNode;
        AppState.MenuDeletePort    += DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    += NewDiagram;

        _onMenuSaveDiagram    = () => InvokeAsync(async () => { await SaveDashboard(); });
        _onMenuReloadDiagram  = () => InvokeAsync(ReloadDiagram);
        _onMenuEditProperties = () => InvokeAsync(EditNodeProperties);
        _onMenuSaveAs         = () => InvokeAsync(SaveAsDiagram);
        _onMenuUndo           = () => InvokeAsync(UndoAction);
        _onMenuRedo           = () => InvokeAsync(RedoAction);

        AppState.MenuSaveDiagram    += _onMenuSaveDiagram;
        AppState.MenuReloadDiagram  += _onMenuReloadDiagram;
        AppState.MenuEditProperties += _onMenuEditProperties;
        AppState.MenuSaveAs         += _onMenuSaveAs;
        AppState.MenuUndo           += _onMenuUndo;
        AppState.MenuRedo           += _onMenuRedo;

        _onMenuDiagramProperties = () => InvokeAsync(ShowDiagramPropertiesAsync);
        AppState.MenuDiagramProperties += _onMenuDiagramProperties;

        _onMenuAddPage = () => InvokeAsync(AddPageAsync);
        AppState.MenuAddPage += _onMenuAddPage;

        // Subscribe to existing nodes' Changed events to detect moves
        foreach (var node in _diagram!.Nodes.OfType<NodeModel>())
            SubscribeToNodeChanges(node);
        // Subscribe to future nodes
        _diagram.Nodes.Added += OnNodeAddedInEditMode;
    }

    private void UnsubscribeEditEvents()
    {
        _diagram?.Links.Added -= OnLinkAdded;
        AppState.MenuAddNode       -= AddNode;
        AppState.MenuDeleteNode    -= DeleteSelectedNode;
        AppState.MenuCutSelected   -= CutSelectedNodes;
        AppState.MenuCopySelected  -= CopySelectedNodes;
        if (_onMenuPaste != null) AppState.MenuPasteSelected -= _onMenuPaste;
        AppState.MenuAddPort       -= AddPortToSelectedNode;
        AppState.MenuDeletePort    -= DeletePortFromSelectedNode;
        AppState.MenuNewDiagram    -= NewDiagram;

        if (_onMenuSaveDiagram    != null) AppState.MenuSaveDiagram    -= _onMenuSaveDiagram;
        if (_onMenuReloadDiagram  != null) AppState.MenuReloadDiagram  -= _onMenuReloadDiagram;
        if (_onMenuEditProperties != null) AppState.MenuEditProperties -= _onMenuEditProperties;
        if (_onMenuSaveAs         != null) AppState.MenuSaveAs         -= _onMenuSaveAs;
        if (_onMenuUndo           != null) AppState.MenuUndo           -= _onMenuUndo;
        if (_onMenuRedo           != null) AppState.MenuRedo           -= _onMenuRedo;

        if (_onMenuDiagramProperties != null) AppState.MenuDiagramProperties -= _onMenuDiagramProperties;
        if (_onMenuAddPage           != null) AppState.MenuAddPage           -= _onMenuAddPage;

        _diagram?.Nodes.Added -= OnNodeAddedInEditMode;
        foreach (var (node, handler) in _nodeChangedSubscriptions)
            node.Changed -= handler;
        _nodeChangedSubscriptions.Clear();

        _onMenuSaveDiagram = _onMenuReloadDiagram = _onMenuEditProperties = null;
        _onMenuSaveAs = _onMenuUndo = _onMenuRedo = _onMenuDiagramProperties = _onMenuAddPage = null;
    }

    // ── Diagram event handlers ────────────────────────────────────────────────

    private void OnSelectionChanged(object model)
    {
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged()
    {
        if (_suppressDirty) return;
        AppState.MarkEdited();
        InvokeAsync(StateHasChanged);
    }

    private void OnLinkAdded(Blazor.Diagrams.Core.Models.Base.BaseLinkModel link)
    {
        if (link is not LinkModel lm) return;
        if ((link.Source?.Model is PortModel port ? port.Parent : link.Source?.Model) is NodeModel sourceNode)
            AppState.CheckForLinkAnimation(sourceNode, lm);
    }

    private void UpdateSelectionState()
    {
        var selected = _diagram?.GetSelectedModels().OfType<NodeModel>().ToList() ?? [];
        AppState.UpdateSelectionState(selected.Count > 0, selected.Count == 1);
    }

    // ── Node operations ───────────────────────────────────────────────────────

    private async void AddNode()
    {
        if (_diagram == null) return;

        var dialog = await DialogService.ShowAsync<NodeTypePickerDialog>("Add Node",
            new DialogParameters(),
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });
        var result = await dialog.Result;
        if (result == null || result.Canceled || result.Data is not string nodeType) return;

        PushUndoSnapshot();
        var rng = new Random();
        _diagram.UnselectAll();

        MudNodeModel node = nodeType switch
        {
            "Gauge"    => new GaugeNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))    { Title = $"Gauge {_nodeCounter++}" },
            "Switch"   => new SwitchNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))   { Title = $"Switch {_nodeCounter++}" },
            "Battery"  => new BatteryNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))  { Title = $"Battery {_nodeCounter++}" },
            "Log"      => new LogNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))      { Title = $"Log {_nodeCounter++}" },
            "TreeView" => new TreeViewNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400))) { Title = $"Tree {_nodeCounter++}" },
            _          => new MudNodeModel(new Point(rng.Next(50, 500), rng.Next(50, 400)))      { Title = $"Node {_nodeCounter++}" },
        };

        _diagram.Nodes.Add(node);
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
            if (AppState.IsEdited)
            {
                bool confirmed = await ConfirmDiscardChanges("New dashboard");
                if (!confirmed) return;
            }
            PushUndoSnapshot();

            // Unsubscribe from current diagram
            if (_diagram != null)
            {
                _diagram.SelectionChanged -= OnSelectionChanged;
                _diagram.Changed -= OnDiagramChanged;
            }
            UnsubscribeEditEvents();

            AppState.ResetDiagram();
            AppState.SetDiagramName(string.Empty);
            AppState.SetDisplayName(string.Empty);
            AppState.MarkSaved();
            AppState.ClearUndoRedo();

            _pageStates = [new DiagramState { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 20 }];
            _diagrams = [null];
            _activePageIndex = 0;
            AppState.SetPageNames(["Page 1"], 0);

            _diagrams[0] = AppState.GetOrCreateDiagram();
            _diagram!.SelectionChanged += OnSelectionChanged;
            _diagram!.Changed += OnDiagramChanged;
            SubscribeEditEvents();

            _nodeCounter = 1;
            UpdateSelectionState();
            Snackbar.Add("New dashboard created", Severity.Info);
            StateHasChanged();
        });
    }

    private async Task ReloadDiagram()
    {
        if (AppState.IsEdited)
        {
            bool confirmed = await ConfirmDiscardChanges("Reload dashboard");
            if (!confirmed) return;
        }
        if (AppState.IsEditMode)
        {
            _diagram?.SelectionChanged -= OnSelectionChanged;
            _diagram?.Changed -= OnDiagramChanged;
            UnsubscribeEditEvents();
        }

        AppState.MarkSaved();
        AppState.ClearUndoRedo();
        var savedState = await DashboardService.LoadDashboardAsync();
        if (savedState != null && (savedState.Nodes.Count > 0 || savedState.Pages?.Count > 0))
        {
            var prevTopics = AppState.SubscribedTopics.ToHashSet();
            LoadFullState(savedState, readOnly: !AppState.IsEditMode);
            await SyncSubscriptionsAsync(prevTopics, AppState.SubscribedTopics);
            var gs = _diagram?.Options.GridSize;
            if (AppState.IsEditMode && gs.HasValue)
                AppState.SetGridSize((int)gs.Value);
            var nodeCount = savedState.Pages?.Sum(p => p.Nodes.Count) ?? savedState.Nodes.Count;
            Snackbar.Add($"Dashboard reloaded ({nodeCount} nodes)", Severity.Info);
        }
        else
        {
            LoadFullState(null, readOnly: !AppState.IsEditMode);
            Snackbar.Add("No saved dashboard found", Severity.Warning);
        }
        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            SubscribeEditEvents();
            UpdateSelectionState();
        }
        StateHasChanged();
    }

    // ── Page management ───────────────────────────────────────────────────────

    private async Task SwitchToPageAsync(int index)
    {
        if (index == _activePageIndex) return;
        if (index < 0 || index >= _pageStates.Count) return;

        // Save current page state
        if (_diagram != null)
            _pageStates[_activePageIndex] = AppState.GetDiagramState();

        // Unsubscribe diagram-specific events from current page
        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
            _diagram.Links.Added -= OnLinkAdded;
            _diagram.Nodes.Added -= OnNodeAddedInEditMode;
            foreach (var (node, handler) in _nodeChangedSubscriptions)
                node.Changed -= handler;
            _nodeChangedSubscriptions.Clear();
        }

        // Switch page
        _activePageIndex = index;
        AppState.SetActivePage(index);

        // Create diagram for the new page (always fresh to handle mode changes)
        _suppressDirty = true;
        try { _diagrams[_activePageIndex] = AppState.CreateDiagramFromState(_pageStates[_activePageIndex], !AppState.IsEditMode); }
        finally { _suppressDirty = false; }

        // Re-subscribe diagram events for the new page
        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            _diagram.Links.Added += OnLinkAdded;
            _diagram.Nodes.Added += OnNodeAddedInEditMode;
            foreach (var node in _diagram.Nodes.OfType<NodeModel>())
                SubscribeToNodeChanges(node);
            UpdateSelectionState();
        }

        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private async Task AddPageAsync()
    {
        var newPageName = $"Page {_pageStates.Count + 1}";
        var newPageState = new DiagramState { GridSize = AppState.GridSize > 0 ? AppState.GridSize : 20 };
        _pageStates.Add(newPageState);
        _diagrams.Add(null);
        var newNames = new List<string>(AppState.PageNames) { newPageName };
        AppState.SetPageNames(newNames, _activePageIndex);
        await SwitchToPageAsync(_pageStates.Count - 1);
        AppState.MarkEdited();
    }

    private async Task RemovePageAsync(int index)
    {
        if (_pageStates.Count <= 1) return;

        var pageName = index < AppState.PageNames.Count ? AppState.PageNames[index] : $"Page {index + 1}";
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Delete Page",
            $"Delete '{pageName}'? All widgets on this page will be lost.",
            yesText: "Delete",
            cancelText: "Cancel");
        if (confirm != true) return;

        // Save current page before removing
        if (_diagram != null && index == _activePageIndex)
            _pageStates[_activePageIndex] = AppState.GetDiagramState();

        // Unsubscribe from the diagram being removed (if in edit mode)
        if (AppState.IsEditMode)
        {
            var removingDiagram = _diagrams.Count > index ? _diagrams[index] : null;
            if (removingDiagram != null)
            {
                removingDiagram.SelectionChanged -= OnSelectionChanged;
                removingDiagram.Changed -= OnDiagramChanged;
                removingDiagram.Links.Added -= OnLinkAdded;
                removingDiagram.Nodes.Added -= OnNodeAddedInEditMode;
            }
            if (index == _activePageIndex)
            {
                foreach (var (node, handler) in _nodeChangedSubscriptions)
                    node.Changed -= handler;
                _nodeChangedSubscriptions.Clear();
            }
        }

        _pageStates.RemoveAt(index);
        _diagrams.RemoveAt(index);
        var newNames = new List<string>(AppState.PageNames);
        newNames.RemoveAt(index);
        var newActive = Math.Clamp(_activePageIndex >= index ? _activePageIndex - 1 : _activePageIndex, 0, _pageStates.Count - 1);
        _activePageIndex = newActive;
        AppState.SetPageNames(newNames, newActive);

        // Create diagram for the now-active page if needed
        if (_diagrams[_activePageIndex] == null)
            _diagrams[_activePageIndex] = AppState.CreateDiagramFromState(_pageStates[_activePageIndex], !AppState.IsEditMode);
        else
            AppState.SetActiveDiagram(_diagrams[_activePageIndex]);

        if (AppState.IsEditMode && _diagram != null)
        {
            _diagram.SelectionChanged += OnSelectionChanged;
            _diagram.Changed += OnDiagramChanged;
            _diagram.Links.Added += OnLinkAdded;
            _diagram.Nodes.Added += OnNodeAddedInEditMode;
            foreach (var node in _diagram.Nodes.OfType<NodeModel>())
                SubscribeToNodeChanges(node);
            UpdateSelectionState();
        }

        AppState.MarkEdited();
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private void StartRename(int index, string currentName)
    {
        _renamingPageIndex = index;
        _renameValue = currentName;
        StateHasChanged();
    }

    private async Task CommitRename(int index)
    {
        _renamingPageIndex = -1;
        await RenamePageAsync(index, _renameValue);
    }

    private Task RenamePageAsync(int index, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return Task.CompletedTask;
        if (index < 0 || index >= _pageStates.Count) return Task.CompletedTask;
        var newNames = new List<string>(AppState.PageNames);
        newNames[index] = newName.Trim();
        AppState.SetPageNames(newNames, _activePageIndex);
        AppState.MarkEdited();
        StateHasChanged();
        return Task.CompletedTask;
    }
    // Clipboard tag written to the OS clipboard so we can recognise our own data on paste.
    private const string ClipboardTag = """{"mqttdashboard":"nodes",""";

    private static List<NodeState> BuildSnapshots(IEnumerable<MudNodeModel> selected)
    {
        return selected.Select(n =>
        {
            var ns = new NodeState
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
                NodeType = n.NodeType ?? "Text",
                TitlePosition = n.TitlePosition,
                Ports = n.Ports.Select(p => new PortState { Id = p.Id, Alignment = p.Alignment.ToString() }).ToList()
            };
            if (n is GaugeNodeModel g)
            {
                ns.MinValue = g.MinValue; ns.MaxValue = g.MaxValue; ns.Unit = g.Unit;
                ns.ArcOrigin = g.ArcOrigin;
                ns.ColorThresholds = g.ColorThresholds.Count > 0
                    ? g.ColorThresholds.Select(t => new GaugeColorThresholdState { Value = t.Value, Color = t.Color, Direction = t.Direction }).ToList()
                    : null;
            }
            else if (n is SwitchNodeModel s)
            {
                ns.PublishTopic = s.PublishTopic; ns.OnValue = s.OnValue; ns.OffValue = s.OffValue;
            }
            else if (n is BatteryNodeModel b)
            {
                ns.MinValue = b.MinValue; ns.MaxValue = b.MaxValue; ns.BatteryShowPercent = b.ShowPercent;
                ns.ColorThresholds = b.ColorThresholds.Count > 0
                    ? b.ColorThresholds.Select(t => new GaugeColorThresholdState { Value = t.Value, Color = t.Color, Direction = t.Direction }).ToList()
                    : null;
            }
            else if (n is LogNodeModel log)
            {
                ns.MaxEntries = log.MaxEntries; ns.ShowTime = log.ShowTime; ns.ShowDate = log.ShowDate;
            }
            else if (n is TreeViewNodeModel tv)
            {
                ns.RootTopic = tv.RootTopic; ns.ShowValues = tv.ShowValues;
            }
            return ns;
        }).ToList();
    }

    private void CopySelectedNodes()
    {
        if (_diagram == null) return;
        var selected = _diagram.GetSelectedModels().OfType<MudNodeModel>().ToList();
        if (selected.Count == 0) return;
        _pasteGeneration = 0;
        var snapshots = BuildSnapshots(selected);
        AppState.SetClipboard(snapshots);

        // Also write to the OS clipboard so paste works across browser windows.
        // Fire-and-forget — failure is benign, in-memory clipboard is the fallback.
        var json = System.Text.Json.JsonSerializer.Serialize(new { mqttdashboard = "nodes", data = snapshots });
        _ = JSRuntime.InvokeAsync<bool>("mqttClipboard.writeText", json).AsTask()
              .ContinueWith(_ => { });   // swallow errors

        Snackbar.Add($"Copied {snapshots.Count} node(s)", Severity.Info);
    }

    private void CutSelectedNodes()
    {
        _pasteGeneration = 0;
        CopySelectedNodes();
        PushUndoSnapshot();
        foreach (var n in _diagram!.GetSelectedModels().OfType<NodeModel>().ToList())
            _diagram.Nodes.Remove(n);
        UpdateSelectionState();
        StateHasChanged();
    }

    private async Task PasteNodesAsync()
    {
        if (_diagram == null) return;

        // Try to read nodes from the OS clipboard first (supports cross-window paste).
        List<NodeState>? toPaste = null;
        try
        {
            var text = await JSRuntime.InvokeAsync<string?>("mqttClipboard.readText");
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("{\"mqttdashboard\":\"nodes\"", StringComparison.Ordinal))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    toPaste = System.Text.Json.JsonSerializer.Deserialize<List<NodeState>>(
                        dataEl.GetRawText());
                    if (toPaste != null)
                        AppState.SetClipboard(toPaste);
                }
            }
        }
        catch { /* ignore — fall back to in-memory clipboard */ }

        if (toPaste == null)
        {
            if (!AppState.HasClipboard) return;
            toPaste = AppState.Clipboard.ToList();
        }

        if (toPaste.Count == 0) return;

        PushUndoSnapshot();
        _pasteGeneration++;
        _diagram.UnselectAll();
        double offset = 30 * _pasteGeneration;

        foreach (var ns in toPaste)
        {
            MudNodeModel node = ns.NodeType switch
            {
                "Gauge" => new GaugeNodeModel(new Point(ns.X + offset, ns.Y + offset))
                {
                    MinValue = ns.MinValue ?? 0,
                    MaxValue = ns.MaxValue ?? 100,
                    Unit = ns.Unit,
                    ArcOrigin = ns.ArcOrigin,
                    ColorThresholds = ns.ColorThresholds?.Select(t => new GaugeColorThreshold { Value = t.Value, Color = t.Color, Direction = t.Direction }).ToList() ?? new(),
                },
                "Switch" => new SwitchNodeModel(new Point(ns.X + offset, ns.Y + offset))
                {
                    PublishTopic = ns.PublishTopic,
                    OnValue = ns.OnValue ?? "1",
                    OffValue = ns.OffValue ?? "0",
                },
                "Battery" => new BatteryNodeModel(new Point(ns.X + offset, ns.Y + offset))
                {
                    MinValue = ns.MinValue ?? 0,
                    MaxValue = ns.MaxValue ?? 100,
                    ShowPercent = ns.BatteryShowPercent ?? true,
                    ColorThresholds = ns.ColorThresholds?.Select(t => new GaugeColorThreshold { Value = t.Value, Color = t.Color, Direction = t.Direction }).ToList() ?? new(),
                },
                "Log" => new LogNodeModel(new Point(ns.X + offset, ns.Y + offset))
                {
                    MaxEntries = ns.MaxEntries ?? 20,
                    ShowTime = ns.ShowTime ?? true,
                    ShowDate = ns.ShowDate ?? false,
                },
                "TreeView" => new TreeViewNodeModel(new Point(ns.X + offset, ns.Y + offset))
                {
                    RootTopic = ns.RootTopic ?? string.Empty,
                    ShowValues = ns.ShowValues ?? true,
                },
                _ => new MudNodeModel(new Point(ns.X + offset, ns.Y + offset)),
            };
            node.Title = ns.Title;
            node.NodeType = ns.NodeType ?? "Text";
            node.Icon = ns.Icon;
            node.IconName = ns.IconName;
            node.Text = ns.Text;
            node.BackgroundColor = ns.BackgroundColor;
            node.IconColor = ns.IconColor;
            node.Metadata = ns.Metadata ?? new Dictionary<string, string>();
            node.DataTopic = ns.DataTopic;
            node.DataTopic2 = ns.DataTopic2;
            node.FontSize = ns.FontSize;
            node.LinkAnimation = ns.LinkAnimation;
            node.TitlePosition = ns.TitlePosition ?? "Above";
            node.Size = new Blazor.Diagrams.Core.Geometry.Size(ns.Width, ns.Height);
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
        Snackbar.Add($"Pasted {toPaste.Count} node(s)", Severity.Info);
        StateHasChanged();
    }


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
        var previousTopics = AppState.SubscribedTopics.ToHashSet();
        AppState.ResetDiagram();
        var newDiagram = AppState.CreateDiagramFromState(state, readOnly: !AppState.IsEditMode);
        _diagrams[_activePageIndex] = newDiagram;
        _pageStates[_activePageIndex] = state;
        await SyncSubscriptionsAsync(previousTopics, AppState.SubscribedTopics);
        if (AppState.IsEditMode)
        {
            _diagram!.SelectionChanged += OnSelectionChanged;
            _diagram!.Changed += OnDiagramChanged;
            UpdateSelectionState();
        }
        StateHasChanged();
        await Task.Delay(50);
        RefreshAll();
        StateHasChanged();
    }

    private async Task SyncSubscriptionsAsync(HashSet<string> previous, IReadOnlyCollection<string> current)
    {
        if (AppState.SignalRService == null) return;
        var currentSet = new HashSet<string>(current);
        foreach (var topic in previous.Where(t => !currentSet.Contains(t)))
            await AppState.SignalRService.UnsubscribeFromTopicAsync(topic);
        foreach (var topic in currentSet.Where(t => !previous.Contains(t)))
            await AppState.SignalRService.SubscribeToTopicAsync(topic);
    }

    // ── Save As / Open ────────────────────────────────────────────────────────

    private async Task SaveAsDiagram()
    {
        var parameters = new DialogParameters<SimpleInputDialog>
        {
            { d => d.Title, "Save Dashboard As" },
            { d => d.Label, "Dashboard name" },
            { d => d.Value, AppState.DiagramName }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<SimpleInputDialog>("Save As", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var state = BuildFullState();
            var success = await DashboardService.SaveDashboardByNameAsync(name, state);
            if (success)
            {
                AppState.SetDiagramName(name);
                AppState.MarkSaved();
                await SaveLastDiagramName(name);
                Snackbar.Add($"Saved as '{name}'", Severity.Success);
            }
            else
            {
                Snackbar.Add("Failed to save dashboard", Severity.Error);
            }
        }
    }

    private async Task OpenDiagram()
    {
        if (AppState.IsEdited)
        {
            bool confirmed = await ConfirmDiscardChanges("Open dashboard");
            if (!confirmed) return;
        }
        var names = await DashboardService.ListDashboardsAsync();
        if (names.Count == 0)
        {
            Snackbar.Add("No saved dashboards found", Severity.Warning);
            return;
        }
        var parameters = new DialogParameters<DashboardPickerDialog>
        {
            { d => d.DiagramNames, names }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<DashboardPickerDialog>("Open Dashboard", parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: string name } && !string.IsNullOrWhiteSpace(name))
        {
            var state = await DashboardService.LoadDashboardByNameAsync(name);
            if (state != null)
            {
                AppState.ClearUndoRedo();
                var prevTopics = AppState.SubscribedTopics.ToHashSet();
                if (AppState.IsEditMode)
                {
                    _diagram?.SelectionChanged -= OnSelectionChanged;
                    _diagram?.Changed -= OnDiagramChanged;
                    UnsubscribeEditEvents();
                }
                LoadFullState(state, readOnly: !AppState.IsEditMode);
                await SyncSubscriptionsAsync(prevTopics, AppState.SubscribedTopics);
                if (AppState.IsEditMode && _diagram != null)
                {
                    _diagram.SelectionChanged += OnSelectionChanged;
                    _diagram.Changed += OnDiagramChanged;
                    SubscribeEditEvents();
                    UpdateSelectionState();
                }
                AppState.SetDiagramName(name);
                AppState.MarkSaved();
                await SaveLastDiagramName(name);
                var nodeCount = state.Pages?.Sum(p => p.Nodes.Count) ?? state.Nodes.Count;
                Snackbar.Add($"Opened '{name}' ({nodeCount} nodes)", Severity.Info);
                StateHasChanged();
                await Task.Delay(100);
                RefreshAll();
                StateHasChanged();
            }
            else
            {
                Snackbar.Add($"Failed to load '{name}'", Severity.Error);
            }
        }
    }

    private async Task<bool> SaveDashboard()
    {
        try
        {
            var state = BuildFullState();
            var success = await DashboardService.SaveDashboardByNameAsync(AppState.DiagramName, state);
            if (success)
            {
                AppState.MarkSaved();
                await SaveLastDiagramName(AppState.DiagramName);
                var nodeCount = state.Pages?.Sum(p => p.Nodes.Count) ?? state.Nodes.Count;
                var linkCount = state.Pages?.Sum(p => p.Links.Count) ?? state.Links.Count;
                Snackbar.Add($"Saved '{AppState.DiagramName}' ({nodeCount} nodes, {linkCount} links)", Severity.Success);
                return true;
            }
            else
            {
                Snackbar.Add($"Failed to save '{AppState.DiagramName}' — check server logs for details", Severity.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving '{AppState.DiagramName}': {ex.Message}", Severity.Error);
            return false;
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

    private string CanvasStyle =>
        string.IsNullOrEmpty(AppState.CanvasBackgroundColor)
            ? "width: 100%; height: calc(100vh - 100px); overflow: hidden;"
            : $"width: 100%; height: calc(100vh - 100px); overflow: hidden; background-color: {AppState.CanvasBackgroundColor};";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SubscribeToNodeChanges(NodeModel node)
    {
        if (_nodeChangedSubscriptions.Any(x => x.Node == node)) return;
        Action<Blazor.Diagrams.Core.Models.Base.Model> handler = _ => OnNodeChanged(node);
        node.Changed += handler;
        _nodeChangedSubscriptions.Add((node, handler));
    }

    private void OnNodeAddedInEditMode(Blazor.Diagrams.Core.Models.Base.Model model)
    {
        if (model is NodeModel node)
            SubscribeToNodeChanges(node);
    }

    private void OnNodeChanged(NodeModel node)
    {
        AppState.MarkEdited();
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastUndoPushByMove).TotalSeconds >= 1.5)
        {
            _lastUndoPushByMove = now;
            PushUndoSnapshot();
        }
        InvokeAsync(StateHasChanged);
    }

    private async Task ShowDiagramPropertiesAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        await DialogService.ShowAsync<DashboardPropertiesDialog>("Dashboard Properties", options);
    }

    private async Task SaveLastDiagramName(string name)
    {
        try { await JSRuntime.InvokeVoidAsync("localStorage.setItem", LastDiagramKey, name); }
        catch { /* ignore */ }
    }

    private async Task<string?> GetLastDiagramName()
    {
        try { return await JSRuntime.InvokeAsync<string?>("localStorage.getItem", LastDiagramKey); }
        catch { return null; }
    }

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

        // These are subscribed regardless of edit mode
        if (_onMenuOpen       != null) AppState.MenuOpen       -= _onMenuOpen;
        if (_onMenuSetActivePage != null) AppState.MenuSetActivePage -= _onMenuSetActivePage;

        if (AppState.IsEditMode)
        {
            Snackbar.Clear();
            AppState.SetEditMode(false);
            AppState.UpdateSelectionState(false, false);
            UnsubscribeEditEvents();
        }

        // Unsubscribe from ALL diagrams
        foreach (var d in _diagrams.OfType<BlazorDiagram>())
        {
            d.SelectionChanged -= OnSelectionChanged;
            d.Changed -= OnDiagramChanged;
        }

        GC.SuppressFinalize(this);
    }

    private record StartupSettingsDto(string Mode, string? Dashboard);
}
