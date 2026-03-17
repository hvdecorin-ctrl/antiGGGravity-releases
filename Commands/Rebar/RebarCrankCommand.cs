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
using antiGGGravity.Utilities;

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

            // CRITICAL: Ensure barStart/barEnd align with the original bar's end 0 / end 1.
            // GetCenterlineCurves returns an ordered chain where:
            //   curves[0].GetEndPoint(0) = bar's END 0 (where hookStartType lives)
            //   curves[last].GetEndPoint(1) = bar's END 1 (where hookEndType lives)
            // The longest line segment might be reversed relative to this chain.
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
            // Method 1 (most reliable): For multi-bar arrays, the distribution
            //   direction between bar positions IS the normal.
            // Method 2 (fallback): For single bars, try each transform basis axis
            //   and pick the one perpendicular to barDir.
            // Using the original normal preserves hook Left/Right meanings exactly.
            int barCount = rebar.NumberOfBarPositions;
            XYZ normal = null;

            if (barCount > 1)
            {
                // Multi-bar: normal = direction from first to second bar position
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
                // Single bar or multi-bar fallback: try transform axes
                // The normal should be perpendicular to the bar direction
                Transform barTransform = accessor.GetBarPositionTransform(0);
                XYZ[] candidates = { barTransform.BasisZ, barTransform.BasisY, barTransform.BasisX };
                foreach (var candidate in candidates)
                {
                    if (!candidate.IsZeroLength())
                    {
                        XYZ cNorm = candidate.Normalize();
                        // Normal must be approximately perpendicular to bar direction
                        if (Math.Abs(cNorm.DotProduct(barDir)) < 0.1)
                        {
                            normal = cNorm;
                            break;
                        }
                    }
                }
            }

            // Last resort fallback: compute from bar direction
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
                TaskDialog.Show("Rebar Crank", "Pick point is too close to the bar ends. Pick a point further along the bar.");
                return Result.Failed;
            }

            // 6. Calculate crank geometry
            double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
            double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
            // Use NZS3101 as default; could be made configurable
            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, DesignCodeStandard.NZS3101);
            double straightLap = lapLen + crankRun;

            // Crank direction: perpendicular to BOTH bar direction and the real normal
            // For horizontal beams: normal is horizontal (width), barDir is horizontal (length),
            // so crankPerp is VERTICAL — pure height direction, no lateral shift.
            XYZ crankPerp = normal.CrossProduct(barDir).Normalize();

            BoundingBoxXYZ hostBox = host.get_BoundingBox(null);
            XYZ crankDir;
            if (hostBox != null)
            {
                XYZ hostCenter = (hostBox.Min + hostBox.Max) / 2.0;
                XYZ barMid = (barStart + barEnd) / 2.0;
                
                // Sign check: which direction points inward toward host center?
                double dot = (hostCenter - barMid).DotProduct(crankPerp);
                crankDir = dot >= 0 ? crankPerp : -crankPerp;
            }
            else
            {
                crankDir = crankPerp; // Fallback
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

            // 7. Read original hook types and layout
            RebarHookType hookStartType = null;
            RebarHookType hookEndType = null;
            var hookStartParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_START_TYPE);
            var hookEndParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_END_TYPE);
            if (hookStartParam != null && hookStartParam.AsElementId() != ElementId.InvalidElementId)
                hookStartType = doc.GetElement(hookStartParam.AsElementId()) as RebarHookType;
            if (hookEndParam != null && hookEndParam.AsElementId() != ElementId.InvalidElementId)
                hookEndType = doc.GetElement(hookEndParam.AsElementId()) as RebarHookType;

            // === UNIVERSAL INWARD HOOK COMPUTATION ===
            // Instead of preserving original hook orientation (which depends on the
            // original bar's unknown normal), we COMPUTE which Left/Right value
            // produces inward-bending hooks for OUR specific normal.
            //
            // Revit convention: "Left" hook bends in the direction of (normal × barDir).
            // So we check: does (normal × barDir) point INWARD toward host center?
            //   If yes → Left = inward
            //   If no  → Right = inward
            RebarHookOrientation inwardOrient = (RebarHookOrientation)1; // Default Left
            if (hostBox != null)
            {
                XYZ hostCenter = (hostBox.Min + hostBox.Max) / 2.0;
                XYZ barMid = (barStart + barEnd) / 2.0;

                // Direction from bar toward host center, perpendicular to bar axis
                XYZ toCenter = hostCenter - barMid;
                XYZ toCenterPerp = toCenter - barDir * toCenter.DotProduct(barDir);

                // The "Left" hook bend direction for this normal/barDir combination
                XYZ hookLeftDir = normal.CrossProduct(barDir);

                if (toCenterPerp.GetLength() > 1e-9 && hookLeftDir.GetLength() > 1e-9)
                {
                    inwardOrient = hookLeftDir.DotProduct(toCenterPerp) > 0
                        ? (RebarHookOrientation)1   // Left
                        : (RebarHookOrientation)(-1); // Right
                }
            }

            // Read layout info
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
                    XYZ posFirst = accessor.GetBarPositionTransform(0).Origin;
                    XYZ posLast = accessor.GetBarPositionTransform(barCount - 1).Origin;
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
                    var rebar1 = RevitCompatibility.CreateRebar(
                        doc, RebarStyle.Standard, barType,
                        hookStartType, null, // start hook only
                        host, normal, curves1,
                        inwardOrient, inwardOrient,
                        true, true);

                    // Create segment 2 (cranked, with original end hook)
                    var rebar2 = RevitCompatibility.CreateRebar(
                        doc, RebarStyle.Standard, barType,
                        null, hookEndType, // end hook only
                        host, normal, curves2,
                        inwardOrient, inwardOrient,
                        true, true);

                    // SAFETY: if either bar creation failed, rollback to preserve original
                    if (rebar1 == null || rebar2 == null)
                    {
                        t.RollBack();
                        TaskDialog.Show("Rebar Crank", "Failed to create cranked bars. Original rebar has been preserved.\n\nThis may happen if the bar geometry is incompatible with the crank operation.");
                        return Result.Failed;
                    }

                    // Apply layout to both new rebars
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
                    TaskDialog.Show("Rebar Crank", $"Failed to apply crank:\n{ex.Message}");
                    return Result.Failed;
                }
            }

            return Result.Succeeded;
        }
    }
}
