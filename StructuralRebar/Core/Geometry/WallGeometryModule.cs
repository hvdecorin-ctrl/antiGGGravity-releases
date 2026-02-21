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
    /// Extracts geometry from Wall elements.
    /// Priority: LocationCurve + Orientation.
    /// </summary>
    public static class WallGeometryModule
    {
        public static HostGeometry Read(Document doc, Wall wall)
        {
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            XYZ center = (bbox.Max + bbox.Min) / 2.0;

            // --- AXIS DETERMINATION ---
            XYZ lAxis, wAxis, hAxis;
            double length;
            XYZ startPt, endPt;
            GeometrySource source;

            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve != null && locCurve.Curve is Line wallLine)
            {
                startPt = wallLine.GetEndPoint(0);
                endPt = wallLine.GetEndPoint(1);
                lAxis = (endPt - startPt).Normalize();
                length = wallLine.Length;
                source = GeometrySource.LocationCurve;
            }
            else
            {
                // Fallback to orientation/bbox if needed (curved walls not supported yet for simplicity)
                lAxis = XYZ.BasisX;
                length = bbox.Max.X - bbox.Min.X;
                startPt = center - lAxis * (length / 2.0);
                endPt = center + lAxis * (length / 2.0);
                source = GeometrySource.BoundingBox;
            }

            // Normal axis (Width direction)
            wAxis = wall.Orientation.Normalize(); // Exterior direction
            hAxis = XYZ.BasisZ; // Walls are usually vertical

            double thickness = wall.Width;
            double height = bbox.Max.Z - bbox.Min.Z;

            // --- COVERS ---
            double coverExt = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double coverInt = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_INTERIOR);
            double coverOther = GeometryUtils.GetCoverDistance(doc, wall, BuiltInParameter.CLEAR_COVER_OTHER);

            return new HostGeometry(
                lAxis: lAxis,
                wAxis: wAxis,
                hAxis: hAxis,
                slopeAngle: 0,
                origin: center,
                startPoint: startPt,
                endPoint: endPt,
                length: length,
                width: thickness,
                height: height,
                coverTop: coverOther, // Using Other for Top/Bot as approximation
                coverBottom: coverOther,
                coverExterior: coverExt,
                coverInterior: coverInt,
                coverOther: coverOther,
                normal: wAxis,
                thickness: thickness,
                source: source,
                solidZMin: bbox.Min.Z,
                solidZMax: bbox.Max.Z
            );
        }
    }
}
