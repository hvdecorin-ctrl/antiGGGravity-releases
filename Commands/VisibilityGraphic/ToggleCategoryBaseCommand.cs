using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public abstract class ToggleCategoryBaseCommand : BaseCommand
    {

        protected abstract BuiltInCategory Category { get; }

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            if (view == null) return Result.Failed;

            // Get elements of the specific category currently visible in the view
            var visibleIds = new FilteredElementCollector(doc, view.Id)
                .OfCategory(Category)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Where(id => doc.GetElement(id).CanBeHidden(view))
                .ToList();

            using (var t = new Transaction(doc, "Toggle Elements Visibility"))
            {
                t.Start();

                if (visibleIds.Any())
                {
                    // If any are visible, hide them all
                    view.HideElements(visibleIds);
                }
                else
                {
                    // If none are visible, find all hidden elements of this category in the document
                    // and try to unhide them in this view.
                    var allCatElements = new FilteredElementCollector(doc)
                        .OfCategory(Category)
                        .WhereElementIsNotElementType();

                    var hiddenIds = new List<ElementId>();
                    foreach (var elem in allCatElements)
                    {
                        if (elem.IsHidden(view) && elem.CanBeHidden(view))
                        {
                            hiddenIds.Add(elem.Id);
                        }
                    }

                    if (hiddenIds.Any())
                    {
                        view.UnhideElements(hiddenIds);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
