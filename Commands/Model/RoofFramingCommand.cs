using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.Model;

namespace antiGGGravity.Commands.Model
{
    [Transaction(TransactionMode.Manual)]
    public class RoofFramingCommand : IExternalCommand
    {
        private const double TOL = 1e-6;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Collect Framing Types
                var framingTypes = CollectFramingTypes(doc);
                if (!framingTypes.Any())
                {
                    TaskDialog.Show("Error", "No structural framing types found in the project.");
                    return Result.Cancelled;
                }

                // 2. Selection
                IList<Reference> refs;
                try
                {
                    refs = uidoc.Selection.PickObjects(ObjectType.Face, "Select roof or slab faces, then press Finish.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (refs == null || !refs.Any()) return Result.Cancelled;

                List<Face> faces = new List<Face>();
                foreach (Reference @ref in refs)
                {
                    Element el = doc.GetElement(@ref);
                    Face face = el.GetGeometryObjectFromReference(@ref) as Face;
                    if (face != null) faces.Add(face);
                }

                if (!faces.Any()) return Result.Failed;

                // 3. Show View
                var view = new RoofFramingView(doc, framingTypes);
                view.ShowDialog();

                if (!view.IsConfirmed) return Result.Cancelled;

                // 4. Generate Logic
                using (Transaction t = new Transaction(doc, "Create Purlins & Rafters"))
                {
                    t.Start();

                    List<(Line, double, XYZ)> allPurlinLines = new List<(Line, double, XYZ)>();
                    List<(Line, double, XYZ)> allRafterLines = new List<(Line, double, XYZ)>();
                    List<(Line, double, XYZ)> edgeLines = new List<(Line, double, XYZ)>();

                    double purlinSpacingInternal = RevitCompatibility.MmToInternal(view.PurlinSpacing);
                    double rafterSpacingInternal = RevitCompatibility.MmToInternal(view.RafterSpacing);
                    double purlinOffsetInternal = RevitCompatibility.MmToInternal(view.PurlinOffset);
                    double rafterOffsetInternal = RevitCompatibility.MmToInternal(view.RafterOffset);
                    double finishingOffsetInternal = RevitCompatibility.MmToInternal(view.FinishingOffsetValue);

                    foreach (Face face in faces)
                    {
                        XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                        XYZ rafterDir = ComputeSlopeDirection(face);
                        XYZ purlinDir = normal.CrossProduct(rafterDir);

                        if (purlinDir.GetLength() < TOL) continue;
                        purlinDir = purlinDir.Normalize();

                        if (view.GenPurlins)
                        {
                            double rotation = view.RotateSlope ? ComputeBeamRotation(normal, purlinDir) : 0;
                            var lines = GenerateGridLines(face, rafterDir, purlinDir, normal, purlinSpacingInternal, purlinOffsetInternal);
                            allPurlinLines.AddRange(lines.Select(l => (l, rotation, normal)));
                        }

                        if (view.GenRafters)
                        {
                            double rotation = view.RotateSlope ? ComputeBeamRotation(normal, rafterDir) : 0;
                            var lines = GenerateGridLines(face, purlinDir, rafterDir, normal, rafterSpacingInternal, rafterOffsetInternal);
                            allRafterLines.AddRange(lines.Select(l => (l, rotation, normal)));
                        }
                    }

                    // Edge Rafters
                    if (view.GenEdgeRafters && faces.Count >= 2)
                    {
                        var edgeData = FindIntersectionEdges(faces);
                        edgeLines.AddRange(edgeData.Select(e => (e.Item1, 0.0, e.Item2)));
                    }

                    // Apply Under-Purlin Offset
                    if (view.RafterUnderPurlin && (allRafterLines.Any() || edgeLines.Any()))
                    {
                        double drop = GetBeamDepth(view.SelectedPurlinType);
                        allRafterLines = OffsetLinesNormal(allRafterLines, -drop);
                        edgeLines = OffsetLinesNormal(edgeLines, -drop);
                    }

                    // Apply Finishing Offset
                    if (view.ApplyFinishingOffset && finishingOffsetInternal > TOL)
                    {
                        allPurlinLines = OffsetLinesNormal(allPurlinLines, -finishingOffsetInternal);
                        allRafterLines = OffsetLinesNormal(allRafterLines, -finishingOffsetInternal);
                        edgeLines = OffsetLinesNormal(edgeLines, -finishingOffsetInternal);
                    }

                    // 5. Placement
                    Level level = GetNearestLevel(doc, faces[0].Evaluate(new UV(0.5, 0.5)));
                    if (level == null) return Result.Failed;

                    int purlinCount = 0, rafterCount = 0, edgeCount = 0;

                    ActivateSymbol(view.SelectedPurlinType);
                    foreach (var item in allPurlinLines)
                    {
                        var inst = doc.Create.NewFamilyInstance(item.Item1, view.SelectedPurlinType, level, StructuralType.Beam);
                        ApplyRotation(inst, item.Item2);
                        purlinCount++;
                    }

                    ActivateSymbol(view.SelectedRafterType);
                    foreach (var item in allRafterLines)
                    {
                        var inst = doc.Create.NewFamilyInstance(item.Item1, view.SelectedRafterType, level, StructuralType.Beam);
                        ApplyRotation(inst, item.Item2);
                        rafterCount++;
                    }

                    ActivateSymbol(view.SelectedEdgeType);
                    foreach (var item in edgeLines)
                    {
                        doc.Create.NewFamilyInstance(item.Item1, view.SelectedEdgeType, level, StructuralType.Beam);
                        edgeCount++;
                    }

                    t.Commit();
                    
                    TaskDialog.Show("Purlin & Rafter", string.Format("Created {0} purlins, {1} rafters, {2} edge members.", purlinCount, rafterCount, edgeCount));
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private Dictionary<string, FamilySymbol> CollectFramingTypes(Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilySymbol));

            var result = new Dictionary<string, FamilySymbol>();
            foreach (FamilySymbol sym in collector)
            {
                string name = string.Format("{0} : {1}", sym.Family.Name, sym.Name);
                if (!result.ContainsKey(name)) result.Add(name, sym);
            }
            return result;
        }

        private XYZ ComputeSlopeDirection(Face face)
        {
            UV mid = new UV(0.5, 0.5);
            XYZ normal = face.ComputeNormal(mid);
            XYZ gravity = new XYZ(0, 0, -1);
            double dot = normal.DotProduct(gravity);
            XYZ slope = gravity - normal.Multiply(dot);
            if (slope.GetLength() < 1e-9)
                return face.ComputeDerivatives(mid).BasisX;
            return slope.Normalize();
        }

        private double ComputeBeamRotation(XYZ faceNormal, XYZ beamDir)
        {
            XYZ zUp = new XYZ(0, 0, 1);
            XYZ zProj = zUp - beamDir.Multiply(zUp.DotProduct(beamDir));
            XYZ nProj = faceNormal - beamDir.Multiply(faceNormal.DotProduct(beamDir));

            if (zProj.GetLength() < TOL || nProj.GetLength() < TOL) return 0;

            zProj = zProj.Normalize();
            nProj = nProj.Normalize();

            double cosA = zProj.DotProduct(nProj);
            cosA = Math.Max(-1.0, Math.Min(1.0, cosA));
            XYZ cross = zProj.CrossProduct(nProj);
            double sinA = cross.DotProduct(beamDir);

            return -Math.Atan2(sinA, cosA);
        }

        private List<Line> GenerateGridLines(Face face, XYZ spacingDir, XYZ lineDir, XYZ normal, double spacing, double offset)
        {
            var vertices = GetFaceVertices(face);
            var spacingProjs = vertices.Select(v => spacingDir.DotProduct(v)).ToList();
            var lineProjs = vertices.Select(v => lineDir.DotProduct(v)).ToList();

            double sMin = spacingProjs.Min(), sMax = spacingProjs.Max();
            double lMin = lineProjs.Min(), lMax = lineProjs.Max();
            double nOff = normal.DotProduct(face.Evaluate(new UV(0.5, 0.5)));

            XYZ baseN = normal.Multiply(nOff);
            double extend = (lMax - lMin) * 0.3;
            List<Line> lines = new List<Line>();

            double s = Math.Floor((sMin - offset) / spacing) * spacing + offset;
            if (s < sMin - TOL) s += spacing;

            while (s <= sMax + TOL)
            {
                XYZ p0 = baseN + spacingDir.Multiply(s) + lineDir.Multiply(lMin - extend);
                XYZ p1 = baseN + spacingDir.Multiply(s) + lineDir.Multiply(lMax + extend);

                if (p0.DistanceTo(p1) > TOL)
                {
                    var clipped = ClipLineToFaceSampled(p0, p1, face);
                    lines.AddRange(clipped);
                }
                s += spacing;
            }
            return lines;
        }

        private List<XYZ> GetFaceVertices(Face face)
        {
            List<XYZ> points = new List<XYZ>();
            foreach (EdgeArray loop in face.EdgeLoops)
            {
                foreach (Edge edge in loop)
                {
                    var curve = edge.AsCurve();
                    points.Add(curve.GetEndPoint(0));
                    points.Add(curve.GetEndPoint(1));
                    points.Add(curve.Evaluate(0.5, true));
                }
            }
            return points;
        }

        private List<Line> ClipLineToFaceSampled(XYZ start, XYZ end, Face face, int numSamples = 60)
        {
            double dist = start.DistanceTo(end);
            if (dist < TOL) return new List<Line>();
            XYZ dir = (end - start).Normalize();

            List<(double, bool)> samples = new List<(double, bool)>();
            for (int i = 0; i <= numSamples; i++)
            {
                double t = (double)i / numSamples;
                XYZ pt = start + dir.Multiply(dist * t);
                samples.Add((t, IsPointInsideFace(face, pt)));
            }

            List<Line> result = new List<Line>();
            double? segStartT = null;

            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Item2 && !segStartT.HasValue) 
                    segStartT = samples[i].Item1;
                else if (!samples[i].Item2 && segStartT.HasValue)
                {
                    result.Add(CreateRefinedLine(start, dir, dist, segStartT.Value, samples[i-1].Item1, face));
                    segStartT = null;
                }
            }
            if (segStartT.HasValue)
                result.Add(CreateRefinedLine(start, dir, dist, segStartT.Value, samples.Last().Item1, face));

            return result.Where(l => l != null).ToList();
        }

        private bool IsPointInsideFace(Face face, XYZ pt)
        {
            IntersectionResult proj = face.Project(pt);
            return proj != null && face.IsInside(proj.UVPoint);
        }

        private Line CreateRefinedLine(XYZ start, XYZ dir, double totalDist, double t0, double t1, Face face)
        {
            // Binary search to refine endpoints
            t0 = RefineT(start, dir, totalDist, t0, t0 - 1.0/60, face, true);
            t1 = RefineT(start, dir, totalDist, t1, t1 + 1.0/60, face, false);

            XYZ p0 = start + dir.Multiply(totalDist * t0);
            XYZ p1 = start + dir.Multiply(totalDist * t1);

            if (p0.DistanceTo(p1) > TOL)
                return Line.CreateBound(p0, p1);
            return null;
        }

        private double RefineT(XYZ start, XYZ dir, double totalDist, double tInside, double tOutside, Face face, bool isStart)
        {
            if (tInside < 0 || tInside > 1) return tInside;
            for (int i = 0; i < 12; i++)
            {
                double mid = (tInside + tOutside) / 2;
                if (mid < 0 || mid > 1) break;
                if (IsPointInsideFace(face, start + dir.Multiply(totalDist * mid)))
                    tInside = mid;
                else
                    tOutside = mid;
            }
            return tInside;
        }

        private List<(Line, XYZ)> FindIntersectionEdges(List<Face> faces)
        {
            var edgeMap = new Dictionary<string, (Curve, HashSet<int>)>();
            for (int i = 0; i < faces.Count; i++)
            {
                foreach (EdgeArray loop in faces[i].EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        var curve = edge.AsCurve();
                        string sig = GetEdgeSignature(curve);
                        if (!edgeMap.ContainsKey(sig)) edgeMap.Add(sig, (curve, new HashSet<int>()));
                        edgeMap[sig].Item2.Add(i);
                    }
                }
            }

            var result = new List<(Line, XYZ)>();
            foreach (var data in edgeMap.Values)
            {
                if (data.Item2.Count >= 2)
                {
                    Line line = data.Item1 as Line;
                    if (line == null)
                    {
                        XYZ p0 = data.Item1.GetEndPoint(0), p1 = data.Item1.GetEndPoint(1);
                        if (p0.DistanceTo(p1) > TOL) line = Line.CreateBound(p0, p1);
                    }

                    if (line != null)
                    {
                        XYZ nSum = XYZ.Zero;
                        foreach (int idx in data.Item2) nSum += faces[idx].ComputeNormal(new UV(0.5, 0.5));
                        result.Add((line, nSum.GetLength() > TOL ? nSum.Normalize() : XYZ.BasisZ));
                    }
                }
            }
            return result;
        }

        private string GetEdgeSignature(Curve curve)
        {
            XYZ p0 = curve.GetEndPoint(0), p1 = curve.GetEndPoint(1);
            Func<XYZ, string> f = p => string.Format("{0:F4},{1:F4},{2:F4}", p.X, p.Y, p.Z);
            string s0 = f(p0), s1 = f(p1);
            return string.Compare(s0, s1) < 0 ? s0 + "|" + s1 : s1 + "|" + s0;
        }

        private List<(Line, double, XYZ)> OffsetLinesNormal(List<(Line, double, XYZ)> items, double dist)
        {
            return items.Select(item => {
                XYZ delta = item.Item3.Multiply(dist);
                return (Line.CreateBound(item.Item1.GetEndPoint(0) + delta, item.Item1.GetEndPoint(1) + delta), item.Item2, item.Item3);
            }).ToList();
        }

        private double GetBeamDepth(FamilySymbol sym)
        {
            var p = sym.LookupParameter("d") ?? sym.LookupParameter("Depth") ?? sym.LookupParameter("Height");
            return (p != null && p.HasValue) ? p.AsDouble() : RevitCompatibility.MmToInternal(200);
        }

        private Level GetNearestLevel(Document doc, XYZ point)
        {
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            return levels.OrderBy(l => Math.Abs(l.Elevation - point.Z)).FirstOrDefault();
        }

        private void ActivateSymbol(FamilySymbol sym)
        {
            if (!sym.IsActive) sym.Activate();
        }

        private void ApplyRotation(FamilyInstance inst, double angle)
        {
            if (Math.Abs(angle) < TOL) return;
            var p = inst.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE);
            if (p != null) p.Set(angle);
        }
    }
}
