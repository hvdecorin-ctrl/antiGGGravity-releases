using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Commands;
using antiGGGravity.StructuralRebar.Core.Calculators;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Selection filter that only allows Rebar elements.
    /// </summary>
    public class RebarSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Autodesk.Revit.DB.Structure.Rebar;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    /// <summary>
    /// Rebar Cranked: Select a rebar, pick a point along it, and apply
    /// a cranked lap splice (1:6 slope, 1×db offset) at that location.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarCrankCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Select a rebar element
            Reference rebarRef;
            try
            {
                rebarRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RebarSelectionFilter(),
                    "Select a rebar element to crank");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            var rebar = doc.GetElement(rebarRef) as Autodesk.Revit.DB.Structure.Rebar;
            if (rebar == null) return Result.Failed;

            // 2. Pick a point for the crank location (on any element)
            XYZ pickPoint;
            try
            {
                Reference ptRef = uidoc.Selection.PickObject(
                    ObjectType.PointOnElement,
                    "Pick a point along the rebar for the crank location");
                pickPoint = ptRef.GlobalPoint;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            // 3. Read rebar properties
            var barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
            if (barType == null) return Result.Failed;

            double barDia = barType.BarNominalDiameter; // feet
            Element host = doc.GetElement(rebar.GetHostId());
            if (host == null)
            {
                TaskDialog.Show("Rebar Crank", "Cannot find the host element for this rebar.");
                return Result.Failed;
            }

            // Get the rebar curves (centerline path)
            var accessor = rebar.GetShapeDrivenAccessor();
            var curves = rebar.GetCenterlineCurves(false, false, false,
                MultiplanarOption.IncludeOnlyPlanarCurves, 0);

            if (curves == null || curves.Count == 0) return Result.Failed;

            // 4. Find the primary bar line (longest curve or first straight line)
            Line barLine = null;
            foreach (var c in curves)
            {
                if (c is Line line)
                {
                    if (barLine == null || line.Length > barLine.Length)
                        barLine = line;
                }
            }
            if (barLine == null)
            {
                TaskDialog.Show("Rebar Crank", "Could not find a straight segment in this rebar.");
                return Result.Failed;
            }

            // 5. Project the pick point onto the bar line to get the split parameter
            XYZ barStart = barLine.GetEndPoint(0);
            XYZ barEnd = barLine.GetEndPoint(1);
            XYZ barDir = (barEnd - barStart).Normalize();
            double barLength = barLine.Length;

            // Compute rebar normal: perpendicular to bar in the distribution plane
            XYZ normal = barDir.CrossProduct(XYZ.BasisZ);
            if (normal.IsZeroLength())
                normal = barDir.CrossProduct(XYZ.BasisX); // vertical bars
            normal = normal.Normalize();

            // Project pick point onto bar axis
            double splitDist = barDir.DotProduct(pickPoint - barStart);
            if (splitDist < 0.1 || splitDist > barLength - 0.1)
            {
                TaskDialog.Show("Rebar Crank", "Pick point is too close to the bar ends. Pick a point further along the bar.");
                return Result.Failed;
            }

            // 6. Calculate crank geometry
            double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
            double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
            // Use NZS3101 as default; could be made configurable
            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, DesignCodeStandard.NZS3101);
            double straightLap = lapLen + crankRun;

            // Determine crank direction: must be perpendicular to bar and point INWARD toward host center
            BoundingBoxXYZ hostBox = host.get_BoundingBox(null);
            XYZ crankDir;
            if (hostBox != null)
            {
                XYZ hostCenter = (hostBox.Min + hostBox.Max) / 2.0;
                XYZ barMid = (barStart + barEnd) / 2.0;
                
                // Vector from bar midpoint toward host center
                XYZ toCenter = hostCenter - barMid;
                
                // Remove the component along the bar direction (we only want perpendicular movement)
                XYZ perpToCenter = toCenter - barDir * toCenter.DotProduct(barDir);
                
                if (perpToCenter.GetLength() > 1e-9)
                {
                    crankDir = perpToCenter.Normalize();
                }
                else
                {
                    // Bar is at host center — use normal cross barDir as fallback
                    crankDir = normal.CrossProduct(barDir).Normalize();
                }
            }
            else
            {
                crankDir = normal.CrossProduct(barDir).Normalize();
            }

            // Ensure we have enough room for the crank
            double shortTolerance = doc.Application.ShortCurveTolerance * 1.5;
            double totalCrankZone = straightLap + crankRun;
            if (splitDist + totalCrankZone > barLength - shortTolerance)
            {
                // Not enough room after the split point: shift split back
                splitDist = barLength - totalCrankZone - shortTolerance;
                if (splitDist < 0.1)
                {
                    TaskDialog.Show("Rebar Crank", "Bar is too short for a cranked lap at this location.");
                    return Result.Failed;
                }
            }

            // 7. Read original hooks and layout
            RebarHookType hookStartType = null;
            RebarHookType hookEndType = null;
            var hookStartParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_START_TYPE);
            var hookEndParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_END_TYPE);
            if (hookStartParam != null && hookStartParam.AsElementId() != ElementId.InvalidElementId)
                hookStartType = doc.GetElement(hookStartParam.AsElementId()) as RebarHookType;
            if (hookEndParam != null && hookEndParam.AsElementId() != ElementId.InvalidElementId)
                hookEndType = doc.GetElement(hookEndParam.AsElementId()) as RebarHookType;

            var hookStartOrient = rebar.GetHookOrientation(0);
            var hookEndOrient = rebar.GetHookOrientation(1);

            // Read layout info from built-in parameters
            int barCount = rebar.NumberOfBarPositions;
            double spacing = 0;
            double distWidth = 0;

            // Read spacing from built-in parameter
            var spacingParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING);
            if (spacingParam != null) spacing = spacingParam.AsDouble();

            // Compute distribution width directly from physical positions
            if (barCount > 1)
            {
                try
                {
                    var acc = rebar.GetShapeDrivenAccessor();
                    XYZ posFirst = acc.GetBarPositionTransform(0).Origin;
                    XYZ posLast = acc.GetBarPositionTransform(barCount - 1).Origin;
                    distWidth = posFirst.DistanceTo(posLast);
                }
                catch
                {
                    // Fallback to spacing if transform fails
                    if (spacing > 0) distWidth = spacing * (barCount - 1);
                }
            }

            // 8. Build the two new bar segments
            XYZ splitPoint = barStart + barDir * splitDist;

            // Segment 1: straight from barStart to splitPoint (keeps original start hook)
            var curves1 = new List<Curve> { Line.CreateBound(barStart, splitPoint) };

            // Segment 2: cranked bar starts 'straightLap' BEFORE the split point to overlap Segment 1
            // Ensure we don't go backwards past the start of the bar
            double seg2BackDir = straightLap;
            if (seg2BackDir > splitDist) seg2BackDir = splitDist - 0.1;

            XYZ seg2Start = splitPoint - barDir * seg2BackDir; // start before split
            XYZ ptA = seg2Start + crankDir * crankOff;                                    // offset start
            XYZ ptB = ptA + barDir * seg2BackDir;                                         // end of straight overlap at offset (exactly at splitPoint if space allowed)
            XYZ ptC = seg2Start + barDir * (seg2BackDir + crankRun);                      // end of crank, back at main level
            var curves2 = new List<Curve>
            {
                Line.CreateBound(ptA, ptB),    // straight at offset level
                Line.CreateBound(ptB, ptC),    // angled crank (offset → main)
                Line.CreateBound(ptC, barEnd)  // straight at main level
            };

            // 9. Execute transaction
            using (Transaction t = new Transaction(doc, "Rebar Crank"))
            {
                t.Start();
                try
                {
                    // Delete original rebar
                    doc.Delete(rebar.Id);

                    // Create segment 1 (straight, with original start hook)
                    var rebar1 = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType,
                        hookStartType, null, // start hook only
                        host, normal, curves1,
                        hookStartOrient, RebarHookOrientation.Left,
                        true, true);

                    // Create segment 2 (cranked, with original end hook)
                    var rebar2 = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType,
                        null, hookEndType, // end hook only
                        host, normal, curves2,
                        RebarHookOrientation.Left, hookEndOrient,
                        true, true);

                    // Apply layout to both new rebars
                    if (rebar1 != null && rebar2 != null)
                    {
                        var acc1 = rebar1.GetShapeDrivenAccessor();
                        var acc2 = rebar2.GetShapeDrivenAccessor();

                        if (barCount > 1 && distWidth > 0)
                        {
                            acc1.SetLayoutAsFixedNumber(barCount, distWidth, true, true, true);
                            acc2.SetLayoutAsFixedNumber(barCount, distWidth, true, true, true);
                        }
                        else if (barCount > 1 && spacing > 0)
                        {
                            acc1.SetLayoutAsMaximumSpacing(spacing, barLine.Length, true, true, true);
                            acc2.SetLayoutAsMaximumSpacing(spacing, barLine.Length, true, true, true);
                        }
                    }

                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show("Rebar Crank", $"Failed to apply crank:\n{ex.Message}");
                    return Result.Failed;
                }
            }

            return Result.Succeeded;
        }
    }
}
