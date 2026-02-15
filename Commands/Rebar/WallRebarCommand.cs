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
    public class WallRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new WallRebarView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Walls
            List<Wall> walls = new List<Wall>();
            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, new WallSelectionFilter(), "Select Walls");
                walls = refs.Select(r => doc.GetElement(r.ElementId) as Wall).Where(w => w != null).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (!walls.Any()) return Result.Cancelled;

            // 3. Process
            int count = 0;
            using (Transaction t = new Transaction(doc, "Wall Rebar"))
            {
                t.Start();

                foreach (var wall in walls)
                {
                    try
                    {
                        if (view.RemoveExisting)
                        {
                            DeleteExistingRebar(doc, wall);
                        }

                        if (GenerateRebar(doc, wall, view))
                        {
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error on wall {wall.Id}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Result", $"Reinforced {count} walls.");
            return Result.Succeeded;
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

        private bool GenerateRebar(Document doc, Wall wall, WallRebarView view)
        {
            // Geometry
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null || !(locCurve.Curve is Line)) return false;

            Line wallLine = locCurve.Curve as Line;
            XYZ wallDir = (wallLine.GetEndPoint(1) - wallLine.GetEndPoint(0)).Normalize();
            XYZ wallNormal = wall.Orientation; // Points to Exterior
            
            // Bounding Box
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;
            
            double thickness = wall.Width;

            // Covers
            double cExt = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_INTERIOR);
            double cOther = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_OTHER);

            // Rebar Types & Diameters
            RebarBarType vType = view.VertType;
            RebarBarType hType = view.HorizType;
            if (vType == null || hType == null) return false;

            double vDia = vType.BarModelDiameter;
            double hDia = hType.BarModelDiameter;

            // Layer Logic
            // Distances from center line? No, let's use offset from center line logic.
            // Center Line = Location Curve (usually). Wait, loc line works best.
            // Offset from Center = 0 means center of wall.
            // Exterior face is at +thickness/2 along wallNormal.
            // Interior face is at -thickness/2 along wallNormal.
            
            // Distances from Center Logic:
            // Exterior Layer:
            // H-Bar Center: (thickness/2) - cExt - (hDia/2)
            // V-Bar Center: H-Bar Center - (hDia/2) - (vDia/2) => (thickness/2) - cExt - hDia - vDia/2
            
            double dExtH = (thickness / 2.0) - cExt - (hDia / 2.0);
            double dExtV = dExtH - (hDia / 2.0) - (vDia / 2.0);

            double dIntH = (thickness / 2.0) - cInt - (hDia / 2.0);
            double dIntV = dIntH - (hDia / 2.0) - (vDia / 2.0);

            // List of layers: (vOffset, hOffset, sideCover)
            // Offsets are along wallNormal (positive = exterior)
            List<Tuple<double, double, double>> layers = new List<Tuple<double, double, double>>();

            string config = view.LayerConfig;
            if (config == "Centre")
            {
                double avgCover = (cExt + cInt) / 2.0;
                // Single layer at center
                layers.Add(new Tuple<double, double, double>(0, (vDia/2 + hDia/2), avgCover)); 
                // Wait, if Center, H bar offset relative to V bar?
                // Usually Center means V bar at 0. H bar can be outside V bar?
                // "Centre" implies one layer.
                // Re-reading python: 
                // layer_configs.append((0, (v_diam / 2.0) + (h_diam / 2.0), avg_cover))
                // V at 0. H at offset? This puts H bar "outside" V bar on one side?
                // Let's stick to python logic.
            }
            else if (config == "Both faces")
            {
                layers.Add(new Tuple<double, double, double>(dExtV, dExtH, cExt));
                layers.Add(new Tuple<double, double, double>(-dIntV, -dIntH, cInt));
            }
            else if (config == "External face")
            {
                layers.Add(new Tuple<double, double, double>(dExtV, dExtH, cExt));
            }
            else if (config == "Internal face")
            {
                layers.Add(new Tuple<double, double, double>(-dIntV, -dIntH, cInt));
            }

            // Generate
            foreach (var layer in layers)
            {
                double vOffset = layer.Item1;
                double hOffset = layer.Item2;
                double sideCover = layer.Item3; 

                // Note: Python passes cOther for side cover of bars.
                // layer.Item3 (sideCover) was c_ext or c_int.
                // In Python: `c_other` is passed as `side_cover` argument to functions?
                // Python: `_create_vertical_bars(..., c_other, ...)`
                // So side cover is strictly End Cover.

                CreateVerticalBars(doc, wall, vType, wallLine, wallDir, wallNormal, zMin, zMax, 
                    view.VertSpacingMM / 304.8, 
                    view.VertStartOffsetMM / 304.8, 
                    view.VertEndOffsetMM / 304.8, 
                    cOther, // Top Cover (using Other)
                    cOther, // Bottom Cover (using Other)
                    view.EnableVertTopExt ? view.VertTopExtMM / 304.8 : 0,
                    view.EnableVertBotExt ? view.VertBotExtMM / 304.8 : 0,
                    vOffset, 
                    view.VertHookStart, view.VertHookEnd, 
                    view.VertHookStartOut, view.VertHookEndOut);

                CreateHorizontalBars(doc, wall, hType, wallLine, wallDir, wallNormal, zMin, zMax,
                    view.HorizSpacingMM / 304.8,
                    view.HorizTopOffsetMM / 304.8,
                    view.HorizBottomOffsetMM / 304.8,
                    cOther, cOther, // Start/End Cover
                    hOffset,
                    view.HorizHookStart, view.HorizHookEnd,
                    view.HorizHookStartOut, view.HorizHookEndOut);
            }

            return true;
        }

        private void CreateVerticalBars(Document doc, Wall wall, RebarBarType type, Line baseCurve, 
            XYZ wallDir, XYZ wallNormal, double zMin, double zMax, 
            double spacing, double startOff, double endOff, 
            double topCover, double botCover, double extTop, double extBot, 
            double offsetDist, 
            RebarHookType hookS, RebarHookType hookE, bool hookSOut, bool hookEOut)
        {
            double totalLen = baseCurve.Length - startOff - endOff;
            if (totalLen <= 0) return;

            // Hook Orientation
            RebarHookOrientation orientS, orientE;
            if (offsetDist >= 0) // Exterior
            {
                orientS = hookSOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
                orientE = hookEOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            }
            else // Interior
            {
                orientS = hookSOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
                orientE = hookEOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
            }

            // Start Point of SET
            XYZ p0 = baseCurve.GetEndPoint(0);
            XYZ point = p0 + wallDir * startOff + wallNormal * offsetDist;

            // Bar Geometry
            // Z limits: zMin + botCover - extBot  TO  zMax - topCover + extTop
            // (Assuming 'BotCover' pulls it up from bottom, 'TopCover' pushes it down from top)
            // Python used: z_min + bottom_off - ext_bot
            // Python 'bottom_off' arguments were passed as 'c_other'.
            
            double zStart = zMin + botCover - extBot;
            double zEnd = zMax - topCover + extTop;

            XYZ startXYZ = new XYZ(point.X, point.Y, zStart);
            XYZ endXYZ = new XYZ(point.X, point.Y, zEnd);

            Line line = Line.CreateBound(startXYZ, endXYZ);
            List<Curve> curves = new List<Curve> { line };

            try
            {
                DBRebar rebar = DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, hookS, hookE,
                    wall, wallDir, curves, orientS, orientE, true, true);

                if (rebar != null)
                {
                    rebar.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spacing, totalLen, true, true, true);
                    
                    if (doc.ActiveView is View3D v3d)
                        rebar.SetUnobscuredInView(v3d, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating V-bars: {ex.Message}");
            }
        }

        private void CreateHorizontalBars(Document doc, Wall wall, RebarBarType type, Line baseCurve,
             XYZ wallDir, XYZ wallNormal, double zMin, double zMax,
             double spacing, double topOff, double botOff,
             double startCover, double endCover,
             double offsetDist,
             RebarHookType hookS, RebarHookType hookE, bool hookSOut, bool hookEOut)
        {
            double hRange = (zMax - zMin) - topOff - botOff;
            if (hRange <= 0) return;

            // Hook Orientation
            RebarHookOrientation orientS, orientE;
            if (offsetDist >= 0) // Exterior
            {
                orientS = hookSOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
                orientE = hookEOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
            }
            else // Interior
            {
                orientS = hookSOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
                orientE = hookEOut ? RebarHookOrientation.Right : RebarHookOrientation.Left;
            }

            // Start Point of SET (Bottom bar)
            // Located at zMin + botOff
            double zStartVal = zMin + botOff;

            XYZ p0 = baseCurve.GetEndPoint(0);
            XYZ p1 = baseCurve.GetEndPoint(1);

            // Bar Geometry
            // Start: p0 + wallDir * startCover + wallNormal * offsetDist
            // End: p1 - wallDir * endCover + wallNormal * offsetDist
            
            XYZ startXYZ = new XYZ(p0.X, p0.Y, zStartVal) + wallDir * startCover + wallNormal * offsetDist;
            XYZ endXYZ = new XYZ(p1.X, p1.Y, zStartVal) - wallDir * endCover + wallNormal * offsetDist;

            Line line = Line.CreateBound(startXYZ, endXYZ);
            List<Curve> curves = new List<Curve> { line };

            try
            {
                DBRebar rebar = DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, hookS, hookE,
                    wall, XYZ.BasisZ, curves, orientS, orientE, true, true);

                if (rebar != null)
                {
                    rebar.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(spacing, hRange, true, true, true);
                     if (doc.ActiveView is View3D v3d)
                        rebar.SetUnobscuredInView(v3d, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating H-bars: {ex.Message}");
            }
        }

        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Wall;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
