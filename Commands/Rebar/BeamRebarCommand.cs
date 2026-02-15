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
    public class BeamRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new BeamRebarView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Beams
            List<FamilyInstance> beams = new List<FamilyInstance>();
            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, new BeamSelectionFilter(), "Select beams to reinforce (press Finish)");
                beams = refs.Select(r => doc.GetElement(r.ElementId) as FamilyInstance).Where(b => b != null).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (beams.Count == 0) return Result.Cancelled;

            // 3. Process
            int beamsProcessed = 0;
            using (Transaction t = new Transaction(doc, "Generate Beam Rebar"))
            {
                t.Start();
                foreach (var beam in beams)
                {
                    if (view.RemoveExisting) DeleteExistingRebar(doc, beam);

                    if (ProcessBeam(doc, beam, view)) beamsProcessed++;
                }
                t.Commit();
            }

            TaskDialog.Show("Result", $"Successfully reinforced {beamsProcessed} beams.");
            return Result.Succeeded;
        }

        private bool ProcessBeam(Document doc, FamilyInstance beam, BeamRebarView view)
        {
             try
            {
                BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
                XYZ center = (bbox.Max + bbox.Min) / 2.0;

                // Determine Primary Axes and Orientation - matching Python logic
                XYZ axisDir = null;
                XYZ widthDir = null;
                double length = 0;
                XYZ startPt = null;
                XYZ endPt = null;

                Curve pathCurve = null;
                if (beam.Location is LocationCurve locCurve)
                    pathCurve = locCurve.Curve;

                if (pathCurve != null)
                {
                    XYZ curveStart = pathCurve.GetEndPoint(0);
                    XYZ curveEnd = pathCurve.GetEndPoint(1);
                    axisDir = (curveEnd - curveStart).Normalize();
                    
                    if (Math.Abs(axisDir.Z) < 0.999)
                        axisDir = new XYZ(axisDir.X, axisDir.Y, 0).Normalize();
                    widthDir = XYZ.BasisZ.CrossProduct(axisDir).Normalize();
                    
                    var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(beam, axisDir);
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
                else
                {
                    // FamilyInstance without LocationCurve - use Transform
                    Transform trans = beam.GetTransform();
                    axisDir = trans.BasisX.Normalize();
                    widthDir = trans.BasisY.Normalize();
                    
                    axisDir = new XYZ(axisDir.X, axisDir.Y, 0).Normalize();
                    widthDir = XYZ.BasisZ.CrossProduct(axisDir).Normalize();
                    
                    var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(beam, axisDir);
                    if (geomResult.HasValue)
                    {
                        length = geomResult.Value.Length;
                        startPt = geomResult.Value.StartPt;
                        endPt = geomResult.Value.EndPt;
                    }
                    else
                    {
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
                        startPt = center - axisDir * (length / 2);
                        endPt = center + axisDir * (length / 2);
                    }
                }

                // Extract Dimensions (use parameters to ignore slab cuts)
                double beamWidth = GetParamValue(beam, "Width", "b");
                double beamHeight = GetParamValue(beam, "Height", "h", "Depth");
                
                if (beamWidth <= 0)
                    beamWidth = Math.Abs((bbox.Max - bbox.Min).DotProduct(widthDir));
                
                // Use bbox for Z bounds, but parameter for HEIGHT if available
                double zMinBbox = bbox.Min.Z;
                double zMaxBbox = bbox.Max.Z;
                double zMin, zMax;
                
                if (beamHeight > 0)
                {
                    // Use HEIGHT parameter to calculate correct Z bounds
                    zMin = zMinBbox;
                    zMax = zMin + beamHeight;
                }
                else
                {
                    zMin = zMinBbox;
                    zMax = zMaxBbox;
                    beamHeight = zMax - zMin;
                }
                
                // Cover
                double cBot = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_BOTTOM);
                double cTop = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_TOP);
                double cSide = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_OTHER);

                // 1. Stirrups
                double transDia = view.TransType.BarModelDiameter;
                double stW = beamWidth - 2 * cSide;
                double stH = beamHeight - cTop - cBot;
                double zCenterOff = (cBot - cTop) / 2.0;

                XYZ stirrupOrigin = startPt + axisDir * (view.StartOffsetMM / 304.8);
                stirrupOrigin = new XYZ(stirrupOrigin.X, stirrupOrigin.Y, (zMax + zMin) / 2.0);

                List<Curve> stirrupCurves = CreateStirrupLoop(stirrupOrigin, widthDir, stW, stH, zCenterOff);
                DBRebar stirrup = DBRebar.CreateFromCurves(doc, RebarStyle.StirrupTie, view.TransType, view.HookStart, view.HookEnd,
                    beam, axisDir, stirrupCurves, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                
                if (stirrup != null)
                {
                    double arrayLen = length - 2 * (view.StartOffsetMM / 304.8);
                    if (arrayLen > 0)
                        stirrup.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(view.TransSpacingMM / 304.8, arrayLen, true, true, true);
                }

                // 2. Longitudinal Layers
                double innerOffsetW = cSide + transDia;
                double distWidth = beamWidth - 2 * innerOffsetW;
                double minLayerGap = 20.0 / 304.8;

                // Top Group
                double topZ = zMax - cTop - transDia;
                if (view.T1Enabled)
                {
                    CreateLayer(doc, beam, view.T1Type, view.T1Count, topZ - (view.T1Type.BarModelDiameter / 2.0), 
                        startPt, endPt, axisDir, widthDir, distWidth, cSide, view.TopHookStart, view.TopHookEnd, true);
                    topZ -= (view.T1Type.BarModelDiameter + minLayerGap);
                }
                if (view.T2Enabled)
                {
                    CreateLayer(doc, beam, view.T2Type, view.T2Count, topZ - (view.T2Type.BarModelDiameter / 2.0), 
                        startPt, endPt, axisDir, widthDir, distWidth, cSide, view.TopHookStart, view.TopHookEnd, true);
                }

                // Bottom Group
                double botZ = zMin + cBot + transDia;
                if (view.B1Enabled)
                {
                    CreateLayer(doc, beam, view.B1Type, view.B1Count, botZ + (view.B1Type.BarModelDiameter / 2.0), 
                        startPt, endPt, axisDir, widthDir, distWidth, cSide, view.BotHookStart, view.BotHookEnd, false);
                    botZ += (view.B1Type.BarModelDiameter + minLayerGap);
                }
                if (view.B2Enabled)
                {
                    CreateLayer(doc, beam, view.B2Type, view.B2Count, botZ + (view.B2Type.BarModelDiameter / 2.0), 
                        startPt, endPt, axisDir, widthDir, distWidth, cSide, view.BotHookStart, view.BotHookEnd, false);
                }

                return true;
            }
            catch { return false; }
        }

        private double GetParamValue(Element elem, params string[] names)
        {
            foreach (string name in names)
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        Element elemType = elem.Document.GetElement(typeId);
                        if (elemType != null)
                            p = elemType.LookupParameter(name);
                    }
                }
                if (p != null && p.HasValue) return p.AsDouble();
            }
            return 0;
        }

        private List<Curve> CreateStirrupLoop(XYZ origin, XYZ widthDir, double w, double h, double zCenterOff = 0)
        {
            // Match Python: rectangle centered at origin, offset by zCenterOff for uneven covers
            XYZ p1 = origin - widthDir * (w / 2.0) + XYZ.BasisZ * (-h / 2.0 + zCenterOff);
            XYZ p2 = origin + widthDir * (w / 2.0) + XYZ.BasisZ * (-h / 2.0 + zCenterOff);
            XYZ p3 = origin + widthDir * (w / 2.0) + XYZ.BasisZ * (h / 2.0 + zCenterOff);
            XYZ p4 = origin - widthDir * (w / 2.0) + XYZ.BasisZ * (h / 2.0 + zCenterOff);

            // Python order: p3->p4, p4->p1, p1->p2, p2->p3 (hook at top-right corner)
            return new List<Curve> {
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3)
            };
        }

        private void CreateLayer(Document doc, FamilyInstance host, RebarBarType type, int count, double z,
            XYZ startPt, XYZ endPt, XYZ axisDir, XYZ widthDir, double distWidth, double coverSide,
            RebarHookType hS, RebarHookType hE, bool isTop)
        {
            if (count < 1) return;

            XYZ s = startPt + axisDir * coverSide;
            XYZ e = endPt - axisDir * coverSide;
            
            XYZ pStart = new XYZ(s.X, s.Y, z) - widthDir * (distWidth / 2.0);
            XYZ pEnd = new XYZ(e.X, e.Y, z) - widthDir * (distWidth / 2.0);
            
            Curve line = Line.CreateBound(pStart, pEnd);
            RebarHookOrientation orient = isTop ? RebarHookOrientation.Left : RebarHookOrientation.Right;

            try
            {
                DBRebar rb = DBRebar.CreateFromCurves(doc, RebarStyle.Standard, type, hS, hE, host, widthDir, new List<Curve> { line }, orient, orient, true, true);
                if (rb != null)
                {
                    if (count > 1)
                        rb.GetShapeDrivenAccessor().SetLayoutAsFixedNumber(count, distWidth, true, true, true);
                    else
                        ElementTransformUtils.MoveElement(doc, rb.Id, widthDir * (distWidth / 2.0));
                }
            }
            catch { }
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

        private class BeamSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
