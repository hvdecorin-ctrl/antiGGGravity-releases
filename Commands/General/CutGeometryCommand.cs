using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace antiGGGravity.Commands.General
{
    [Transaction(TransactionMode.Manual)]
    public class CutGeometryCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Get cutting element (Pre-selected or Picked)
            var selIds = uidoc.Selection.GetElementIds();
            Element cuttingElement = null;

            if (selIds.Count == 1)
            {
                cuttingElement = doc.GetElement(selIds.First());
            }
            else
            {
                try
                {
                    Reference pickedRef = uidoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element, 
                        "Select the CUTTING object (the element that will cut others)");
                    
                    if (pickedRef != null)
                    {
                        cuttingElement = doc.GetElement(pickedRef.ElementId);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (cuttingElement == null)
            {
                TaskDialog.Show("Cut Geometry", "No cutting element selected. Please select exactly ONE element.");
                return Result.Cancelled;
            }

            // 2. Find all elements that intersect with the cutting element
            // We use the active view to limit search to visible elements if preferred, 
            // but the user said "all elements intersected", typically implying model-wide or view-specific.
            // Let's go with model-wide filtering for intersection, then filtering by category/type.
            
            var intersectFilter = new ElementIntersectsElementFilter(cuttingElement);
            
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(intersectFilter)
                .Where(e => e.Id != cuttingElement.Id); // Skip the cutting element itself

            List<Element> elementsToCut = collector.ToList();

            if (!elementsToCut.Any())
            {
                TaskDialog.Show("Cut Geometry", "No intersecting elements found to cut.");
                return Result.Succeeded;
            }

            int successCount = 0;
            int alreadyCutCount = 0;
            int errorCount = 0;

            using (Transaction t = new Transaction(doc, "Cut Geometry Advance"))
            {
                t.Start();
                foreach (Element elToBeCut in elementsToCut)
                {
                    try
                    {
                        if (SolidSolidCutUtils.IsAllowedForSolidCut(elToBeCut) && 
                            SolidSolidCutUtils.IsAllowedForSolidCut(cuttingElement))
                        {
                            if (!SolidSolidCutUtils.CutExistsBetweenElements(elToBeCut, cuttingElement, out bool firstCutsSecond))
                            {
                                SolidSolidCutUtils.AddCutBetweenSolids(doc, elToBeCut, cuttingElement);
                                successCount++;
                            }
                            else
                            {
                                alreadyCutCount++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        errorCount++;
                    }
                }
                t.Commit();
            }

            TaskDialog.Show("Cut Geometry", 
                $"Process Complete.\n" +
                $"- Elements successfully cut: {successCount}\n" +
                $"- Elements already cut or skipped: {alreadyCutCount}\n" +
                $"- Errors: {errorCount}");

            return Result.Succeeded;
        }
    }
}
