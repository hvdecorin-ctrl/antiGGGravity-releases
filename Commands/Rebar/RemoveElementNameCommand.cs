using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Wipes out existing "Element Name" shared parameter values for all architectural and structural elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RemoveElementNameCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        private const string ParamName = "Element Name";

        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Rooms
        };

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument?.Document;

            if (doc == null) return Result.Cancelled;

            // Ask scope
            var dlg = new TaskDialog("Remove Existing Element Names");
            dlg.MainInstruction = "Wipe out all Element Name values?";
            dlg.MainContent = "This will clear the 'Element Name' parameter for all selected categories (Foundations, Walls, Doors, Windows, Rooms, etc.).\n\nChoose scope:";
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Active View Only");
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Entire Project");
            dlg.CommonButtons = TaskDialogCommonButtons.Cancel;

            var result = dlg.Show();
            if (result == TaskDialogResult.Cancel) return Result.Cancelled;

            bool activeViewOnly = (result == TaskDialogResult.CommandLink1);

            // Collect elements
            var allElements = new List<Element>();
            foreach (var bic in TargetCategories)
            {
                FilteredElementCollector collector;
                if (activeViewOnly)
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                else
                    collector = new FilteredElementCollector(doc);

                allElements.AddRange(collector.OfCategory(bic).WhereElementIsNotElementType().ToList());
            }

            if (allElements.Count == 0)
            {
                TaskDialog.Show("Remove Element Name", "No relevant elements found in the selected scope.");
                return Result.Succeeded;
            }

            int updated = 0;
            using (Transaction t = new Transaction(doc, "Remove Existing Element Names"))
            {
                t.Start();
                foreach (var elem in allElements)
                {
                    Parameter p = elem.LookupParameter(ParamName);
                    if (p != null && !p.IsReadOnly)
                    {
                        p.Set("");
                        updated++;
                    }
                }
                t.Commit();
            }

            TaskDialog.Show("Remove Element Name", $"Successfully cleared Element Name values for {updated} elements.");
            return Result.Succeeded;
        }
    }
}
