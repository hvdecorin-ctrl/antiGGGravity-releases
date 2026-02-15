using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.Utilities
{
    public static class GeometryUtils
    {
        public static double GetCoverDistance(Document doc, Element element, BuiltInParameter coverParam, double defaultFeet = 0.0833) // 25mm default
        {
            Parameter p = element.get_Parameter(coverParam);
            if (p != null)
            {
                ElementId coverId = p.AsElementId();
                if (coverId != ElementId.InvalidElementId)
                {
                    Element coverType = doc.GetElement(coverId);
                    if (coverType != null)
                    {
                        Parameter coverDistParam = coverType.get_Parameter(BuiltInParameter.COVER_TYPE_LENGTH);
                        if (coverDistParam != null)
                        {
                            return coverDistParam.AsDouble();
                        }
                    }
                }
            }
            return defaultFeet;
        }

        public static XYZ FindCornerPoint(Wall wall1, Wall wall2)
        {
            Curve curve1 = (wall1.Location as LocationCurve)?.Curve;
            Curve curve2 = (wall2.Location as LocationCurve)?.Curve;

            if (curve1 == null || curve2 == null) return null;
            if (!(curve1 is Line) || !(curve2 is Line)) return null;

            XYZ p1Start = curve1.GetEndPoint(0);
            XYZ p1End = curve1.GetEndPoint(1);
            XYZ p2Start = curve2.GetEndPoint(0);
            XYZ p2End = curve2.GetEndPoint(1);

            // Check if endpoints are close
            double tolerance = 1.0; // 1 foot
            
            if (p1Start.DistanceTo(p2Start) < tolerance) return Midpoint(p1Start, p2Start);
            if (p1Start.DistanceTo(p2End) < tolerance) return Midpoint(p1Start, p2End);
            if (p1End.DistanceTo(p2Start) < tolerance) return Midpoint(p1End, p2Start);
            if (p1End.DistanceTo(p2End) < tolerance) return Midpoint(p1End, p2End);

            return null; // No corner found
        }

        private static XYZ Midpoint(XYZ p1, XYZ p2)
        {
            return new XYZ((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, p1.Z);
        }

        public static XYZ GetWallDirectionFromCorner(Wall wall, XYZ cornerPoint)
        {
            Curve curve = (wall.Location as LocationCurve)?.Curve;
            if (curve == null) return XYZ.Zero;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);

            double d0 = p0.DistanceTo(cornerPoint);
            double d1 = p1.DistanceTo(cornerPoint);

            if (d0 < d1)
                return (p1 - p0).Normalize();
            else
                return (p0 - p1).Normalize();
        }

        /// <summary>
        /// Gets 2D horizontal vector from curve.
        /// </summary>
        public static XYZ GetDirection(Curve curve)
        {
            if (curve is Line line)
            {
                XYZ dir = line.Direction;
                return new XYZ(dir.X, dir.Y, 0).Normalize();
            }
            return XYZ.BasisX;
        }

        /// <summary>
        /// Gets perpendicular vector (Z-cross product).
        /// </summary>
        public static XYZ GetPerpendicular(XYZ direction)
        {
            return XYZ.BasisZ.CrossProduct(direction).Normalize();
        }

        /// <summary>
        /// Extract true length and endpoints from solid geometry faces perpendicular to axis.
        /// More accurate than LocationCurve for extended/rotated elements.
        /// Returns null if extraction fails.
        /// </summary>
        public static (double Length, XYZ StartPt, XYZ EndPt)? GetGeometryLengthAndEndpoints(Element element, XYZ axisDir)
        {
            try
            {
                Options opt = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = false
                };
                GeometryElement geom = element.get_Geometry(opt);
                if (geom == null) return null;

                // Collect all solids
                List<Solid> solids = new List<Solid>();
                foreach (GeometryObject g in geom)
                {
                    if (g is Solid solid && solid.Volume > 0)
                    {
                        solids.Add(solid);
                    }
                    else if (g is GeometryInstance gi)
                    {
                        foreach (GeometryObject gg in gi.GetInstanceGeometry())
                        {
                            if (gg is Solid s && s.Volume > 0)
                                solids.Add(s);
                        }
                    }
                }

                if (!solids.Any()) return null;

                // Find faces perpendicular to axisDir (end faces)
                List<(XYZ Centroid, XYZ Normal)> endFaces = new List<(XYZ, XYZ)>();
                double tolerance = 0.1; // ~6 degrees tolerance for face normal alignment

                foreach (Solid solid in solids)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            XYZ normal = planarFace.FaceNormal;
                            double dot = Math.Abs(normal.DotProduct(axisDir));
                            if (dot > (1.0 - tolerance))
                            {
                                // Calculate face centroid
                                BoundingBoxUV bbox = planarFace.GetBoundingBox();
                                double uMid = (bbox.Min.U + bbox.Max.U) / 2;
                                double vMid = (bbox.Min.V + bbox.Max.V) / 2;
                                XYZ centroid = planarFace.Evaluate(new UV(uMid, vMid));
                                endFaces.Add((centroid, normal));
                            }
                        }
                    }
                }

                if (endFaces.Count < 2) return null;

                // Find the two most distant end faces
                double maxDist = 0;
                XYZ bestPt1 = null, bestPt2 = null;

                for (int i = 0; i < endFaces.Count; i++)
                {
                    for (int j = i + 1; j < endFaces.Count; j++)
                    {
                        double dist = endFaces[i].Centroid.DistanceTo(endFaces[j].Centroid);
                        if (dist > maxDist)
                        {
                            maxDist = dist;
                            bestPt1 = endFaces[i].Centroid;
                            bestPt2 = endFaces[j].Centroid;
                        }
                    }
                }

                if (bestPt1 == null || bestPt2 == null) return null;

                double length = bestPt1.DistanceTo(bestPt2);

                // Determine start/end based on direction
                XYZ vec = bestPt2 - bestPt1;
                if (vec.DotProduct(axisDir) > 0)
                    return (length, bestPt1, bestPt2);
                else
                    return (length, bestPt2, bestPt1);
            }
            catch
            {
                return null;
            }
        }
    }
}
