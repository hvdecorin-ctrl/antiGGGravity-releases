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
    public class StripFootingRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new StripFootingRebarView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Foundations
            List<Element> foundations = new List<Element>();
            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, new StripFootingSelectionFilter(), "Select Strip Footings");
                foundations = refs.Select(r => doc.GetElement(r.ElementId)).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (!foundations.Any()) return Result.Cancelled;

            // 3. Process
            int count = 0;
            using (Transaction t = new Transaction(doc, "Strip Footing Rebar"))
            {
                t.Start();

                foreach (var foundation in foundations)
                {
                    try
                    {
                        if (view.RemoveExisting)
                        {
                            DeleteExistingRebar(doc, foundation);
                        }

                        if (GenerateRebar(doc, foundation, view))
                        {
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue
                         System.Diagnostics.Debug.WriteLine($"Error on foundation {foundation.Id}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Result", $"Reinforced {count} strip footings.");
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

        private bool GenerateRebar(Document doc, Element foundation, StripFootingRebarView view)
        {
            // Geometry Analysis
            BoundingBoxXYZ bbox = foundation.get_BoundingBox(null);
            if (bbox == null) return false;

            XYZ center = (bbox.Min + bbox.Max) / 2;
            
            // Determine Axis (Long direction) - matching Python logic exactly
            XYZ axisDir = null;
            XYZ widthDir = null;
            double length = 0;
            XYZ startPt = null;
            XYZ endPt = null;

            // Attempt A: Explicit Location Curve (Most robust for Strip/Wall Footings)
            Curve pathCurve = null;
            if (foundation.Location is LocationCurve locCurve)
            {
                pathCurve = locCurve.Curve;
            }
            // Backup for WallFoundations where Location might be tricky
            if (pathCurve == null && foundation is WallFoundation wf && wf.WallId != ElementId.InvalidElementId)
            {
                var wall = doc.GetElement(wf.WallId) as Wall;
                if (wall?.Location is LocationCurve wallLocCurve)
                    pathCurve = wallLocCurve.Curve;
            }

            if (pathCurve != null)
            {
                XYZ curveStart = pathCurve.GetEndPoint(0);
                XYZ curveEnd = pathCurve.GetEndPoint(1);
                axisDir = (curveEnd - curveStart).Normalize();
                
                // Project onto horizontal plane
                if (Math.Abs(axisDir.Z) < 0.999)
                    axisDir = new XYZ(axisDir.X, axisDir.Y, 0).Normalize();
                widthDir = XYZ.BasisZ.CrossProduct(axisDir).Normalize();
                
                // Use solid geometry to get TRUE length (fixes wall foundation extensions)
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(foundation, axisDir);
                if (geomResult.HasValue)
                {
                    length = geomResult.Value.Length;
                    startPt = geomResult.Value.StartPt;
                    endPt = geomResult.Value.EndPt;
                }
                else
                {
                    length = pathCurve.Length;
                    startPt = curveStart;
                    endPt = curveEnd;
                }
            }
            // Attempt B: Transform / Rotation (Family Instances / Isolated)
            else if (foundation is FamilyInstance fi)
            {
                Transform trans = fi.GetTransform();
                axisDir = trans.BasisX.Normalize();
                widthDir = trans.BasisY.Normalize();
                
                // Project onto horizontal
                axisDir = new XYZ(axisDir.X, axisDir.Y, 0).Normalize();
                widthDir = XYZ.BasisZ.CrossProduct(axisDir).Normalize();
                
                // Use solid geometry to find true length (fixes rotated elements)
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(foundation, axisDir);
                if (geomResult.HasValue)
                {
                    length = geomResult.Value.Length;
                    startPt = geomResult.Value.StartPt;
                    endPt = geomResult.Value.EndPt;
                }
                else
                {
                    // Fallback to bbox if geometry extraction fails
                    XYZ vBbox = bbox.Max - bbox.Min;
                    length = Math.Abs(vBbox.DotProduct(axisDir));
                    double wGuess = Math.Abs(vBbox.DotProduct(widthDir));
                    if (wGuess > length)
                    {
                        // Swap axis and width direction
                        XYZ temp = axisDir;
                        axisDir = widthDir;
                        widthDir = temp.Negate();
                        length = wGuess;
                    }
                    startPt = center - axisDir * (length / 2);
                    endPt = center + axisDir * (length / 2);
                }
            }
            // Attempt C: Location Point Rotation (fallback for families)
            else if (foundation.Location is LocationPoint locPt)
            {
                double rot = locPt.Rotation;
                axisDir = new XYZ(Math.Cos(rot), Math.Sin(rot), 0);
                widthDir = XYZ.BasisZ.CrossProduct(axisDir).Normalize();
                
                // Use solid geometry to find true length
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(foundation, axisDir);
                if (geomResult.HasValue)
                {
                    length = geomResult.Value.Length;
                    startPt = geomResult.Value.StartPt;
                    endPt = geomResult.Value.EndPt;
                }
                else
                {
                    // Fallback to bbox
                    XYZ vBbox = bbox.Max - bbox.Min;
                    length = Math.Abs(vBbox.DotProduct(axisDir));
                    double wGuess = Math.Abs(vBbox.DotProduct(widthDir));
                    if (wGuess > length)
                    {
                        XYZ temp = axisDir;
                        axisDir = widthDir;
                        widthDir = temp.Negate();
                        length = wGuess;
                    }
                    startPt = locPt.Point - axisDir * (length / 2);
                    endPt = locPt.Point + axisDir * (length / 2);
                }
            }
            // Final Fallback: Bounding Box Only
            else
            {
                double sizeX = bbox.Max.X - bbox.Min.X;
                double sizeY = bbox.Max.Y - bbox.Min.Y;
                if (sizeX > sizeY)
                {
                    axisDir = XYZ.BasisX;
                    widthDir = XYZ.BasisY;
                    length = sizeX;
                }
                else
                {
                    axisDir = XYZ.BasisY;
                    widthDir = XYZ.BasisX;
                    length = sizeY;
                }
                startPt = center - axisDir * (length / 2);
                endPt = center + axisDir * (length / 2);
            }

            double width = GetWidth(foundation, widthDir, bbox);
            double thickness = bbox.Max.Z - bbox.Min.Z;
            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;

            // Covers
            double cTop = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_TOP);
            double cBot = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double cSide = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_OTHER);

            // --- Stirrups ---
            RebarBarType transType = view.TransBarType;
            RebarShape transShape = view.TransShape;
            if (transType == null || transShape == null) return false;

            double transDia = transType.BarModelDiameter;
            double stW = width - 2 * cSide;
            double stH = thickness - cTop - cBot;

            if (stW <= 0 || stH <= 0) return false;

            // Create Stirrup Shape
            // We need a rectangle profile centered at the section
            // Start offset
            double startOffsetFt = view.TransStartOffsetMM / 304.8;
            XYZ stirrupOrigin = startPt + axisDir * startOffsetFt;
            // Adjust Z to average
            stirrupOrigin = new XYZ(stirrupOrigin.X, stirrupOrigin.Y, (zMax + zMin) / 2);

            // Calculate corners in local coordinates (Width, Height)
            // Z-center offset to account for uneven covers
            double zCenterOff = (cBot - cTop) / 2;

            XYZ p1 = new XYZ(-stW / 2, 0, -stH / 2 + zCenterOff);
            XYZ p2 = new XYZ(stW / 2, 0, -stH / 2 + zCenterOff);
            XYZ p3 = new XYZ(stW / 2, 0, stH / 2 + zCenterOff);
            XYZ p4 = new XYZ(-stW / 2, 0, stH / 2 + zCenterOff);

            // Transform to Global
             XYZ ToGlobal(XYZ pt) => stirrupOrigin + widthDir * pt.X + XYZ.BasisZ * pt.Z;

             List<Curve> curves = new List<Curve>
             {
                 Line.CreateBound(ToGlobal(p3), ToGlobal(p4)),
                 Line.CreateBound(ToGlobal(p4), ToGlobal(p1)),
                 Line.CreateBound(ToGlobal(p1), ToGlobal(p2)),
                 Line.CreateBound(ToGlobal(p2), ToGlobal(p3))
             };

             // Create Stirrup
             DBRebar stirrup = DBRebar.CreateFromCurves(doc, RebarStyle.StirrupTie, transType, view.TransHookStart, view.TransHookEnd,
                 foundation, axisDir, curves, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);

             if (stirrup != null)
             {
                // try { stirrup.RebarShapeId = transShape.Id; } catch { }

                 var accessor = stirrup.GetShapeDrivenAccessor();
                 double arrayLen = length - 2 * startOffsetFt; // Assume symmetric end offset for stirrups as simplified logic
                 if (arrayLen > 0)
                 {
                     double spacingFt = view.TransSpacingMM / 304.8;
                     accessor.SetLayoutAsMaximumSpacing(spacingFt, arrayLen, true, true, true);
                 }
                 
                 // Apply Visibility
                 if (doc.ActiveView is View3D view3D)
                     stirrup.SetUnobscuredInView(view3D, true);
             }

             // --- Longitudinal Bars ---
             double innerOffW = cSide + transDia;
             double distWidth = width - 2 * innerOffW;
             
             double longStartOff = view.LongStartOffsetMM / 304.8;
             double longEndOff = view.LongEndOffsetMM / 304.8;


             // Bottom Bars
             if (view.EnableBottom && view.BottomBarType != null && view.BottomCount > 0)
             {
                 double dia = view.BottomBarType.BarModelDiameter;
                 double zPos = zMin + cBot + transDia + dia / 2;
                 CreateLongitudinalLayer(doc, foundation, view.BottomBarType, view.BottomCount, view.BottomHook, 
                     startPt, endPt, axisDir, widthDir, distWidth, zPos, longStartOff, longEndOff, false);
             }

             // Top Bars
             if (view.EnableTop && view.TopBarType != null && view.TopCount > 0)
             {
                double dia = view.TopBarType.BarModelDiameter;
                double zPos = zMax - cTop - transDia - dia / 2;
                CreateLongitudinalLayer(doc, foundation, view.TopBarType, view.TopCount, view.TopHook, 
                     startPt, endPt, axisDir, widthDir, distWidth, zPos, longStartOff, longEndOff, true);
             }

            return true;
        }

        private void CreateLongitudinalLayer(Document doc, Element host, RebarBarType type, int count, RebarHookType hook,
            XYZ startPt, XYZ endPt, XYZ axisDir, XYZ widthDir, double distWidth, double zPos, 
            double startOff, double endOff, bool isTop)
        {
            // Distribution Line (Center of the layer width)
            XYZ pStart = startPt + axisDir * startOff;
            XYZ pEnd = endPt - axisDir * endOff;
            
            // Adjust Z
            pStart = new XYZ(pStart.X, pStart.Y, zPos);
            pEnd = new XYZ(pEnd.X, pEnd.Y, zPos);

            // Calculate start point for the FIRST bar (usually far left)
            // But Rebar.CreateFromCurves with FixedNumber distributes along the plane normal.
            // We define the curve of the MAIN bar.
            // Let's define the bar at the Center, then distribute perpendicular to it (along widthDir).
            
            // Correct approach: Define ONE curve, then SetLayoutAsFixedNumber with distribution width.
            // If we define the curve at "Start - DistributionWidth/2", the distribution will spread "DistributionWidth".

            // Let's place the curve at: Center - Width/2 (Far Left Edge of distribution)
            XYZ barStart = pStart - widthDir * (distWidth / 2);
            XYZ barEnd = pEnd - widthDir * (distWidth / 2);
            
            Line geomLine = Line.CreateBound(barStart, barEnd);
            List<Curve> curves = new List<Curve> { geomLine };

            RebarHookOrientation orient = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right;
            // Flipped orientation logic from python: Top=Left, Bot=Right (pointing inward)

            DBRebar rebar = DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, hook, hook,
                host, widthDir, curves, orient, orient, true, true);

            if (rebar != null)
            {
                var accessor = rebar.GetShapeDrivenAccessor();
                if (count > 1)
                {
                    accessor.SetLayoutAsFixedNumber(count, distWidth, true, true, true);
                }
                else
                {
                    // Move to center if single bar
                     ElementTransformUtils.MoveElement(doc, rebar.Id, widthDir * (distWidth / 2));
                }

                if (doc.ActiveView is View3D view3D)
                     rebar.SetUnobscuredInView(view3D, true);
            }
        }

        private double GetWidth(Element foundation, XYZ widthDir, BoundingBoxXYZ bbox)
        {
            // Try Parameters
            Parameter p = foundation.LookupParameter("Width");
            if (p != null && p.HasValue) return p.AsDouble();

            // Fallback to Geometry
            // Size of bbox along widthDir
            XYZ size = bbox.Max - bbox.Min;
            // Approximate based on direction
            if (Math.Abs(widthDir.X) > 0.9) return size.X;
            if (Math.Abs(widthDir.Y) > 0.9) return size.Y;
            
            return Math.Min(size.X, size.Y); // Fallback
        }
    }

    public class StripFootingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
