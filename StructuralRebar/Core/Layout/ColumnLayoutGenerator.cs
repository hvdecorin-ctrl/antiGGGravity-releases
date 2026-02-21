using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class ColumnLayoutGenerator
    {
        public static RebarDefinition CreateColumnTie(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double spacing,
            double startOffset,
            double endOffset,
            string hookStartName,
            string hookEndName)
        {
            double totalHeight = host.Length;
            double tieLen = totalHeight - startOffset - endOffset;
            if (tieLen <= 0 || spacing <= 0) return null;

            XYZ basisX = host.LAxis;
            XYZ basisY = host.WAxis;
            XYZ basisZ = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double depth = host.Height;
            double cover = host.CoverExterior; // Side cover

            double wTie = width - 2 * cover;
            double dTie = depth - 2 * cover;

            XYZ tieOrigin = origin + basisZ * startOffset;

            // Points in Local Basis
            XYZ p1 = tieOrigin - basisX * (wTie / 2.0) - basisY * (dTie / 2.0);
            XYZ p2 = tieOrigin + basisX * (wTie / 2.0) - basisY * (dTie / 2.0);
            XYZ p3 = tieOrigin + basisX * (wTie / 2.0) + basisY * (dTie / 2.0);
            XYZ p4 = tieOrigin - basisX * (wTie / 2.0) + basisY * (dTie / 2.0);

            List<Curve> curves = new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            return new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.StirrupTie,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = spacing,
                ArrayLength = tieLen,
                Normal = basisZ,
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                HookStartOrientation = RebarHookOrientation.Left,
                HookEndOrientation = RebarHookOrientation.Left,
                Label = "Column Tie"
            };
        }

        public static List<RebarDefinition> CreateColumnVerticals(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            int nx,
            int ny,
            double topExt,
            double botExt,
            string hookStartName,
            string hookEndName,
            bool hookStartOut,
            bool hookEndOut)
        {
            var definitions = new List<RebarDefinition>();
            if (nx < 1 && ny < 1) return definitions;

            XYZ basisX = host.LAxis;
            XYZ basisY = host.WAxis;
            XYZ basisZ = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double depth = host.Height;
            double coverSide = host.CoverExterior;
            double totalHeight = host.Length;

            // Inner offset for vertical bars (Accounting for tie diameter)
            // Hardcoded tDia approx if not known? Original code uses tDia.
            // Let's assume tDia = 10mm (0.0328 feet) as fallback or pass it.
            double tDia = 0.0328; 
            double innerOff = coverSide + tDia + barDiameter / 2.0;

            // X and Y grid points
            List<double> xPts = new List<double>();
            if (nx > 1)
            {
                double stepX = (width - 2 * innerOff) / (nx - 1);
                for (int i = 0; i < nx; i++) xPts.Add(-width / 2.0 + innerOff + i * stepX);
            }
            else xPts.Add(0);

            List<double> yPts = new List<double>();
            if (ny > 1)
            {
                double stepY = (depth - 2 * innerOff) / (ny - 1);
                for (int i = 0; i < ny; i++) yPts.Add(-depth / 2.0 + innerOff + i * stepY);
            }
            else yPts.Add(0);

            for (int ix = 0; ix < xPts.Count; ix++)
            {
                for (int iy = 0; iy < yPts.Count; iy++)
                {
                    bool isXEdge = (ix == 0 || ix == xPts.Count - 1);
                    bool isYEdge = (iy == 0 || iy == yPts.Count - 1);

                    if (isXEdge || isYEdge)
                    {
                        double x = xPts[ix];
                        double y = yPts[iy];

                        // Determine hook normal (outward direction)
                        XYZ outDir;
                        if (isXEdge && isYEdge)
                            outDir = (basisX * (x > 0 ? 1 : -1) + basisY * (y > 0 ? 1 : -1)).Normalize();
                        else if (isXEdge)
                            outDir = basisX * (x > 0 ? 1 : -1);
                        else
                            outDir = basisY * (y > 0 ? 1 : -1);

                        XYZ hookNormal = basisZ.CrossProduct(outDir);

                        XYZ pos = origin + basisX * x + basisY * y;
                        XYZ pStart = pos + basisZ * (coverSide - botExt);
                        XYZ pEnd = pos + basisZ * (totalHeight - coverSide + topExt);

                        Curve vLine = Line.CreateBound(pStart, pEnd);

                        definitions.Add(new RebarDefinition
                        {
                            Curves = new List<Curve> { vLine },
                            Style = RebarStyle.Standard,
                            BarTypeName = barTypeName,
                            BarDiameter = barDiameter,
                            Normal = hookNormal,
                            HookStartName = hookStartName,
                            HookEndName = hookEndName,
                            HookStartOrientation = hookStartOut ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                            HookEndOrientation = hookEndOut ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                            Label = "Main Vertical Bar"
                        });
                    }
                }
            }

            return definitions;
        }
    }
}
