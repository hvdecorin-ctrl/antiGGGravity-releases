using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    public class CornerInfo
    {
        public Wall Wall1 { get; set; }
        public Wall Wall2 { get; set; }
        public XYZ Point { get; set; }
        public XYZ Dir1 { get; set; }
        public XYZ Dir2 { get; set; }
        public XYZ Normal1 { get; set; }
        public XYZ Normal2 { get; set; }
        public double Thickness { get; set; }
        public double ZMin { get; set; }
        public double ZMax { get; set; }
    }

    /// <summary>
    /// Extracts corner geometry between two intersecting walls.
    /// Finds intersection point, directions away from corner, and appropriate normals.
    /// </summary>
    public static class WallCornerGeometryModule
    {
        public static List<CornerInfo> FindCorners(List<Wall> walls)
        {
            var corners = new List<CornerInfo>();
            for (int i = 0; i < walls.Count; i++)
            {
                for (int j = i + 1; j < walls.Count; j++)
                {
                    XYZ intersect = GetIntersect(walls[i], walls[j]);
                    if (intersect != null)
                    {
                        var info = BuildCornerInfo(walls[i], walls[j], intersect);
                        if (info != null) corners.Add(info);
                    }
                }
            }
            return corners;
        }

        private static XYZ GetIntersect(Wall w1, Wall w2)
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
                {
                    if (p1.DistanceTo(p2) < tolerance) return (p1 + p2) / 2.0;
                }
            }
            return null;
        }

        private static CornerInfo BuildCornerInfo(Wall w1, Wall w2, XYZ cp)
        {
            LocationCurve loc1 = w1.Location as LocationCurve;
            LocationCurve loc2 = w2.Location as LocationCurve;
            if (loc1 == null || loc2 == null) return null;

            XYZ dir1 = GetDirectionAway(loc1.Curve, cp);
            XYZ dir2 = GetDirectionAway(loc2.Curve, cp);

            XYZ tangent1 = (loc1.Curve as Line).Direction;
            XYZ normal1 = new XYZ(-tangent1.Y, tangent1.X, 0);

            XYZ tangent2 = (loc2.Curve as Line).Direction;
            XYZ normal2 = new XYZ(-tangent2.Y, tangent2.X, 0);

            BoundingBoxXYZ bbox = w1.get_BoundingBox(null);

            return new CornerInfo
            {
                Wall1 = w1,
                Wall2 = w2,
                Point = cp,
                Dir1 = dir1,
                Dir2 = dir2,
                Normal1 = normal1,
                Normal2 = normal2,
                Thickness = w1.Width,
                ZMin = bbox.Min.Z,
                ZMax = bbox.Max.Z
            };
        }

        private static XYZ GetDirectionAway(Curve curve, XYZ cornerPoint)
        {
            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            if (p0.DistanceTo(cornerPoint) < p1.DistanceTo(cornerPoint))
                return (p1 - p0).Normalize();
            else
                return (p0 - p1).Normalize();
        }
    }
}
