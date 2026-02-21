using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Extracts geometry from Structural Framing (beam) elements.
    /// Dual-mode: flat axis for horizontal beams, true 3D axis for slanted beams.
    /// Uses solid geometry for reliable Z bounds.
    /// </summary>
    public static class BeamGeometryModule
    {
        private const double SlopeThreshold = 0.01; // ~0.6° — below this = horizontal

        public static HostGeometry Read(Document doc, FamilyInstance beam)
        {
            BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
            XYZ bboxCenter = (bbox.Max + bbox.Min) / 2.0;

            // === Z BOUNDS FROM SOLID GEOMETRY (reliable for offset beams) ===
            var solidZ = GetSolidZBounds(beam);
            double solidZMin = solidZ.ZMin;
            double solidZMax = solidZ.ZMax;

            // --- AXIS DETERMINATION ---
            XYZ lAxis, wAxis;
            double length;
            XYZ startPt, endPt;
            GeometrySource source;
            double slopeAngle = 0;

            Curve pathCurve = (beam.Location as LocationCurve)?.Curve;

            if (pathCurve != null)
            {
                XYZ curveStart = pathCurve.GetEndPoint(0);
                XYZ curveEnd = pathCurve.GetEndPoint(1);
                XYZ rawDir = (curveEnd - curveStart).Normalize();

                // Compute slope angle
                XYZ lFlat = new XYZ(rawDir.X, rawDir.Y, 0);
                slopeAngle = lFlat.GetLength() > 1e-9
                    ? Math.Acos(Math.Min(1.0, Math.Abs(rawDir.DotProduct(lFlat.Normalize()))))
                    : Math.PI / 2;

                // DECISION: flatten for horizontal, keep 3D for slanted
                if (slopeAngle < SlopeThreshold)
                {
                    // Horizontal beam — flatten axis (fixes offset-up/down positioning)
                    lAxis = new XYZ(rawDir.X, rawDir.Y, 0).Normalize();
                }
                else
                {
                    // Slanted beam — keep true 3D axis (rebar follows slope)
                    lAxis = rawDir;
                }

                // Get length/endpoints from solid geometry
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(beam, lAxis);
                if (geomResult.HasValue)
                {
                    length = geomResult.Value.Length;
                    startPt = geomResult.Value.StartPt;
                    endPt = geomResult.Value.EndPt;
                    source = GeometrySource.SolidFaces;
                }
                else
                {
                    length = pathCurve.Length;
                    startPt = curveStart;
                    endPt = curveEnd;
                    source = GeometrySource.LocationCurve;
                }
            }
            else
            {
                Transform trans = beam.GetTransform();
                lAxis = trans.BasisX.Normalize();
                lAxis = new XYZ(lAxis.X, lAxis.Y, 0).Normalize();

                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(beam, lAxis);
                if (geomResult.HasValue)
                {
                    length = geomResult.Value.Length;
                    startPt = geomResult.Value.StartPt;
                    endPt = geomResult.Value.EndPt;
                    source = GeometrySource.SolidFaces;
                }
                else
                {
                    XYZ vBbox = bbox.Max - bbox.Min;
                    XYZ tempWidth = trans.BasisY.Normalize();
                    double lengthGuess = Math.Abs(vBbox.DotProduct(lAxis));
                    double widthGuess = Math.Abs(vBbox.DotProduct(tempWidth));
                    if (widthGuess > lengthGuess)
                    {
                        lAxis = tempWidth;
                        lengthGuess = widthGuess;
                    }
                    length = lengthGuess;
                    startPt = bboxCenter - lAxis * (length / 2);
                    endPt = bboxCenter + lAxis * (length / 2);
                    source = GeometrySource.BoundingBox;
                }
            }

            // --- W-AXIS and H-AXIS ---
            if (Math.Abs(lAxis.DotProduct(XYZ.BasisZ)) > 0.999)
                wAxis = XYZ.BasisX;
            else
                wAxis = XYZ.BasisZ.CrossProduct(lAxis).Normalize();

            XYZ hAxis = lAxis.CrossProduct(wAxis).Normalize();

            // --- DIMENSIONS ---
            double width = GetParamValue(beam, "Width", "b");
            double height = GetParamValue(beam, "Height", "h", "Depth");

            if (width <= 0)
                width = Math.Abs((bbox.Max - bbox.Min).DotProduct(wAxis));
            if (height <= 0)
                height = solidZMax - solidZMin;

            // Use center of solid Z as origin for horizontal beams
            XYZ origin = new XYZ(bboxCenter.X, bboxCenter.Y, (solidZMin + solidZMax) / 2.0);

            // --- COVERS ---
            double coverTop = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_TOP);
            double coverBot = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double coverOther = GeometryUtils.GetCoverDistance(doc, beam, BuiltInParameter.CLEAR_COVER_OTHER);

            return new HostGeometry(
                lAxis: lAxis,
                wAxis: wAxis,
                hAxis: hAxis,
                slopeAngle: slopeAngle,
                origin: origin,
                startPoint: startPt,
                endPoint: endPt,
                length: length,
                width: width,
                height: height,
                coverTop: coverTop,
                coverBottom: coverBot,
                coverExterior: 0,
                coverInterior: 0,
                coverOther: coverOther,
                normal: null,
                thickness: 0,
                source: source,
                solidZMin: solidZMin,
                solidZMax: solidZMax
            );
        }

        /// <summary>
        /// Extracts true Z bounds from actual solid geometry vertices.
        /// </summary>
        private static (double ZMin, double ZMax) GetSolidZBounds(FamilyInstance beam)
        {
            double zMin = double.MaxValue;
            double zMax = double.MinValue;

            try
            {
                Options opt = new Options
                {
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };
                GeometryElement geom = beam.get_Geometry(opt);
                if (geom == null) goto fallback;

                foreach (GeometryObject g in geom)
                    CollectSolidZBounds(g, ref zMin, ref zMax);

                if (zMin < double.MaxValue && zMax > double.MinValue)
                    return (zMin, zMax);
            }
            catch { }

            fallback:
            BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
            return (bbox.Min.Z, bbox.Max.Z);
        }

        private static void CollectSolidZBounds(GeometryObject g, ref double zMin, ref double zMax)
        {
            if (g is Solid solid && solid.Volume > 0)
            {
                foreach (Edge edge in solid.Edges)
                {
                    var pts = edge.Tessellate();
                    foreach (var pt in pts)
                    {
                        if (pt.Z < zMin) zMin = pt.Z;
                        if (pt.Z > zMax) zMax = pt.Z;
                    }
                }
            }
            else if (g is GeometryInstance gi)
            {
                foreach (GeometryObject gg in gi.GetInstanceGeometry())
                    CollectSolidZBounds(gg, ref zMin, ref zMax);
            }
        }

        private static double GetParamValue(Element elem, params string[] names)
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
    }
}
