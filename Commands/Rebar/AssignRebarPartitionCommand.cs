using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Auto-assigns each rebar's Partition parameter from its host element's Mark.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MarkToPartitionCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Mark → Partition", "Please open a project first.");
                return Result.Cancelled;
            }

            return AssignPartitionFromHost(doc, uiDoc, "Mark", BuiltInParameter.ALL_MODEL_MARK);
        }

        private Result AssignPartitionFromHost(Document doc, UIDocument uiDoc, string sourceName, BuiltInParameter sourceParam)
        {
            // Use selection if available, otherwise all rebar in project
            var selectedIds = uiDoc.Selection.GetElementIds();
            IEnumerable<Element> rebarElements;

            if (selectedIds.Count > 0)
            {
                rebarElements = selectedIds.Select(id => doc.GetElement(id))
                    .Where(e => e != null && e.IsValidObject && !(e is ElementType))
                    .Where(e => e.Category != null && e.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Rebar)
                    .ToList();
            }
            else
            {
                rebarElements = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rebar)
                    .WhereElementIsNotElementType()
                    .Where(e => e.IsValidObject)
                    .ToList();
            }

            var rebarList = rebarElements.ToList();
            if (rebarList.Count == 0)
            {
                TaskDialog.Show("Mark → Partition", "No rebar found in the current selection or project.");
                return Result.Succeeded;
            }

            int updated = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, $"Assign Partition from {sourceName}"))
            {
                t.Start();

                foreach (var elem in rebarList)
                {
                    try
                    {
                        // Get host element
                        ElementId hostId = GetRebarHostId(elem);
                        if (hostId == null || hostId == ElementId.InvalidElementId)
                        {
                            skipped++;
                            continue;
                        }

                        Element host = doc.GetElement(hostId);
                        if (host == null)
                        {
                            skipped++;
                            continue;
                        }

                        // Read source value from host
                        Parameter srcParam = host.get_Parameter(sourceParam);
                        string value = srcParam?.AsString();

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            skipped++;
                            continue;
                        }

                        // Write to rebar's Partition parameter
                        Parameter partParam = elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM);
                        if (partParam != null && !partParam.IsReadOnly)
                        {
                            partParam.Set(value);
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

                if (updated > 0)
                    t.Commit();
                else
                    t.RollBack();
            }

            TaskDialog.Show("Mark → Partition",
                $"Partition assignment complete!\n\n" +
                $"  ✓ Updated: {updated} rebar\n" +
                $"  ⚠ Skipped: {skipped} (no host Mark or read-only)\n\n" +
                $"  Source: Host {sourceName}");

            return Result.Succeeded;
        }

        private static ElementId GetRebarHostId(Element rebarElem)
        {
            if (rebarElem is Autodesk.Revit.DB.Structure.Rebar rebar)
                return rebar.GetHostId();
            if (rebarElem is RebarInSystem ris)
                return ris.GetHostId();
            return ElementId.InvalidElementId;
        }
    }

    /// <summary>
    /// Auto-assigns each rebar's Partition parameter from its host element's "Element Name" shared parameter.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ElementNameToPartitionCommand : BaseCommand
    {
        private const string ParamName = "Element Name";

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("ElementName → Partition", "Please open a project first.");
                return Result.Cancelled;
            }

            // Use selection if available, otherwise all rebar in project
            var selectedIds = uiDoc.Selection.GetElementIds();
            IEnumerable<Element> rebarElements;

            if (selectedIds.Count > 0)
            {
                rebarElements = selectedIds.Select(id => doc.GetElement(id))
                    .Where(e => e != null && e.IsValidObject && !(e is ElementType))
                    .Where(e => e.Category != null && e.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Rebar)
                    .ToList();
            }
            else
            {
                rebarElements = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rebar)
                    .WhereElementIsNotElementType()
                    .Where(e => e.IsValidObject)
                    .ToList();
            }

            var rebarList = rebarElements.ToList();
            if (rebarList.Count == 0)
            {
                TaskDialog.Show("ElementName → Partition", "No rebar found in the current selection or project.");
                return Result.Succeeded;
            }

            int updated = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, "Assign Partition from Element Name"))
            {
                t.Start();

                foreach (var elem in rebarList)
                {
                    try
                    {
                        // Get host element
                        ElementId hostId = GetRebarHostId(elem);
                        if (hostId == null || hostId == ElementId.InvalidElementId)
                        {
                            skipped++;
                            continue;
                        }

                        Element host = doc.GetElement(hostId);
                        if (host == null)
                        {
                            skipped++;
                            continue;
                        }

                        // Read "Element Name" shared parameter from host
                        Parameter srcParam = host.LookupParameter(ParamName);
                        string value = srcParam?.AsString();

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            skipped++;
                            continue;
                        }

                        // Write to rebar's Partition parameter
                        Parameter partParam = elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM);
                        if (partParam != null && !partParam.IsReadOnly)
                        {
                            partParam.Set(value);
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

                if (updated > 0)
                    t.Commit();
                else
                    t.RollBack();
            }

            TaskDialog.Show("ElementName → Partition",
                $"Partition assignment complete!\n\n" +
                $"  ✓ Updated: {updated} rebar\n" +
                $"  ⚠ Skipped: {skipped} (no host Element Name or read-only)\n\n" +
                $"  Source: Host \"{ParamName}\"");

            return Result.Succeeded;
        }

        private static ElementId GetRebarHostId(Element rebarElem)
        {
            if (rebarElem is Autodesk.Revit.DB.Structure.Rebar rebar)
                return rebar.GetHostId();
            if (rebarElem is RebarInSystem ris)
                return ris.GetHostId();
            return ElementId.InvalidElementId;
        }
    }
}
