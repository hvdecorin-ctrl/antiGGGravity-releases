using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleAllElementsCommand : BaseCommand
    {
        protected override bool RequiresLicense => false; // As requested, simple utility

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            if (view == null) return Result.Failed;

            // Collect MUST be document-wide to find hidden elements. 
            // View-specific collectors only find already visible elements.
            var allElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            if (!allElements.Any()) return Result.Succeeded;

            // Check if any element is hidden to decide logic
            bool anyHidden = allElements.Any(e => e.IsHidden(view));

            using (var t = new Transaction(doc, "Toggle All Elements"))
            {
                t.Start();

                if (anyHidden)
                {
                    // Unhide everything
                    var idsToUnhide = allElements.Select(e => e.Id).ToList();
                    view.UnhideElements(idsToUnhide);
                }
                else
                {
                    // Hide everything that CAN be hidden
                    var idsToHide = allElements
                        .Where(e => e.Id != view.Id && e.CanBeHidden(view))
                        .Select(e => e.Id)
                        .ToList();

                    if (idsToHide.Any())
                    {
                        view.HideElements(idsToHide);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
