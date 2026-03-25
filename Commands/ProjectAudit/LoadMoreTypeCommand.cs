using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ProjectAudit;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class LoadMoreTypeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                var selection = uidoc.Selection.GetElementIds();
                if (!selection.Any())
                {
                    TaskDialog.Show("Load More Type", "Select a family instance first.");
                    return Result.Cancelled;
                }

                Element elem = doc.GetElement(selection.First());
                Family family = null;

                if (elem is FamilySymbol symbol) family = symbol.Family;
                else if (elem is FamilyInstance instance) family = instance.Symbol.Family;

                if (family == null || family.IsEditable == false)
                {
                    TaskDialog.Show("Load More Type", "System families do not have external type definitions.");
                    return Result.Cancelled;
                }

                Document famDoc = doc.EditFamily(family);
                string famPath = famDoc.PathName;
                famDoc.Close(false);

                if (string.IsNullOrEmpty(famPath) || !File.Exists(famPath))
                {
                    TaskDialog.Show("Error", "Could not find original family file at:\n" + famPath);
                    return Result.Failed;
                }

                // Get symbols currently in project
                HashSet<string> loadedSymbols = new HashSet<string>(family.GetFamilySymbolIds().Select(id => doc.GetElement(id).Name));

                // Fetch ALL symbols from file using a temporary transaction
                List<string> allSymbols = new List<string>();
                using (Transaction t = new Transaction(doc, "Temp Load"))
                {
                    t.Start();
                    if (doc.LoadFamily(famPath, out Family tempFamily))
                    {
                        foreach (ElementId id in tempFamily.GetFamilySymbolIds())
                        {
                            allSymbols.Add(doc.GetElement(id).Name);
                        }
                    }
                    t.RollBack();
                }

                var options = allSymbols.Except(loadedSymbols).OrderBy(s => s).ToList();
                if (!options.Any())
                {
                    TaskDialog.Show("Load More Type", "All types are already loaded.");
                    return Result.Succeeded;
                }

                var loadHandler = new LoadFamilyTypesHandler();
                var loadEvent = ExternalEvent.Create(loadHandler);

                var view = new LoadMoreTypeView(family.Name, famPath, options, loadEvent, loadHandler);
                view.Show();

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
