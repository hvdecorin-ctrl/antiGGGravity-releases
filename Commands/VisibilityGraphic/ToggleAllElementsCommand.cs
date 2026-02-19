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
            var doc = commandData.Application.ActiveUIDocument.Document;
            var uidoc = commandData.Application.ActiveUIDocument;
            var activeView = doc.ActiveView;

            if (activeView == null) return Result.Failed;
            
            var selectedIds = uidoc.Selection.GetElementIds().ToList();
            
            using (var t = new Transaction(doc, "Toggle All Elements"))
            {
                t.Start();

                if (selectedIds.Any())
                {
                    // TOGGLE SELECTION: Filter to hideable elements
                    var selectedHideable = selectedIds
                        .Where(id => doc.GetElement(id).CanBeHidden(activeView))
                        .ToList();

                    if (selectedHideable.Any())
                    {
                        // Logic: If ANY selected element is visible, hide ALL selected. If ALL are hidden, unhide ALL.
                        bool anyVisible = selectedHideable.Any(id => !doc.GetElement(id).IsHidden(activeView));
                        
                        if (anyVisible)
                        {
                            activeView.HideElements(selectedHideable);
                        }
                        else
                        {
                            activeView.UnhideElements(selectedHideable);
                        }
                    }
                }
                else
                {
                    // GLOBAL TOGGLE: 
                    // 1. Check if ANY hideable elements are currently VISIBLE in the view
                    var visibleIds = new FilteredElementCollector(doc, activeView.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Where(e => e.Id != activeView.Id && e.CanBeHidden(activeView))
                        .Select(e => e.Id)
                        .ToList();

                    if (visibleIds.Any())
                    {
                        // Some are visible -> HIDE them
                        activeView.HideElements(visibleIds);
                    }
                    else
                    {
                        // None are visible -> UNHIDE everything in doc that is hidden in this view
                        var hiddenIds = new FilteredElementCollector(doc)
                            .WhereElementIsNotElementType()
                            .ToElements()
                            .Where(e => e.Id != activeView.Id && e.CanBeHidden(activeView) && e.IsHidden(activeView))
                            .Select(e => e.Id)
                            .ToList();

                        if (hiddenIds.Any())
                        {
                            activeView.UnhideElements(hiddenIds);
                        }
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
