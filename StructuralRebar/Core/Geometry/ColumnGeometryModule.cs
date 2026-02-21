using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using System;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Extracts geometry from Column elements (Structural Columns).
    /// Uses Transform for LCS and BoundingBox for vertical bounds.
    /// </summary>
    public static class ColumnGeometryModule
    {
        public static HostGeometry? Read(Document doc, FamilyInstance column)
        {
            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            if (bbox == null) return null;

            Transform trans = column.GetTransform();
            XYZ basisX = trans.BasisX.Normalize();
            XYZ basisY = trans.BasisY.Normalize();
            XYZ basisZ = trans.BasisZ.Normalize();

            // Dimensions
            double width = GetParameter(column, "Width", "b") ?? (bbox.Max.X - bbox.Min.X);
            double depth = GetParameter(column, "Depth", "h") ?? (bbox.Max.Y - bbox.Min.Y);

            // Origin at the bottom center of the column
            XYZ colOrigin = trans.Origin;
            XYZ bottomOrigin;
            if (Math.Abs(basisZ.Z) > 0.001)
            {
                double distToBottom = colOrigin.Z - bbox.Min.Z;
                bottomOrigin = colOrigin - basisZ * (distToBottom / basisZ.Z);
            }
            else
            {
                bottomOrigin = colOrigin;
            }

            // Covers
            double coverSide = GeometryUtils.GetCoverDistance(doc, column, BuiltInParameter.CLEAR_COVER_OTHER);
            double coverTop = GeometryUtils.GetCoverDistance(doc, column, BuiltInParameter.CLEAR_COVER_TOP);
            double coverBot = GeometryUtils.GetCoverDistance(doc, column, BuiltInParameter.CLEAR_COVER_BOTTOM);

            return new HostGeometry(
                lAxis: basisX, // Column 'Width' direction
                wAxis: basisY, // Column 'Depth' direction
                hAxis: basisZ, // Column vertical direction
                slopeAngle: 0, // Usually 0 for columns
                origin: bottomOrigin,
                startPoint: bottomOrigin, // For columns, origin is the bottom start
                endPoint: bottomOrigin + basisZ * (bbox.Max.Z - bbox.Min.Z),
                length: bbox.Max.Z - bbox.Min.Z,
                width: width,
                height: depth,
                coverTop: coverTop,
                coverBottom: coverBot,
                coverExterior: coverSide, // Use side cover
                coverInterior: coverSide,
                coverOther: coverSide,
                normal: basisY,
                thickness: depth,
                source: GeometrySource.Transform,
                solidZMin: bbox.Min.Z,
                solidZMax: bbox.Max.Z
            );
        }

        private static double? GetParameter(Element e, params string[] names)
        {
            foreach (var name in names)
            {
                Parameter p = e.LookupParameter(name) ?? (e as FamilyInstance)?.Symbol?.LookupParameter(name);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            return null;
        }
    }
}
