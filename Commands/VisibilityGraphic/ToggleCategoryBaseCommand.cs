using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public abstract class ToggleCategoryBaseCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        protected abstract BuiltInCategory Category { get; }

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            if (view == null) return Result.Failed;
            
            var categoryId = new ElementId(Category);
            var selectedIds = uidoc.Selection.GetElementIds();
            
            // Filter selection to elements of this category that can be hidden
            var selectedOfCategory = selectedIds
                .Select(id => doc.GetElement(id))
                .Where(e => e != null && e.Category != null && e.Category.Id == categoryId && e.CanBeHidden(view))
                .Select(e => e.Id)
                .ToList();

            using (var t = new Transaction(doc, "Toggle Category Elements"))
            {
                t.Start();

                if (selectedOfCategory.Any())
                {
                    // TOGGLE SELECTION: If any are visible, hide all; if all are hidden, unhide all.
                    bool anyVisible = selectedOfCategory.Any(id => !doc.GetElement(id).IsHidden(view));
                    if (anyVisible)
                    {
                        view.HideElements(selectedOfCategory);
                    }
                    else
                    {
                        view.UnhideElements(selectedOfCategory);
                    }
                }
                else
                {
                    // GLOBAL TOGGLE for Category: 
                    // 1. Check if ANY elements of this category are currently VISIBLE in the view
                    var visibleOfCategory = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(categoryId)
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    if (visibleOfCategory.Any())
                    {
                        // Some are visible -> HIDE ALL of this category in the view
                        view.HideElements(visibleOfCategory);
                    }
                    else
                    {
                        // None are visible -> UNHIDE ALL of this category in the document
                        var allOfCategory = new FilteredElementCollector(doc)
                            .OfCategoryId(categoryId)
                            .WhereElementIsNotElementType()
                            .ToElements()
                            .Where(e => e.CanBeHidden(view) && e.IsHidden(view))
                            .Select(e => e.Id)
                            .ToList();

                        if (allOfCategory.Any())
                        {
                            view.UnhideElements(allOfCategory);
                        }
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
