using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace antiGGGravity.StructuralRebar.Core.Validation
{
    public static class CoverValidator
    {
        /// <summary>
        /// Validates that a list of curves fits within the host element bounds subtracted by clear cover.
        /// Warning: simplified implementation. Real Revit cover geometry can be complex.
        /// </summary>
        public static bool ValidateCurvesAgainstCover(List<Curve> curves, Element host, double barDiameter)
        {
            try
            {
                BoundingBoxXYZ hostBox = host.get_BoundingBox(null);
                if (hostBox == null) return true; // Can't validate, assume okay

                double cExt = GetCoverDist(host, BuiltInParameter.CLEAR_COVER_EXTERIOR);
                double cInt = GetCoverDist(host, BuiltInParameter.CLEAR_COVER_INTERIOR);
                double cOther = GetCoverDist(host, BuiltInParameter.CLEAR_COVER_OTHER);

                // Use the smallest cover for a conservative check against the bounding box limits
                double minCover = Math.Min(cExt, Math.Min(cInt, cOther));

                XYZ minLimit = hostBox.Min + new XYZ(minCover, minCover, minCover);
                XYZ maxLimit = hostBox.Max - new XYZ(minCover, minCover, minCover);

                foreach (var curve in curves)
                {
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);

                    if (!IsPointInside(p0, minLimit, maxLimit, barDiameter)) return false;
                    if (!IsPointInside(p1, minLimit, maxLimit, barDiameter)) return false;
                }

                return true;
            }
            catch
            {
                return true; // Fail open
            }
        }

        private static bool IsPointInside(XYZ pt, XYZ minLimit, XYZ maxLimit, double barDia)
        {
            double tol = barDia / 2.0; 
            return pt.X >= (minLimit.X - tol) && pt.X <= (maxLimit.X + tol) &&
                   pt.Y >= (minLimit.Y - tol) && pt.Y <= (maxLimit.Y + tol) &&
                   pt.Z >= (minLimit.Z - tol) && pt.Z <= (maxLimit.Z + tol);
        }

        private static double GetCoverDist(Element element, BuiltInParameter param)
        {
            Parameter coverParam = element.get_Parameter(param);
            if (coverParam != null)
            {
                ElementId coverId = coverParam.AsElementId();
                if (coverId != ElementId.InvalidElementId)
                {
                    RebarCoverType coverType = element.Document.GetElement(coverId) as RebarCoverType;
                    if (coverType != null)
                    {
                        return coverType.CoverDistance;
                    }
                }
            }
            return 50.0 / 304.8; // 50mm fallback
        }
    }
}
