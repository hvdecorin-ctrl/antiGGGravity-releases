using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.General.AutoDimension;

namespace antiGGGravity.Commands.General
{
    [Transaction(TransactionMode.Manual)]
    public class DimAuditCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (view is not ViewPlan && view is not ViewSection)
            {
                TaskDialog.Show("Dim Audit", "Please open a plan, section, or elevation view.");
                return Result.Cancelled;
            }

            // Get selected dimensions, or all dimensions in view if none selected
            var selIds = uidoc.Selection.GetElementIds();
            var dims = new List<Dimension>();

            if (selIds.Count > 0)
            {
                foreach (var id in selIds)
                {
                    if (doc.GetElement(id) is Dimension dim && dim.DimensionShape == DimensionShape.Linear)
                    {
                        dims.Add(dim);
                    }
                }
                if (dims.Count == 0)
                {
                    TaskDialog.Show("Dim Audit", "Selected elements do not contain any linear dimensions.");
                    return Result.Cancelled;
                }
            }
            else
            {
                dims = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .Where(d => d.DimensionShape == DimensionShape.Linear)
                    .ToList();
            }

            if (dims.Count == 0)
            {
                TaskDialog.Show("Dim Audit", "No linear dimensions found in the active view.");
                return Result.Cancelled;
            }

            using (var t = new Transaction(doc, "Dim Audit"))
            {
                t.Start();
                AutoDimCore.AuditAndFixDimensions(doc, view, dims);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
