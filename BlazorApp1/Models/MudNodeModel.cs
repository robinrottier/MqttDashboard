using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorApp1.Models
{
    public class MudNodeModel : NodeModel
    {
        public MudNodeModel(Point? position = null) : base(position)
        {
        }

        /// <summary>
        /// Icon name from MudBlazor Icons (e.g., Icons.Material.Filled.Home)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Secondary text displayed below the title
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Background color for the node
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Icon color
        /// </summary>
        public string? IconColor { get; set; }

        /// <summary>
        /// Custom metadata dictionary for future extensibility
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
