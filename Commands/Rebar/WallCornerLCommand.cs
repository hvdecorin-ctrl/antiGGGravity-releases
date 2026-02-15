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
    public class WallCornerLCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new WallCornerRebarLView(doc);
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
                TaskDialog.Show("Info", "No intersecting walls found among the selection.");
                return Result.Succeeded;
            }

            // 4. Process Rebar
            int rebarCount = 0;
            using (Transaction t = new Transaction(doc, "Wall Corner L Rebar"))
            {
                t.Start();

                if (view.RemoveExisting)
                {
                    foreach (var wall in walls)
                    {
                         DeleteExistingRebar(doc, wall);
                    }
                }

                foreach (var corner in cornerPairs)
                {
                    try
                    {
                        rebarCount += GenerateCornerRebar(doc, corner, view);
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

            double tolerance = 1.0; // 1 foot tolerance for corner identification
            
            XYZ[] pts1 = { p1_0, p1_1 };
            XYZ[] pts2 = { p2_0, p2_1 };

            foreach (var p1 in pts1)
            {
                foreach (var p2 in pts2)
                {
                    if (p1.DistanceTo(p2) < tolerance)
                    {
                        // Return average point for more accuracy if slightly off
                        return (p1 + p2) / 2.0;
                    }
                }
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

        private int GenerateCornerRebar(Document doc, CornerInfo corner, WallCornerRebarLView view)
        {
            Wall w1 = corner.Wall1;
            Wall w2 = corner.Wall2;
            XYZ cp = corner.Point;

            // Directions away from corner
            XYZ dir1 = GetDirectionAway(w1, cp);
            XYZ dir2 = GetDirectionAway(w2, cp);

            // Normals
            XYZ tangent1 = ((w1.Location as LocationCurve).Curve as Line).Direction;
            XYZ normal1 = new XYZ(-tangent1.Y, tangent1.X, 0); // Default Normal

            XYZ tangent2 = ((w2.Location as LocationCurve).Curve as Line).Direction;
            XYZ normal2 = new XYZ(-tangent2.Y, tangent2.X, 0);

            // Bounding Box (assuming walls similar Z)
            BoundingBoxXYZ bbox = w1.get_BoundingBox(null);
            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;

            double thickness = w1.Width;
            double cExt = GeometryUtils.GetCoverDistance(doc, w1, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GeometryUtils.GetCoverDistance(doc, w1, BuiltInParameter.CLEAR_COVER_INTERIOR);
            
            double hDia = view.HorizType.BarModelDiameter;

            // Offsets from centerline
            double hExt = (thickness / 2.0) - cExt - (hDia / 2.0);
            double hInt = (thickness / 2.0) - cInt - (hDia / 2.0);

            List<double> layerOffsets = new List<double>();
            string config = view.LayerConfig;
            if (config == "Centre") layerOffsets.Add(hDia); // Gap to avoid clash? Python used h_diam. 
            else if (config == "Both faces") { layerOffsets.Add(hExt); layerOffsets.Add(-hInt); }
            else if (config == "External face") layerOffsets.Add(hExt);
            else if (config == "Internal face") layerOffsets.Add(-hInt);

            int count = 0;
            foreach (double offset in layerOffsets)
            {
                count += CreateCornerLBar(doc, w1, view.HorizType, cp, dir1, dir2, normal1, normal2, 
                    zMin, zMax, 
                    view.Leg1MM / 304.8, 
                    view.Leg2MM / 304.8, 
                    view.HorizSpacingMM / 304.8, 
                    view.TopOffsetMM / 304.8, 
                    view.BottomOffsetMM / 304.8, 
                    offset, 
                    view.HookStart, view.HookEnd, 
                    view.HookStartOut, view.HookEndOut);
            }

            if (view.AddTrimmers)
            {
                CreateTrimmerBars(doc, w1, view.TrimmerType ?? view.HorizType, cp, 
                    zMin, zMax, view.TopOffsetMM / 304.8, view.BottomOffsetMM / 304.8);
            }

            return count;
        }

        private XYZ GetDirectionAway(Wall wall, XYZ cornerPoint)
        {
            Curve curve = (wall.Location as LocationCurve).Curve;
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            if (p0.DistanceTo(cornerPoint) < p1.DistanceTo(cornerPoint))
                return (p1 - p0).Normalize();
            else
                return (p0 - p1).Normalize();
        }

        private int CreateCornerLBar(Document doc, Wall host, RebarBarType type, XYZ cornerPoint, 
            XYZ dir1, XYZ dir2, XYZ normal1, XYZ normal2, 
            double zMin, double zMax, double leg1, double leg2, 
            double spacing, double topOff, double botOff, 
            double offsetDist, 
            RebarHookType hookS, RebarHookType hookE, bool hookSOut, bool hookEOut)
        {
            double hRange = (zMax - zMin) - topOff - botOff;
            if (hRange <= 0) return 0;

            // Orient Normals Toward OUTER corner
            // Outer corner is opposite to directions AWAY from corner.
            XYZ adjNormal1 = normal1;
            if (normal1.DotProduct(-dir2) < 0) adjNormal1 = -normal1;

            XYZ adjNormal2 = normal2;
            if (normal2.DotProduct(-dir1) < 0) adjNormal2 = -normal2;

            // Corner position at rebar layer
            XYZ cornerOffset = cornerPoint + adjNormal1 * offsetDist + adjNormal2 * offsetDist;
            
            double zStart = zMin + botOff;
            XYZ p_corner = new XYZ(cornerOffset.X, cornerOffset.Y, zStart);
            XYZ p_start = p_corner + dir1 * leg1;
            XYZ p_end = p_corner + dir2 * leg2;

            List<Curve> curves = new List<Curve> {
                Line.CreateBound(p_start, p_corner),
                Line.CreateBound(p_corner, p_end)
            };

            // Hook Orientation
            RebarHookOrientation orientS, orientE;
            if (offsetDist >= 0) {
                orientS = hookSOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
                orientE = hookEOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
            } else {
                orientS = hookSOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
                orientE = hookEOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            }

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

        private void CreateTrimmerBars(Document doc, Wall host, RebarBarType type, XYZ cp, 
            double zMin, double zMax, double topOff, double botOff)
        {
            double zStart = zMin + botOff;
            double zEnd = zMax - topOff;
            
            XYZ pBot = new XYZ(cp.X, cp.Y, zStart);
            XYZ pTop = new XYZ(cp.X, cp.Y, zEnd);

            List<Curve> curves = new List<Curve> { Line.CreateBound(pBot, pTop) };

            try {
                DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, null, null,
                    host, XYZ.BasisX, curves, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
            } catch { }
        }

        private class CornerInfo
        {
            public Wall Wall1;
            public Wall Wall2;
            public XYZ Point;
        }

        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
