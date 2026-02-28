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
    /// Rebar Split: Select a rebar, pick a point along it, and split it into two bars
    /// that lap according to code, without any lateral offset (crank).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarSplitCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Run(commandData.Application);
        }

        public Result Run(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Select a rebar element
            Reference rebarRef;
            try
            {
                rebarRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RebarSelectionFilter(),
                    "Select a rebar element to split");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            var rebar = doc.GetElement(rebarRef) as Autodesk.Revit.DB.Structure.Rebar;
            if (rebar == null) return Result.Failed;

            // 2. Pick a point for the split location (on any element)
            XYZ pickPoint;
            try
            {
                Reference ptRef = uidoc.Selection.PickObject(
                    ObjectType.PointOnElement,
                    "Pick a point along the rebar for the split location");
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
                TaskDialog.Show("Rebar Split", "Cannot find the host element for this rebar.");
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
                TaskDialog.Show("Rebar Split", "Could not find a straight segment in this rebar.");
                return Result.Failed;
            }

            // 5. Project the pick point onto the bar line to get the split parameter
            XYZ barStart = barLine.GetEndPoint(0);
            XYZ barEnd = barLine.GetEndPoint(1);
            XYZ barDir = (barEnd - barStart).Normalize();
            double barLength = barLine.Length;

            // CRITICAL: Ensure barStart/barEnd align with the original bar's end 0 / end 1.
            XYZ chainStart = curves[0].GetEndPoint(0);
            XYZ chainEnd = curves[curves.Count - 1].GetEndPoint(1);
            if (barStart.DistanceTo(chainEnd) < barStart.DistanceTo(chainStart))
            {
                // barLine is reversed relative to the chain — swap endpoints
                XYZ temp = barStart;
                barStart = barEnd;
                barEnd = temp;
                barDir = -barDir;
            }

            // === Read the REAL normal from the original rebar ===
            int barCount = rebar.NumberOfBarPositions;
            XYZ normal = null;

            if (barCount > 1)
            {
                try
                {
                    XYZ p0 = accessor.GetBarPositionTransform(0).Origin;
                    XYZ p1 = accessor.GetBarPositionTransform(1).Origin;
                    XYZ distDir = (p1 - p0);
                    if (distDir.GetLength() > 1e-9)
                        normal = distDir.Normalize();
                }
                catch { /* fall through to fallback */ }
            }

            if (normal == null)
            {
                Transform barTransform = accessor.GetBarPositionTransform(0);
                XYZ[] candidates = { barTransform.BasisZ, barTransform.BasisY, barTransform.BasisX };
                foreach (var candidate in candidates)
                {
                    if (!candidate.IsZeroLength())
                    {
                        XYZ cNorm = candidate.Normalize();
                        if (Math.Abs(cNorm.DotProduct(barDir)) < 0.1)
                        {
                            normal = cNorm;
                            break;
                        }
                    }
                }
            }

            if (normal == null)
            {
                normal = XYZ.BasisZ.CrossProduct(barDir);
                if (normal.IsZeroLength())
                    normal = XYZ.BasisX.CrossProduct(barDir);
                normal = normal.Normalize();
            }

            // Project pick point onto bar axis
            double splitDist = barDir.DotProduct(pickPoint - barStart);
            if (splitDist < 0.1 || splitDist > barLength - 0.1)
            {
                TaskDialog.Show("Rebar Split", "Pick point is too close to the bar ends. Pick a point further along the bar.");
                return Result.Failed;
            }

            // 6. Calculate lap splice length
            // Use NZS3101 as default; could be made configurable
            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, DesignCodeStandard.NZS3101);

            double shortTolerance = doc.Application.ShortCurveTolerance * 1.5;
            if (splitDist + lapLen > barLength - shortTolerance)
            {
                // Not enough room after the split point: shift split back
                splitDist = barLength - lapLen - shortTolerance;
                if (splitDist < 0.1)
                {
                    TaskDialog.Show("Rebar Split", "Bar is too short for a straight lap at this location.");
                    return Result.Failed;
                }
            }

            BoundingBoxXYZ hostBox = host.get_BoundingBox(null);

            // 7. Read original hook types and layout
            RebarHookType hookStartType = null;
            RebarHookType hookEndType = null;
            var hookStartParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_START_TYPE);
            var hookEndParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_END_TYPE);
            if (hookStartParam != null && hookStartParam.AsElementId() != ElementId.InvalidElementId)
                hookStartType = doc.GetElement(hookStartParam.AsElementId()) as RebarHookType;
            if (hookEndParam != null && hookEndParam.AsElementId() != ElementId.InvalidElementId)
                hookEndType = doc.GetElement(hookEndParam.AsElementId()) as RebarHookType;

            // Compute inward hook orientation
            RebarHookOrientation inwardOrient = RebarHookOrientation.Left; // default
            if (hostBox != null)
            {
                XYZ hostCenter = (hostBox.Min + hostBox.Max) / 2.0;
                XYZ barMid = (barStart + barEnd) / 2.0;
                XYZ toCenter = hostCenter - barMid;
                XYZ toCenterPerp = toCenter - barDir * toCenter.DotProduct(barDir);
                XYZ hookLeftDir = normal.CrossProduct(barDir);

                if (toCenterPerp.GetLength() > 1e-9 && hookLeftDir.GetLength() > 1e-9)
                {
                    inwardOrient = hookLeftDir.DotProduct(toCenterPerp) > 0
                        ? RebarHookOrientation.Left
                        : RebarHookOrientation.Right;
                }
            }

            // Read layout info
            double spacing = 0;
            double distWidth = 0;
            var spacingParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING);
            if (spacingParam != null) spacing = spacingParam.AsDouble();

            if (barCount > 1)
            {
                try
                {
                    XYZ posFirst = accessor.GetBarPositionTransform(0).Origin;
                    XYZ posLast = accessor.GetBarPositionTransform(barCount - 1).Origin;
                    distWidth = posFirst.DistanceTo(posLast);
                }
                catch
                {
                    if (spacing > 0) distWidth = spacing * (barCount - 1);
                }
            }

            // 8. Build the two new bar segments (straight lap, collinear)
            XYZ splitPoint = barStart + barDir * splitDist;

            // Segment 1: straight from barStart to splitPoint
            var curves1 = new List<Curve> { Line.CreateBound(barStart, splitPoint) };

            // Segment 2: straight lap BEFORE the split point to overlap Segment 1
            double seg2BackDir = lapLen;
            if (seg2BackDir > splitDist) seg2BackDir = splitDist - 0.1;

            XYZ seg2Start = splitPoint - barDir * seg2BackDir; // start lapLen before split
            var curves2 = new List<Curve>
            {
                Line.CreateBound(seg2Start, barEnd)  // perfectly collinear
            };

            // 9. Execute transaction
            using (Transaction t = new Transaction(doc, "Rebar Split"))
            {
                t.Start();
                try
                {
                    // Delete original rebar
                    doc.Delete(rebar.Id);

                    // Create segment 1
                    var rebar1 = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType,
                        hookStartType, null, // start hook only
                        host, normal, curves1,
                        inwardOrient, inwardOrient,
                        true, true);

                    // Create segment 2
                    var rebar2 = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                        doc, RebarStyle.Standard, barType,
                        null, hookEndType, // end hook only
                        host, normal, curves2,
                        inwardOrient, inwardOrient,
                        true, true);

                    if (rebar1 == null || rebar2 == null)
                    {
                        t.RollBack();
                        TaskDialog.Show("Rebar Split", "Failed to create split bars. Original rebar has been preserved.");
                        return Result.Failed;
                    }

                    // Apply layout
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

                    t.Commit();
                }
                catch (Exception ex)
                {
                    if (t.GetStatus() == TransactionStatus.Started)
                        t.RollBack();
                    TaskDialog.Show("Rebar Split", $"Failed to apply split:\n{ex.Message}");
                    return Result.Failed;
                }
            }

            return Result.Succeeded;
        }
    }
}
