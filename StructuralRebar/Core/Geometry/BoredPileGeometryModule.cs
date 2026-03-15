using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    public static class BoredPileGeometryModule
    {
        public static HostGeometry? Read(Document doc, Element pile)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            GeometryElement geo = pile.get_Geometry(opt);
            if (geo == null) return null;

            PlanarFace bottomFace = null;
            PlanarFace topFace = null;
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            // Find top and bottom faces
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    ProcessSolid(solid, ref bottomFace, ref topFace, ref minZ, ref maxZ);
                }
                else if (obj is GeometryInstance inst)
                {
                    foreach (GeometryObject instObj in inst.GetInstanceGeometry())
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 0)
                        {
                            ProcessSolid(instSolid, ref bottomFace, ref topFace, ref minZ, ref maxZ);
                        }
                    }
                }
            }

            if (bottomFace == null || topFace == null)
            {
                // Fallback to BoundingBox if solid processing failed to find PLANAR faces
                BoundingBoxXYZ bbox = pile.get_BoundingBox(null);
                if (bbox == null) return null;
                minZ = bbox.Min.Z;
                maxZ = bbox.Max.Z;
                XYZ centerPt = (bbox.Min + bbox.Max) / 2.0;
                
                // Create a dummy start/end based on bbox
                XYZ topPt = new XYZ(centerPt.X, centerPt.Y, maxZ);
                XYZ botPt = new XYZ(centerPt.X, centerPt.Y, minZ);
                
                // Try to get horizontal dimensions from bbox if we don't have faces
                double bboxW = bbox.Max.X - bbox.Min.X;
                double bboxL = bbox.Max.Y - bbox.Min.Y;

                // Orientation
                XYZ lAxis1 = XYZ.BasisX;
                XYZ wAxis1 = XYZ.BasisY;
                XYZ hAxis1 = XYZ.BasisZ;
                if (pile is FamilyInstance fi1)
                {
                    Transform trans = fi1.GetTransform();
                    lAxis1 = trans.BasisX.Normalize();
                    wAxis1 = trans.BasisY.Normalize();
                    hAxis1 = trans.BasisZ.Normalize();
                }

                return new HostGeometry(
                    lAxis: lAxis1, wAxis: wAxis1, hAxis: hAxis1, slopeAngle: 0,
                    origin: centerPt, startPoint: topPt, endPoint: botPt,
                    length: bboxL, width: bboxW, height: maxZ - minZ,
                    coverTop: 0, coverBottom: 0, coverExterior: 0, coverInterior: 0, coverOther: 0, // Fallback
                    normal: hAxis1, thickness: maxZ-minZ, source: GeometrySource.BoundingBox,
                    solidZMin: minZ, solidZMax: maxZ
                );
            }

            // Analyze shape: Circular or Rectangular
            bool isCircular = false;
            double radius = 0;
            XYZ centerPlanePt = bottomFace.Origin;
            
            // Check edges of bottom face
            var edges = bottomFace.GetEdgesAsCurveLoops().SelectMany(l => l).ToList();
            if (edges.Count == 1 && edges[0] is Arc arc && arc.IsCyclic)
            {
                isCircular = true;
                radius = arc.Radius;
                centerPlanePt = arc.Center;
            }
            else if (edges.Count == 2 && edges.All(e => e is Arc))
            {
                // Two semi-circles
                isCircular = true;
                radius = (edges[0] as Arc).Radius;
                centerPlanePt = (edges[0] as Arc).Center;
            }
            else
            {
                BoundingBoxXYZ bbox = pile.get_BoundingBox(null);
                if (bbox != null)
                {
                    centerPlanePt = new XYZ((bbox.Min.X + bbox.Max.X) / 2.0, (bbox.Min.Y + bbox.Max.Y) / 2.0, bottomFace.Origin.Z);
                }
            }

            XYZ center = new XYZ(centerPlanePt.X, centerPlanePt.Y, (minZ + maxZ) / 2.0);
            double height = Math.Abs(maxZ - minZ);

            // Orientation
            XYZ lAxis = XYZ.BasisX;
            XYZ wAxis = XYZ.BasisY;
            XYZ hAxis = XYZ.BasisZ;

            if (pile is FamilyInstance fi)
            {
                Transform trans = fi.GetTransform();
                lAxis = trans.BasisX.Normalize();
                wAxis = trans.BasisY.Normalize();
                hAxis = trans.BasisZ.Normalize();
            }

            // Dimensions
            double length = 0;
            double width = 0;

            if (isCircular)
            {
                length = radius * 2;
                width = radius * 2;
            }
            else
            {
                // Calculate size along local axes from edges
                double minL = double.MaxValue, maxL = double.MinValue;
                double minW = double.MaxValue, maxW = double.MinValue;
                foreach (var edge in edges)
                {
                    foreach (var pt in new[] { edge.GetEndPoint(0), edge.GetEndPoint(1) })
                    {
                        double pL = pt.DotProduct(lAxis);
                        double pW = pt.DotProduct(wAxis);
                        minL = Math.Min(minL, pL); maxL = Math.Max(maxL, pL);
                        minW = Math.Min(minW, pW); maxW = Math.Max(maxW, pW);
                    }
                }
                length = maxL - minL;
                width = maxW - minW;
            }

            double cTop = GeometryUtils.GetCoverDistance(doc, pile, BuiltInParameter.CLEAR_COVER_TOP);
            double cBot = GeometryUtils.GetCoverDistance(doc, pile, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double cSide = GeometryUtils.GetCoverDistance(doc, pile, BuiltInParameter.CLEAR_COVER_OTHER);

            return new HostGeometry(
                lAxis: lAxis,
                wAxis: wAxis,
                hAxis: hAxis,
                slopeAngle: 0,
                origin: center,
                startPoint: topFace.Origin,
                endPoint: bottomFace.Origin,
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
                boundaryCurves: edges
            );
        }

        private static void ProcessSolid(Solid solid, ref PlanarFace bottom, ref PlanarFace top, ref double minZ, ref double maxZ)
        {
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace pf)
                {
                    if (pf.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
                    {
                        if (pf.Origin.Z < minZ)
                        {
                            minZ = pf.Origin.Z;
                            bottom = pf;
                        }
                    }
                    else if (pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                    {
                        if (pf.Origin.Z > maxZ)
                        {
                            maxZ = pf.Origin.Z;
                            top = pf;
                        }
                    }
                }
            }
        }
    }
}
