using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Extracts geometry from Isolated Structural Foundation elements.
    /// Uses LCS from Transform or Bounding Box.
    /// </summary>
    public static class FootingPadGeometryModule
    {
        public static HostGeometry? Read(Document doc, Element foundation)
        {
            BoundingBoxXYZ bbox = foundation.get_BoundingBox(null);
            if (bbox == null) return null;

            XYZ center = (bbox.Min + bbox.Max) / 2.0;
            
            XYZ lAxis, wAxis, hAxis;
            double length, width, height;
            GeometrySource source;

            if (foundation is FamilyInstance fi)
            {
                Transform trans = fi.GetTransform();
                lAxis = trans.BasisX.Normalize();
                wAxis = trans.BasisY.Normalize();
                hAxis = trans.BasisZ.Normalize();
                source = GeometrySource.Transform;
            }
            else
            {
                lAxis = XYZ.BasisX;
                wAxis = XYZ.BasisY;
                hAxis = XYZ.BasisZ;
                source = GeometrySource.BoundingBox;
            }

            XYZ size = bbox.Max - bbox.Min;
            length = Math.Abs(size.DotProduct(lAxis));
            width = Math.Abs(size.DotProduct(wAxis));
            height = Math.Abs(size.DotProduct(hAxis));

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
                source: source,
                solidZMin: bbox.Min.Z,
                solidZMax: bbox.Max.Z
            );
        }
    }
}
