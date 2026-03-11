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

            // === Z BOUNDS FROM UNCUT SOLID GEOMETRY (reliable for offset beams) ===
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
        /// Uses GetOriginalGeometry to retrieve the beam shape BEFORE any slab cuts or joins.
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
                
                // Get original geometry to bypass slab cuts and joins distorting the Z bounds.
                // NOTE: GetOriginalGeometry returns geometry in the family's LOCAL coordinate system.
                GeometryElement geom = beam.GetOriginalGeometry(opt);
                if (geom == null) goto fallback;

                // We must transform local points to world space to get true Z bounds.
                Transform tf = beam.GetTransform();

                foreach (GeometryObject g in geom)
                    CollectSolidZBounds(g, tf, ref zMin, ref zMax);

                if (zMin < double.MaxValue && zMax > double.MinValue)
                    return (zMin, zMax);
            }
            catch { }

            fallback:
            BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
            return (bbox.Min.Z, bbox.Max.Z);
        }

        private static void CollectSolidZBounds(GeometryObject g, Transform tf, ref double zMin, ref double zMax)
        {
            if (g is Solid solid && solid.Volume > 0)
            {
                foreach (Edge edge in solid.Edges)
                {
                    var pts = edge.Tessellate();
                    foreach (XYZ pt in pts)
                    {
                        XYZ worldPt = tf.OfPoint(pt);
                        if (worldPt.Z < zMin) zMin = worldPt.Z;
                        if (worldPt.Z > zMax) zMax = worldPt.Z;
                    }
                }
            }
            else if (g is GeometryInstance inst)
            {
                // Unlikely for GetOriginalGeometry, but just in case
                Transform instTf = tf.Multiply(inst.Transform);
                foreach (GeometryObject instObj in inst.GetInstanceGeometry())
                {
                    CollectSolidZBounds(instObj, instTf, ref zMin, ref zMax);
                }
            }
        }

        /// <summary>
        /// Computes the true bottom-Z of a beam from its reference level, offset,
        /// and z-justification — unaffected by Revit geometry joins / slab cuts.
        /// </summary>
        private static double GetBeamBottomZ(Document doc, FamilyInstance beam, double paramHeight)
        {
            double refZ = 0;
            Curve pathCurve = (beam.Location as LocationCurve)?.Curve;
            if (pathCurve != null)
            {
                // LocationCurve natively includes both the Level Elevation AND Start/End Level Offsets
                refZ = Math.Min(pathCurve.GetEndPoint(0).Z, pathCurve.GetEndPoint(1).Z);
            }
            else
            {
                // Fallback 
                double levelZ = 0;
                Parameter levelParam = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
                {
                    Level refLevel = doc.GetElement(levelParam.AsElementId()) as Level;
                    if (refLevel != null) levelZ = refLevel.Elevation;
                }
                Parameter startOffParam = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                double sOff = startOffParam != null && startOffParam.HasValue ? startOffParam.AsDouble() : 0;
                refZ = levelZ + sOff;
            }

            // --- Vertical offset ("z Offset Value") ---
            double zOffset = 0;
            Parameter offsetParam = beam.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE);
            if (offsetParam != null && offsetParam.HasValue)
                zOffset = offsetParam.AsDouble();

            // --- z Justification (Top=0, Center=1, Bottom=2, Origin=3) ---
            double refElevation = refZ + zOffset;
            int zJustify = 0;
            Parameter justifyParam = beam.get_Parameter(BuiltInParameter.Z_JUSTIFICATION);
            if (justifyParam != null && justifyParam.HasValue)
                zJustify = justifyParam.AsInteger();

            double bottomZ;
            switch (zJustify)
            {
                case 1: // Center
                    bottomZ = refElevation - paramHeight / 2.0;
                    break;
                case 2: // Bottom
                    bottomZ = refElevation;
                    break;
                case 0: // Top
                case 3: // Origin (usually top for structural framing)
                default:
                    bottomZ = refElevation - paramHeight;
                    break;
            }

            return bottomZ;
        }

        public static double GetParamValue(Element elem, params string[] names)
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
