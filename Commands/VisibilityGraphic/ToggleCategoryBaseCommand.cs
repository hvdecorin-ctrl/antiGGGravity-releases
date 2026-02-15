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

            // Collect all elements of the category in the document
            var collector = new FilteredElementCollector(doc)
                .OfCategory(Category)
                .WhereElementIsNotElementType()
                .ToElements();

            if (!collector.Any()) return Result.Succeeded;

            // Separate visible and hidden (ensure they CAN be hidden in this view)
            var hidden = collector.Where(e => e.IsHidden(view)).ToList();
            var visible = collector.Where(e => !e.IsHidden(view) && e.CanBeHidden(view)).ToList();

            if (!hidden.Any() && !visible.Any()) return Result.Succeeded;

            using (var t = new Transaction(doc, $"Toggle {Category}"))
            {
                t.Start();

                if (hidden.Any())
                {
                    view.UnhideElements(hidden.Select(e => e.Id).ToList());
                }
                else if (visible.Any())
                {
                    view.HideElements(visible.Select(e => e.Id).ToList());
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
