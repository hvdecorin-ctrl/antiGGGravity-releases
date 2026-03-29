using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    [Transaction(TransactionMode.Manual)]
    public class HighlightCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            // Get pre-selected elements
            var selection = uidoc.Selection.GetElementIds();
            if (selection.Count == 0)
            {
                TaskDialog.Show("Highlight", "Please select one or more elements first, then run Highlight.");
                return Result.Cancelled;
            }

            var selectedIds = new HashSet<ElementId>(selection);

            // Get Solid Fill Pattern for color overrides
            FillPatternElement solidFill = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

            using (Transaction t = new Transaction(doc, "Highlight Selection"))
            {
                t.Start();

                // Reset any existing temporary isolation
                try { view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate); } catch { }

                // Step 1: Make ALL visible model elements transparent
                var transparentOgs = new OverrideGraphicSettings();
                transparentOgs.SetSurfaceTransparency(80);
                transparentOgs.SetHalftone(true);

                var allVisibleElements = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .WhereElementIsViewIndependent();

                foreach (Element e in allVisibleElements)
                {
                    if (e.Category == null) continue;
                    if (e.Category.CategoryType != CategoryType.Model) continue;

                    view.SetElementOverrides(e.Id, transparentOgs);
                }

                // Step 2: Highlight selected elements — clear transparency + apply highlight color
                var highlightColor = new Color(0, 180, 255); // Bright cyan-blue highlight

                foreach (var id in selectedIds)
                {
                    var colorOgs = new OverrideGraphicSettings();
                    colorOgs.SetSurfaceTransparency(0);
                    colorOgs.SetHalftone(false);
                    colorOgs.SetSurfaceForegroundPatternColor(highlightColor);
                    colorOgs.SetCutForegroundPatternColor(highlightColor);

                    if (solidFill != null)
                    {
                        colorOgs.SetSurfaceForegroundPatternId(solidFill.Id);
                        colorOgs.SetCutForegroundPatternId(solidFill.Id);
                    }

                    view.SetElementOverrides(id, colorOgs);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
