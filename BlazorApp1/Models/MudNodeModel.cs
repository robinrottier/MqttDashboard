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

    }
}
