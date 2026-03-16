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
            XYZ p1 = tieOrigin - basisX * (wTie / 2.0) - basisY * (dTie / 2.0); // BL
            XYZ p2 = tieOrigin + basisX * (wTie / 2.0) - basisY * (dTie / 2.0); // BR
            XYZ p3 = tieOrigin + basisX * (wTie / 2.0) + basisY * (dTie / 2.0); // TR
            XYZ p4 = tieOrigin - basisX * (wTie / 2.0) + basisY * (dTie / 2.0); // TL

            // Use Counter-Clockwise (CCW) order starting from TR: TR -> TL -> BL -> BR -> TR
            // This sequence matches beam stirrups and ensures standard hook shape matching succeeds.
            List<Curve> curves = new List<Curve>
            {
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3)
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
                Label = "Column Tie",
                Comment = "Stirrup"
            };
        }

        /// <summary>
        /// Creates zone-based column ties for confinement regions.
        /// </summary>
        public static List<RebarDefinition> CreateZonedColumnTies(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double startOffset,
            List<SpacingZone> zones,
            string hookStartName,
            string hookEndName)
        {
            var defs = new List<RebarDefinition>();

            XYZ basisX = host.LAxis;
            XYZ basisY = host.WAxis;
            XYZ basisZ = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double depth = host.Height;
            double cover = host.CoverExterior;

            double wTie = width - 2 * cover;
            double dTie = depth - 2 * cover;

            foreach (var zone in zones)
            {
                double arrLen = zone.EndOffset - zone.StartOffset;
                if (arrLen <= 0 || zone.Spacing <= 0) continue;

                XYZ tieOrigin = origin + basisZ * (startOffset + zone.StartOffset);

                XYZ p1 = tieOrigin - basisX * (wTie / 2.0) - basisY * (dTie / 2.0); // BL
                XYZ p2 = tieOrigin + basisX * (wTie / 2.0) - basisY * (dTie / 2.0); // BR
                XYZ p3 = tieOrigin + basisX * (wTie / 2.0) + basisY * (dTie / 2.0); // TR
                XYZ p4 = tieOrigin - basisX * (wTie / 2.0) + basisY * (dTie / 2.0); // TL

                // Counter-Clockwise (CCW) order starting from TR: TR -> TL -> BL -> BR -> TR
                var curves = new List<Curve>
                {
                    Line.CreateBound(p3, p4),
                    Line.CreateBound(p4, p1),
                    Line.CreateBound(p1, p2),
                    Line.CreateBound(p2, p3)
                };

                defs.Add(new RebarDefinition
                {
                    Curves = curves,
                    Style = RebarStyle.StirrupTie,
                    BarTypeName = barTypeName,
                    BarDiameter = barDiameter,
                    Spacing = zone.Spacing,
                    ArrayLength = arrLen,
                    Normal = basisZ,
                    HookStartName = hookStartName,
                    HookEndName = hookEndName,
                    HookStartOrientation = RebarHookOrientation.Left,
                    HookEndOrientation = RebarHookOrientation.Left,
                    Label = $"Column Tie ({zone.Label})",
                    Comment = "Stirrup"
                });
            }

            return defs;
        }

        public static List<RebarDefinition> CreateColumnVerticals(
            HostGeometry host,
            string barTypeNameX,
            double barDiameterX,
            string barTypeNameY,
            double barDiameterY,
            double tDia,
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

            // Inner offset for vertical bars (Account for largest bar diameter and tie)
            double maxBarDia = Math.Max(barDiameterX, barDiameterY);
            double innerOff = coverSide + tDia + maxBarDia / 2.0;

            double stepX = nx > 1 ? (width - 2 * innerOff) / (nx - 1) : 0;
            double stepY = ny > 1 ? (depth - 2 * innerOff) / (ny - 1) : 0;

            double xFirst = -width / 2.0 + innerOff;
            double xLast = width / 2.0 - innerOff;
            double yFirst = -depth / 2.0 + innerOff;
            double yLast = depth / 2.0 - innerOff;

            // Helper to create a Set definition
            void AddSet(int count, XYZ startPos, XYZ arrayDir, double distWidth, string bType, double bDia, XYZ outDir, string label)
            {
                if (count < 1) return;

                XYZ hookNormal = basisZ.CrossProduct(outDir);
                XYZ pStart = startPos + basisZ * (coverSide - botExt);
                XYZ pEnd = startPos + basisZ * (totalHeight - coverSide + topExt);

                definitions.Add(new RebarDefinition
                {
                    Curves = new List<Curve> { Line.CreateBound(pStart, pEnd) },
                    Style = RebarStyle.Standard,
                    BarTypeName = bType,
                    BarDiameter = bDia,
                    Normal = hookNormal,
                    ArrayDirection = arrayDir,
                    FixedCount = count,
                    DistributionWidth = distWidth,
                    HookStartName = hookStartName,
                    HookEndName = hookEndName,
                    HookStartOrientation = hookStartOut ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                    HookEndOrientation = hookEndOut ? RebarHookOrientation.Left : RebarHookOrientation.Right,
                    Label = label,
                    Comment = "Main Bar"
                });
            }

            // 1. Bottom Face Set (y = yFirst)
            if (nx > 0)
            {
                XYZ pos = origin + basisX * xFirst + basisY * yFirst;
                double dist = nx > 1 ? xLast - xFirst : 0;
                AddSet(nx, pos, basisX, dist, barTypeNameX, barDiameterX, -basisY, "Main Vertical Bar (Bottom Face)");
            }

            // 2. Top Face Set (y = yLast)
            if (nx > 0 && ny > 1)
            {
                XYZ pos = origin + basisX * xLast + basisY * yLast;
                double dist = nx > 1 ? xLast - xFirst : 0;
                AddSet(nx, pos, -basisX, dist, barTypeNameX, barDiameterX, basisY, "Main Vertical Bar (Top Face)");
            }

            // 3. Left Face Inner Set (x = xFirst, inner ny-2 bars)
            int nyInner = ny - 2;
            if (nyInner > 0 && nx > 0)
            {
                XYZ pos = origin + basisX * xFirst + basisY * (yLast - stepY);
                double dist = nyInner > 1 ? stepY * (nyInner - 1) : 0;
                AddSet(nyInner, pos, -basisY, dist, barTypeNameY, barDiameterY, -basisX, "Main Vertical Bar (Left Face)");
            }

            // 4. Right Face Inner Set (x = xLast, inner ny-2 bars)
            if (nyInner > 0 && nx > 1)
            {
                XYZ pos = origin + basisX * xLast + basisY * (yFirst + stepY);
                double dist = nyInner > 1 ? stepY * (nyInner - 1) : 0;
                AddSet(nyInner, pos, basisY, dist, barTypeNameY, barDiameterY, basisX, "Main Vertical Bar (Right Face)");
            }

            return definitions;
        }
    }
}
