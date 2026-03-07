using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Selection filter that allows Grid, Wall, Column, Foundation, and Group categories.
    /// Matches the Python CategorySelectionFilter.
    /// </summary>
    public class AutoDimSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<string> _allowed;

        public AutoDimSelectionFilter(AutoDimSettings settings)
        {
            _allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (settings.DimGrids) _allowed.Add("grid");
            if (settings.DimWalls) _allowed.Add("wall");
            if (settings.DimColumns) _allowed.Add("column");
            if (settings.DimFoundations) _allowed.Add("foundation");
        }

        public bool AllowElement(Element elem)
        {
            try
            {
                if (elem.Category == null) return false;
                string catName = elem.Category.Name.ToLower();

                // Groups must always be allowed so nested elements can be extracted
                if (catName.Contains("group")) return true;

                // Grids always allowed as references
                if (catName.Contains("grids") && _allowed.Contains("grid")) return true;

                if (_allowed.Contains("wall") && catName.Contains("walls")) return true;
                if (_allowed.Contains("foundation") && (catName.Contains("foundation") || catName.Contains("pads"))) return true;
                if (_allowed.Contains("column") && (catName.Contains("column") || catName.Contains("framing"))) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
