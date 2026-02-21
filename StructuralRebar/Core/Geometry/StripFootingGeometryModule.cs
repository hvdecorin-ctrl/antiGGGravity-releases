using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Extracts geometry from Structural Foundation (Strip/Wall Footing) elements.
    /// Supports WallFoundations and Isolated foundations used as strip footings.
    /// Flattened LCS: Length follows horizontal projection, Width is perpendicular, Height is Z.
    /// </summary>
    public static class StripFootingGeometryModule
    {
        public static HostGeometry? Read(Document doc, Element foundation)
        {
            BoundingBoxXYZ bbox = foundation.get_BoundingBox(null);
            if (bbox == null) return null;

            XYZ center = (bbox.Min + bbox.Max) / 2.0;
            
            XYZ lAxis = null;
            XYZ wAxis = null;
            double length = 0;
            XYZ startPt = null;
            XYZ endPt = null;
            GeometrySource source = GeometrySource.BoundingBox;

            // 1. Path Determination
            Curve pathCurve = null;
            if (foundation.Location is LocationCurve locCurve)
            {
                pathCurve = locCurve.Curve;
            }
            else if (foundation is WallFoundation wf && wf.WallId != ElementId.InvalidElementId)
            {
                var wall = doc.GetElement(wf.WallId) as Wall;
                if (wall?.Location is LocationCurve wallLocCurve)
                    pathCurve = wallLocCurve.Curve;
            }

            if (pathCurve != null)
            {
                XYZ curveStart = pathCurve.GetEndPoint(0);
                XYZ curveEnd = pathCurve.GetEndPoint(1);
                lAxis = (curveEnd - curveStart).Normalize();
                
                // Flatten to horizontal
                if (Math.Abs(lAxis.Z) < 0.999)
                    lAxis = new XYZ(lAxis.X, lAxis.Y, 0).Normalize();
                
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(foundation, lAxis);
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
            else if (foundation is FamilyInstance fi)
            {
                Transform trans = fi.GetTransform();
                lAxis = trans.BasisX.Normalize();
                lAxis = new XYZ(lAxis.X, lAxis.Y, 0).Normalize();
                
                var geomResult = GeometryUtils.GetGeometryLengthAndEndpoints(foundation, lAxis);
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
                    wAxis = trans.BasisY.Normalize();
                    double lGuess = Math.Abs(vBbox.DotProduct(lAxis));
                    double wGuess = Math.Abs(vBbox.DotProduct(wAxis));
                    if (wGuess > lGuess)
                    {
                        lAxis = wAxis;
                        lGuess = wGuess;
                    }
                    length = lGuess;
                    startPt = center - lAxis * (length / 2.0);
                    endPt = center + lAxis * (length / 2.0);
                    source = GeometrySource.BoundingBox;
                }
            }
            else
            {
                // Fallback BBox
                double dx = bbox.Max.X - bbox.Min.X;
                double dy = bbox.Max.Y - bbox.Min.Y;
                if (dx > dy)
                {
                    lAxis = XYZ.BasisX;
                    length = dx;
                }
                else
                {
                    lAxis = XYZ.BasisY;
                    length = dy;
                }
                startPt = center - lAxis * (length / 2.0);
                endPt = center + lAxis * (length / 2.0);
            }

            // 2. Cross Axis and Origin
            wAxis = XYZ.BasisZ.CrossProduct(lAxis).Normalize();
            XYZ hAxis = XYZ.BasisZ;

            // 3. Dimensions
            double width = GetWidth(foundation, wAxis, bbox);
            double thickness = bbox.Max.Z - bbox.Min.Z;

            // 4. Covers
            double cTop = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_TOP);
            double cBot = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double cSide = GeometryUtils.GetCoverDistance(doc, foundation, BuiltInParameter.CLEAR_COVER_OTHER);

            return new HostGeometry(
                lAxis: lAxis,
                wAxis: wAxis,
                hAxis: hAxis,
                slopeAngle: 0,
                origin: center,
                startPoint: startPt,
                endPoint: endPt,
                length: length,
                width: width,
                height: thickness,
                coverTop: cTop,
                coverBottom: cBot,
                coverExterior: cSide,
                coverInterior: cSide,
                coverOther: cSide,
                normal: wAxis,
                thickness: width,
                source: source,
                solidZMin: bbox.Min.Z,
                solidZMax: bbox.Max.Z
            );
        }

        private static double GetWidth(Element foundation, XYZ wAxis, BoundingBoxXYZ bbox)
        {
            Parameter p = foundation.LookupParameter("Width");
            if (p != null && p.HasValue) return p.AsDouble();

            XYZ size = bbox.Max - bbox.Min;
            if (Math.Abs(wAxis.X) > 0.9) return size.X;
            if (Math.Abs(wAxis.Y) > 0.9) return size.Y;
            return Math.Min(size.X, size.Y);
        }
    }
}
