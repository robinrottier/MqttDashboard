using BlazorApp1.Models;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace BlazorApp1.Components;

public partial class NodePropertyEditor
{
    //[CascadingParameter] private dynamic? MudDialog { get; set; }
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public MudNodeModel Node { get; set; } = default!;

    private double Width { get; set; }
    private double Height { get; set; }
    private int _newMetadataCounter = 1;

    protected override void OnInitialized()
    {
        Width = Node.Size?.Width ?? 120;
        Height = Node.Size?.Height ?? 90;
    }

    private void UpdateMetadata(string key, string value)
    {
        Node.Metadata[key] = value;
    }

    private void RemoveMetadata(string key)
    {
        Node.Metadata.Remove(key);
    }

    private void AddMetadata()
    {
        var newKey = $"Property{_newMetadataCounter++}";
        while (Node.Metadata.ContainsKey(newKey))
        {
            newKey = $"Property{_newMetadataCounter++}";
        }
        Node.Metadata[newKey] = "";
    }

    private void Save()
    {
        // Update size
        Node.Size = new Blazor.Diagrams.Core.Geometry.Size(Width, Height);

        // Refresh the node to update the display
        Node.Refresh();

        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog?.Cancel();
    }
}
