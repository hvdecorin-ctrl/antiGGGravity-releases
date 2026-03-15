using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Extracts arbitrary boundary geometry from Structural Foundation elements.
    /// Supports non-rectangular pads by capturing face boundary loops.
    /// </summary>
    public static class PadShapeGeometryModule
    {
        public static HostGeometry? Read(Document doc, Element foundation)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            GeometryElement geo = foundation.get_Geometry(opt);
            if (geo == null) return null;

            Solid bottomSolid = null;
            PlanarFace bottomFace = null;
            double minZ = double.MaxValue;

            // Find the lowest horizontal face
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
                        {
                            if (pf.Origin.Z < minZ)
                            {
                                minZ = pf.Origin.Z;
                                bottomFace = pf;
                                bottomSolid = solid;
                            }
                        }
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    foreach (GeometryObject instObj in inst.GetInstanceGeometry())
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 0)
                        {
                            foreach (Face face in instSolid.Faces)
                            {
                                if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
                                {
                                    if (pf.Origin.Z < minZ)
                                    {
                                        minZ = pf.Origin.Z;
                                        bottomFace = pf;
                                        bottomSolid = instSolid;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (bottomFace == null) return null;

            // Extract boundary curves
            var boundaryCurves = new List<Curve>();
            foreach (CurveLoop loop in bottomFace.GetEdgesAsCurveLoops())
            {
                foreach (Curve curve in loop)
                {
                    boundaryCurves.Add(curve);
                }
            }

            // Find top face for height and top boundary
            PlanarFace? topFace = null;
            double maxZ = double.MinValue;
            foreach (Face face in bottomSolid.Faces)
            {
                if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                {
                    if (pf.Origin.Z > maxZ)
                    {
                        maxZ = pf.Origin.Z;
                        topFace = pf;
                    }
                }
            }

            var topBoundaryCurves = new List<Curve>();
            if (topFace != null)
            {
                foreach (CurveLoop loop in topFace.GetEdgesAsCurveLoops())
                {
                    foreach (Curve curve in loop)
                    {
                        topBoundaryCurves.Add(curve);
                    }
                }
            }

            // Orientation and dimensions
            BoundingBoxXYZ bbox = foundation.get_BoundingBox(null);
            XYZ center = (bbox.Min + bbox.Max) / 2.0;
            double height = Math.Abs(maxZ - minZ);

            XYZ lAxis = XYZ.BasisX;
            XYZ wAxis = XYZ.BasisY;
            XYZ hAxis = XYZ.BasisZ;

            if (foundation is FamilyInstance fi)
            {
                Transform trans = fi.GetTransform();
                lAxis = trans.BasisX.Normalize();
                wAxis = trans.BasisY.Normalize();
                hAxis = trans.BasisZ.Normalize();
            }

            XYZ size = bbox.Max - bbox.Min;
            double length = Math.Abs(size.DotProduct(lAxis));
            double width = Math.Abs(size.DotProduct(wAxis));

            // Covers
            double cTop = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_TOP);
            double cBot = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double cSide = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_OTHER);

            return new HostGeometry(
                lAxis: lAxis,
                wAxis: wAxis,
                hAxis: hAxis,
                slopeAngle: 0,
                origin: center,
                startPoint: center - lAxis * (length / 2.0),
                endPoint: center + lAxis * (length / 2.0),
                length: length,
                width: width,
                height: height,
                coverTop: cTop,
                coverBottom: cBot,
                coverExterior: cSide,
                coverInterior: cSide,
                coverOther: cSide,
                normal: hAxis,
                thickness: height,
                source: GeometrySource.SolidFaces,
                solidZMin: minZ,
                solidZMax: maxZ,
                boundaryCurves: boundaryCurves,
                topBoundaryCurves: topBoundaryCurves
            );
        }
    }
}
