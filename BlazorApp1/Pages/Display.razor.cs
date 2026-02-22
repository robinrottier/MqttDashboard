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
                _diagram = AppState.CreateDiagramFromState(savedState, true);

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
                _diagram = AppState.CreateDiagramFromState(null, true);
                StateHasChanged();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public void Dispose() { }
}
