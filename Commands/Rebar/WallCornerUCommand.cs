using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Utilities;
using antiGGGravity.Views.Rebar;
using DBRebar = Autodesk.Revit.DB.Structure.Rebar;
using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands;

namespace antiGGGravity.Commands.Rebar
{
    [Transaction(TransactionMode.Manual)]
    public class WallCornerUCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new WallCornerRebarUView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Walls
            List<Wall> walls = new List<Wall>();
            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, new WallSelectionFilter(), "Select walls forming corners (press Finish)");
                walls = refs.Select(r => doc.GetElement(r.ElementId) as Wall).Where(w => w != null).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (walls.Count < 2) return Result.Cancelled;

            // 3. Find Corners
            var cornerPairs = FindCorners(walls);
            if (!cornerPairs.Any())
            {
                TaskDialog.Show("Info", "No intersecting walls found.");
                return Result.Succeeded;
            }

            // 4. Process
            int rebarCount = 0;
            using (Transaction t = new Transaction(doc, "Wall Corner U Rebar"))
            {
                t.Start();

                if (view.RemoveExisting)
                {
                    foreach (var wall in walls) DeleteExistingRebar(doc, wall);
                }

                foreach (var corner in cornerPairs)
                {
                    try
                    {
                        rebarCount += GenerateCornerURebar(doc, corner, view);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error on corner: {ex.Message}");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Result", $"Generated {rebarCount} bars at {cornerPairs.Count} corners.");
            return Result.Succeeded;
        }

        private List<CornerInfo> FindCorners(List<Wall> walls)
        {
            var corners = new List<CornerInfo>();
            for (int i = 0; i < walls.Count; i++)
            {
                for (int j = i + 1; j < walls.Count; j++)
                {
                    XYZ intersect = GetIntersect(walls[i], walls[j]);
                    if (intersect != null)
                    {
                        corners.Add(new CornerInfo { Wall1 = walls[i], Wall2 = walls[j], Point = intersect });
                    }
                }
            }
            return corners;
        }

        private XYZ GetIntersect(Wall w1, Wall w2)
        {
            Curve c1 = (w1.Location as LocationCurve)?.Curve;
            Curve c2 = (w2.Location as LocationCurve)?.Curve;
            if (c1 == null || c2 == null || !(c1 is Line) || !(c2 is Line)) return null;

            XYZ p1_0 = c1.GetEndPoint(0);
            XYZ p1_1 = c1.GetEndPoint(1);
            XYZ p2_0 = c2.GetEndPoint(0);
            XYZ p2_1 = c2.GetEndPoint(1);

            double tolerance = 1.0; 
            XYZ[] pts1 = { p1_0, p1_1 };
            XYZ[] pts2 = { p2_0, p2_1 };

            foreach (var p1 in pts1)
            {
                foreach (var p2 in pts2)
                    if (p1.DistanceTo(p2) < tolerance) return (p1 + p2) / 2.0;
            }
            return null;
        }

        private void DeleteExistingRebar(Document doc, Element host)
        {
            var rebarHostData = RebarHostData.GetRebarHostData(host);
            if (rebarHostData != null)
            {
                foreach (var refRebar in rebarHostData.GetRebarsInHost())
                {
                    try { doc.Delete(new List<ElementId> { refRebar.Id }); } catch { }
                }
            }
        }

        private int GenerateCornerURebar(Document doc, CornerInfo corner, WallCornerRebarUView view)
        {
            Wall w1 = corner.Wall1;
            Wall w2 = corner.Wall2;
            XYZ cp = corner.Point;

            XYZ dir1 = GetDirectionAway(w1, cp);
            XYZ dir2 = GetDirectionAway(w2, cp);

            XYZ tangent1 = ((w1.Location as LocationCurve).Curve as Line).Direction;
            XYZ normal1 = new XYZ(-tangent1.Y, tangent1.X, 0);

            XYZ tangent2 = ((w2.Location as LocationCurve).Curve as Line).Direction;
            XYZ normal2 = new XYZ(-tangent2.Y, tangent2.X, 0);

            double t1 = w1.Width;
            double t2 = w2.Width;
            
            double cExt1 = GeometryUtils.GetCoverDistance(doc, w1, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cExt2 = GeometryUtils.GetCoverDistance(doc, w2, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            
            double hDia = view.HorizType.BarModelDiameter;
            double offset1 = (t1 / 2.0) - cExt1 - (hDia / 2.0);
            double offset2 = (t2 / 2.0) - cExt2 - (hDia / 2.0);

            int count = 0;

            // Generate U-bar for Wall 1
            BoundingBoxXYZ bbox1 = w1.get_BoundingBox(null);
            count += CreateUBar(doc, w1, view.HorizType, cp, dir1, dir2, normal1, normal2, 
                bbox1.Min.Z, bbox1.Max.Z, 
                view.Leg1MM / 304.8, 
                view.Leg1MM / 304.8, // Leg2 used same as Leg1 in python? 
                view.HorizSpacingMM / 304.8, 
                view.TopOffsetMM / 304.8, 
                view.BottomOffsetMM / 304.8, 
                offset1, offset2, 
                view.HookStart, view.HookEnd, view.HookStartOut, view.HookEndOut);

            // Generate U-bar for Wall 2
            BoundingBoxXYZ bbox2 = w2.get_BoundingBox(null);
            count += CreateUBar(doc, w2, view.HorizType, cp, dir2, dir1, normal2, normal1, 
                bbox2.Min.Z, bbox2.Max.Z, 
                view.Leg2MM / 304.8, 
                view.Leg2MM / 304.8, 
                view.HorizSpacingMM / 304.8, 
                view.TopOffsetMM / 304.8, 
                view.BottomOffsetMM / 304.8, 
                offset2, offset1, 
                view.HookStart, view.HookEnd, view.HookStartOut, view.HookEndOut);

            if (view.AddTrimmers)
            {
                CreateTrimmersU(doc, w1, view.TrimmerType ?? view.HorizType, cp, dir1, dir2, normal1, normal2,
                    bbox1.Min.Z, bbox1.Max.Z,
                    view.TopOffsetMM / 304.8, 
                    view.BottomOffsetMM / 304.8,
                    offset1, offset2, hDia);
            }

            return count;
        }

        private XYZ GetDirectionAway(Wall wall, XYZ cornerPoint)
        {
            Curve curve = (wall.Location as LocationCurve).Curve;
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            return (p0.DistanceTo(cornerPoint) < p1.DistanceTo(cornerPoint)) ? (p1 - p0).Normalize() : (p0 - p1).Normalize();
        }

        private int CreateUBar(Document doc, Wall host, RebarBarType type, XYZ cornerPoint,
            XYZ dir1, XYZ dir2, XYZ normal1, XYZ normal2,
            double zMin, double zMax, double leg1, double leg2,
            double spacing, double topOff, double botOff,
            double offset1, double offset2,
            RebarHookType hookS, RebarHookType hookE, bool hookSOut, bool hookEOut)
        {
            double hRange = (zMax - zMin) - topOff - botOff;
            if (hRange <= 0) return 0;

            // Orient Normal1 Toward Exterior
            XYZ adjN1 = normal1; 
            // offset1 is distance from center to bar on ext face.
            // p1 = cp + adjN1 * offset1 - dir1 * offset2
            
            // Segments:
            // p_start -> p1 (along dir1, ext face)
            // p1 -> p2 (across thickness)
            // p2 -> p_end (along dir1, int face)
            
            double zStart = zMin + botOff;
            XYZ p1 = cornerPoint + adjN1 * offset1 - dir1 * offset2;
            XYZ p2 = cornerPoint - adjN1 * offset1 - dir1 * offset2;
            XYZ p_start = p1 + dir1 * leg1;
            XYZ p_end = p2 + dir1 * leg2;

            p1 = new XYZ(p1.X, p1.Y, zStart);
            p2 = new XYZ(p2.X, p2.Y, zStart);
            p_start = new XYZ(p_start.X, p_start.Y, zStart);
            p_end = new XYZ(p_end.X, p_end.Y, zStart);

            List<Curve> curves = new List<Curve> {
                Line.CreateBound(p_start, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p_end)
            };

            RebarHookOrientation orientS = hookSOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
            RebarHookOrientation orientE = hookEOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;

            try {
                DBRebar rebar = DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, hookS, hookE,
                    host, XYZ.BasisZ, curves, orientS, orientE, true, true);
                if (rebar != null) {
                    rebar.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spacing, hRange, true, true, true);
                    return rebar.NumberOfBarPositions;
                }
            } catch { }
            return 0;
        }

        private void CreateTrimmersU(Document doc, Wall host, RebarBarType type, XYZ cp, 
            XYZ dir1, XYZ dir2, XYZ normal1, XYZ normal2,
            double zMin, double zMax, double topOff, double botOff,
            double offset1, double offset2, double barDiam)
        {
            double zStart = zMin + botOff;
            double zEnd = zMax - topOff;

            // Calculate 4 positions using 2D line intersection (matching Python implementation)
            // Wall1 U-bar leg positions:
            // External leg passes through: cp + normal1 * offset1, runs in direction dir1
            // Internal leg passes through: cp - normal1 * offset1, runs in direction dir1
            // Wall2 U-bar leg positions:
            // External leg passes through: cp + normal2 * offset2, runs in direction dir2
            // Internal leg passes through: cp - normal2 * offset2, runs in direction dir2

            XYZ w1ExtBase = cp + normal1 * offset1;
            XYZ w1IntBase = cp - normal1 * offset1;
            XYZ w2ExtBase = cp + normal2 * offset2;
            XYZ w2IntBase = cp - normal2 * offset2;

            // Find 4 intersection points
            XYZ pos1 = LineIntersection2D(w1ExtBase, dir1, w2ExtBase, dir2); // ext-ext
            XYZ pos2 = LineIntersection2D(w1ExtBase, dir1, w2IntBase, dir2); // ext-int
            XYZ pos3 = LineIntersection2D(w1IntBase, dir1, w2ExtBase, dir2); // int-ext
            XYZ pos4 = LineIntersection2D(w1IntBase, dir1, w2IntBase, dir2); // int-int

            List<XYZ> rawPositions = new List<XYZ>();
            if (pos1 != null) rawPositions.Add(pos1);
            if (pos2 != null) rawPositions.Add(pos2);
            if (pos3 != null) rawPositions.Add(pos3);
            if (pos4 != null) rawPositions.Add(pos4);

            if (rawPositions.Count < 4) return; // Need all 4 intersections

            // Calculate geometric center
            double centerX = rawPositions.Average(p => p.X);
            double centerY = rawPositions.Average(p => p.Y);
            XYZ center = new XYZ(centerX, centerY, cp.Z);

            // Offset each position inward toward center by 2 × barDiam
            double inwardOffset = barDiam * 2;
            List<XYZ> trimmerPositions = new List<XYZ>();

            foreach (var pos in rawPositions)
            {
                XYZ toCenter = new XYZ(center.X - pos.X, center.Y - pos.Y, 0);
                double len = Math.Sqrt(toCenter.X * toCenter.X + toCenter.Y * toCenter.Y);
                if (len > 0.001)
                {
                    XYZ unitToCenter = new XYZ(toCenter.X / len, toCenter.Y / len, 0);
                    XYZ newPos = new XYZ(pos.X + unitToCenter.X * inwardOffset, 
                                         pos.Y + unitToCenter.Y * inwardOffset, pos.Z);
                    trimmerPositions.Add(newPos);
                }
                else
                {
                    trimmerPositions.Add(pos);
                }
            }

            // Create vertical trimmer bars
            foreach (var pos in trimmerPositions)
            {
                XYZ pBot = new XYZ(pos.X, pos.Y, zStart);
                XYZ pTop = new XYZ(pos.X, pos.Y, zEnd);
                List<Curve> curves = new List<Curve> { Line.CreateBound(pBot, pTop) };
                try {
                    DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, null, null,
                        host, normal1, curves, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                } catch { }
            }
        }

        /// <summary>
        /// Find intersection of two 2D lines.
        /// Line 1: p1 + t * d1
        /// Line 2: p2 + s * d2
        /// </summary>
        private XYZ LineIntersection2D(XYZ p1, XYZ d1, XYZ p2, XYZ d2)
        {
            double cross = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(cross) < 1e-10) return null; // Parallel lines

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double t = (dx * d2.Y - dy * d2.X) / cross;

            return new XYZ(p1.X + t * d1.X, p1.Y + t * d1.Y, p1.Z);
        }

        private class CornerInfo { public Wall Wall1; public Wall Wall2; public XYZ Point; }
        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
