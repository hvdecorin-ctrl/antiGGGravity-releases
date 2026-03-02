using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Tool 3: Assigns rebar Host Mark from the host element's "Element Name" parameter.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignHostMarkCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        private const string ParamName = "Element Name";

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Assign Host Mark", "Please open a project first.");
                return Result.Cancelled;
            }

            // Ask scope
            var dlg = new TaskDialog("Assign Host Mark");
            dlg.MainInstruction = "Assign Host Mark from Element Name";
            dlg.MainContent =
                "This will copy each host element's \"Element Name\" value into the rebar's Host Mark parameter.\n\n" +
                "Choose scope:";
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Active View Only");
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Entire Project");
            dlg.CommonButtons = TaskDialogCommonButtons.Cancel;

            var result = dlg.Show();
            if (result == TaskDialogResult.Cancel) return Result.Cancelled;

            bool activeViewOnly = (result == TaskDialogResult.CommandLink1);

            // Collect rebars
            FilteredElementCollector rebarCollector;
            if (activeViewOnly)
                rebarCollector = new FilteredElementCollector(doc, doc.ActiveView.Id);
            else
                rebarCollector = new FilteredElementCollector(doc);

            var rebars = rebarCollector
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .ToList();

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Assign Host Mark",
                    activeViewOnly
                        ? "No rebar elements found in the active view."
                        : "No rebar elements found in the project.");
                return Result.Succeeded;
            }

            int updated = 0;
            int skipped = 0;
            int noHost = 0;
            int noElementName = 0;

            using (Transaction t = new Transaction(doc, "Assign Host Mark from Element Name"))
            {
                t.Start();

                foreach (var elem in rebars)
                {
                    try
                    {
                        // Get host ID
                        ElementId hostId = null;

                        if (elem is Autodesk.Revit.DB.Structure.Rebar rebar)
                        {
                            hostId = rebar.GetHostId();
                        }
                        else if (elem is RebarInSystem rebarInSystem)
                        {
                            hostId = rebarInSystem.GetHostId();
                        }
                        else
                        {
                            // Try via parameter
                            var hostParam = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_HOST_MARK);
                            if (hostParam == null)
                            {
                                skipped++;
                                continue;
                            }
                        }

                        if (hostId == null || hostId == ElementId.InvalidElementId)
                        {
                            noHost++;
                            continue;
                        }

                        // Get host element
                        Element host = doc.GetElement(hostId);
                        if (host == null)
                        {
                            noHost++;
                            continue;
                        }

                        // Read host's "Element Name" 
                        Parameter elementNameParam = host.LookupParameter(ParamName);
                        if (elementNameParam == null || !elementNameParam.HasValue ||
                            string.IsNullOrWhiteSpace(elementNameParam.AsString()))
                        {
                            noElementName++;
                            continue;
                        }

                        string elementName = elementNameParam.AsString();

                        // Write to rebar's Host Mark
                        Parameter hostMarkParam = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_HOST_MARK);
                        if (hostMarkParam != null && !hostMarkParam.IsReadOnly)
                        {
                            hostMarkParam.Set(elementName);
                            updated++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                t.Commit();
            }

            // Show summary
            string summary = $"Host Mark Assignment Complete\n\n" +
                             $"  ✓ Updated: {updated} rebars\n" +
                             $"  ⚠ No host found: {noHost}\n" +
                             $"  ⚠ No Element Name on host: {noElementName}\n" +
                             $"  ⊘ Skipped: {skipped}";

            TaskDialog.Show("Assign Host Mark", summary);

            return Result.Succeeded;
        }
    }
}
