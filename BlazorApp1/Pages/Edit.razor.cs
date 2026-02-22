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
    private bool _gridSnapEnabled = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppState.SetInteractive();

            // Check if diagram already exists in memory (user navigated back to this page)
            _diagram = AppState.GetOrCreateDiagram();

            // Only load from server if diagram is empty (no nodes)
            if (_diagram.Nodes.Count == 0)
            {
                // Try to load saved diagram from server
                var savedState = await DiagramService.LoadDiagramAsync();

                if (savedState != null && savedState.Nodes.Count > 0)
                {
                    // Load from saved state
                    AppState.ResetDiagram(); // Clear the empty diagram first
                    _diagram = AppState.CreateDiagramFromState(savedState, false);
                    _gridSnapEnabled = _diagram.Options.GridSize != null;

                    Snackbar.Add("Diagram loaded from server", Severity.Info);

                    // Trigger initial render
                    StateHasChanged();

                    // Wait for Blazor to render the nodes to the DOM, then refresh links
                    await Task.Delay(100); // Small delay to allow DOM rendering

                    // Now refresh everything so links calculate their paths correctly
                    foreach (var node in _diagram.Nodes)
                    {
                        node.Refresh();
                    }

                    foreach (var link in _diagram.Links)
                    {
                        link.Refresh();
                    }

                    _diagram.Refresh();
                    StateHasChanged();
                }
                // else: diagram stays empty (already created by GetOrCreateDiagram)
            }
            // else: diagram already exists in memory, use it as-is

            // Subscribe to selection changes to update button states
            _diagram.SelectionChanged += OnSelectionChanged;

            // Also subscribe to diagram changes for other updates
            _diagram.Changed += OnDiagramChanged;

            UpdateSelectionState();
            StateHasChanged();

            // make sure grid enabled reflects setting in diagram state
            _gridSnapEnabled = _diagram.Options.GridSize != null;

        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private void OnSelectionChanged(object model)
    {
        UpdateSelectionState();
        InvokeAsync(StateHasChanged);
    }

    private void OnDiagramChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void UpdateSelectionState()
    {
        var selectedNodes = _diagram?.GetSelectedModels()
            .OfType<NodeModel>()
            .ToList() ?? [];

        _hasSelectedNode = selectedNodes.Count > 0;
        _hasSingleSelectedNode = selectedNodes.Count == 1;
    }

    private void AddNode()
    {
        if (_diagram == null) return;

        var random = new Random();
        var x = random.Next(50, 500);
        var y = random.Next(50, 400);

        // Deselect all nodes first
        _diagram.UnselectAll();

        var node = _diagram.Nodes.Add(new MudNodeModel(position: new Point(x, y))
        {
            Title = $"Node {_nodeCounter++}"
        });

        // Select the newly created node
        _diagram.SelectModel(node, false);

        UpdateSelectionState();
        StateHasChanged();
    }

    private void DeleteSelectedNode()
    {
        if (_diagram == null) return;

        var selectedNodes = _diagram.GetSelectedModels()
            .OfType<NodeModel>()
            .ToList();

        foreach (var node in selectedNodes)
        {
            _diagram.Nodes.Remove(node);
        }

        UpdateSelectionState();
        StateHasChanged();
    }

    private bool HasPortAlignment(PortAlignment alignment)
    {
        if (_diagram == null) return false;

        var selectedNode = _diagram.GetSelectedModels()
            .OfType<NodeModel>()
            .FirstOrDefault();

        if (selectedNode == null) return false;

        return selectedNode.Ports.Any(p => p.Alignment == alignment);
    }

    private void AddPortToSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;

        var selectedNode = _diagram.GetSelectedModels()
            .OfType<NodeModel>()
            .FirstOrDefault();

        if (selectedNode != null && !HasPortAlignment(alignment))
        {
            AppState.AddPortToNode(selectedNode, alignment);
            //selectedNode.AddPort(alignment);
            selectedNode.Refresh();
            StateHasChanged();
        }
    }

    private void DeletePortFromSelectedNode(PortAlignment alignment)
    {
        if (_diagram == null) return;

        var selectedNode = _diagram.GetSelectedModels()
            .OfType<NodeModel>()
            .FirstOrDefault();

        if (selectedNode != null)
        {
            var portToRemove = selectedNode.Ports.FirstOrDefault(p => p.Alignment == alignment);
            if (portToRemove != null)
            {
                selectedNode.RemovePort(portToRemove);
                selectedNode.Refresh();
                StateHasChanged();
            }
        }
    }

    private async Task SaveDiagram()
    {
        try
        {
            var state = AppState.GetDiagramState();
            var success = await DiagramService.SaveDiagramAsync(state);

            if (success)
            {
                Snackbar.Add($"Diagram saved successfully ({state.Nodes.Count} nodes, {state.Links.Count} links)", Severity.Success);
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

    private async Task EditNodeProperties()
    {
        if (_diagram == null) return;

        var selectedNode = _diagram.GetSelectedModels()
            .OfType<MudNodeModel>()
            .FirstOrDefault();

        if (selectedNode == null)
        {
            Snackbar.Add("No node selected", Severity.Warning);
            return;
        }

        var parameters = new DialogParameters
        {
            { "Node", selectedNode }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            BackdropClick = true
        };

        var dialog = await DialogService.ShowAsync<NodePropertyEditor>("Edit Node Properties", parameters, options);
        var result = await dialog.Result;

        if (result == null || result.Canceled)
        {
            return;
        }
        // Refresh the diagram to show updates
        StateHasChanged();
        Snackbar.Add("Node properties updated", Severity.Success);
    }

    private void ToggleGridSnap()
    {
        if (_diagram == null) return;

        _gridSnapEnabled = !_gridSnapEnabled;

        if (_gridSnapEnabled)
        {
            _diagram.Options.GridSize = 20;
        }
        else
        {
            _diagram.Options.GridSize = null;
        }

        var message = _gridSnapEnabled 
            ? "Grid snap enabled - nodes will snap to grid when moved" 
            : "Grid snap disabled";

        Snackbar.Add(message, _gridSnapEnabled ? Severity.Success : Severity.Info);
        StateHasChanged();
        _diagram.Refresh();
    }

    public void Dispose()
    {
        // Clear any active snackbars to prevent them from persisting on navigation
        Snackbar.Clear();

        if (_diagram != null)
        {
            _diagram.SelectionChanged -= OnSelectionChanged;
            _diagram.Changed -= OnDiagramChanged;
        }
    }
}
