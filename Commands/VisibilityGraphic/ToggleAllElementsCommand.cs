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
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var uidoc = commandData.Application.ActiveUIDocument;
            var activeView = doc.ActiveView;

            if (activeView == null) return Result.Failed;

            // Check for Selection first
            var selectedIds = uidoc.Selection.GetElementIds();
            var selectedHideable = selectedIds.Where(id => doc.GetElement(id).CanBeHidden(activeView)).ToList();

            // Collect Visible Elements in View (for "Hide All" logic)
            var visibleColl = new FilteredElementCollector(doc, activeView.Id).WhereElementIsNotElementType();
            var visibleIds = visibleColl.ToElementIds().Where(id => doc.GetElement(id).CanBeHidden(activeView)).ToList();

            using (var t = new Transaction(doc, "Toggle Visibility"))
            {
                t.Start();

                if (selectedHideable.Any())
                {
                    // TOGGLE SELECTION
                    activeView.HideElements(selectedHideable);
                }
                else if (visibleIds.Any())
                {
                    // HIDE ALL VISIBLE
                    activeView.HideElements(visibleIds);
                }
                else
                {
                    // UNHIDE ALL
                    var allColl = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                    var hiddenIds = new List<ElementId>();

                    foreach (var elem in allColl)
                    {
                        if (elem.Id != activeView.Id && elem.IsHidden(activeView) && elem.CanBeHidden(activeView))
                        {
                            hiddenIds.Add(elem.Id);
                        }
                    }

                    if (hiddenIds.Any())
                    {
                        activeView.UnhideElements(hiddenIds);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
