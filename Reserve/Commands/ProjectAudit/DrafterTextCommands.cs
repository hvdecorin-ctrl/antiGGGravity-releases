using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class DrafterTextOffCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            const string keyword = "DRAFTER";

            try
            {
                var textNotes = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TextNotes)
                    .WhereElementIsNotElementType()
                    .Cast<TextNote>()
                    .Where(n => n.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(n => n.Id)
                    .ToList();

                if (!textNotes.Any()) return Result.Succeeded;

                using (Transaction t = new Transaction(doc, "Hide Drafter Text"))
                {
                    t.Start();
                    
                    var views = new FilteredElementCollector(doc)
                        .WherePasses(new ElementClassFilter(typeof(View)))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser);

                    foreach (View view in views)
                    {
                        try { view.HideElements(textNotes); } catch { }
                    }

                    var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
                    foreach (ViewSheet sheet in sheets)
                    {
                        try { sheet.HideElements(textNotes); } catch { }
                    }

                    t.Commit();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class DrafterTextOnCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            const string keyword = "DRAFTER";

            try
            {
                var textNotes = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TextNotes)
                    .WhereElementIsNotElementType()
                    .Cast<TextNote>()
                    .Where(n => n.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(n => n.Id)
                    .ToList();

                if (!textNotes.Any()) return Result.Succeeded;

                using (Transaction t = new Transaction(doc, "Unhide Drafter Text"))
                {
                    t.Start();
                    
                    var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate);
                    foreach (View view in views)
                    {
                        try { view.UnhideElements(textNotes); } catch { }
                    }

                    var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
                    foreach (ViewSheet sheet in sheets)
                    {
                        try { sheet.UnhideElements(textNotes); } catch { }
                    }

                    t.Commit();
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
