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

            // The reliable SetCategoryHidden method can directly use BuiltInCategory
            using (var t = new Transaction(doc, "Toggle Category"))
            {
                t.Start();

                ElementId catId = new ElementId(Category);
                if (view.CanCategoryBeHidden(catId))
                {
                    bool shouldHide = !view.GetCategoryHidden(catId);
                    view.SetCategoryHidden(catId, shouldHide);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
