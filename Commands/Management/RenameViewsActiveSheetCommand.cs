using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Management
{
    [Transaction(TransactionMode.Manual)]
    public class RenameViewsActiveSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (!(doc.ActiveView is ViewSheet currentSheet))
            {
                TaskDialog.Show("Sheet Required", "Please open a Sheet View to run this command.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Rename Views (Active Sheet)"))
            {
                t.Start();
                try
                {
                    HashSet<string> existingViewNames = new HashSet<string>(
                        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name)
                    );

                    ViewRenamingLogic.RenameViewsOnSheet(currentSheet, existingViewNames);
                    ViewRenamingLogic.SetViewportTitles(currentSheet);

                    t.Commit();
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    t.RollBack();
                    return Result.Failed;
                }
            }
        }
    }
}
