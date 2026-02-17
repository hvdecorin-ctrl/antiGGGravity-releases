using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Management
{
    [Transaction(TransactionMode.Manual)]
    public class RenameViewsAllSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (Transaction t = new Transaction(doc, "Rename Views (All Sheets)"))
            {
                t.Start();
                try
                {
                    HashSet<string> existingViewNames = new HashSet<string>(
                        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name)
                    );

                    var allSheets = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>();

                    foreach (ViewSheet sheet in allSheets)
                    {
                        ViewRenamingLogic.RenameViewsOnSheet(sheet, existingViewNames);
                        ViewRenamingLogic.SetViewportTitles(sheet);
                    }

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
