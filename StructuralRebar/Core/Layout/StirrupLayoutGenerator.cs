using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    /// <summary>
    /// Generates stirrup/tie RebarDefinitions from HostGeometry.
    /// Dual-mode: BasisZ for horizontal beams, HAxis for slanted beams.
    /// </summary>
    public static class StirrupLayoutGenerator
    {
        /// <summary>
        /// Creates a stirrup definition for a beam.
        /// </summary>
        public static RebarDefinition CreateBeamStirrup(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double spacing,
            double startOffset,
            string hookStartName,
            string hookEndName,
            double zMin,
            double zMax)
        {
            double stW = host.Width - 2 * host.CoverOther;
            double stH = host.Height - host.CoverTop - host.CoverBottom;
            double hCenterOff = (host.CoverBottom - host.CoverTop) / 2.0;

            XYZ stirrupOrigin;
            List<Curve> curves;

            if (host.IsSlanted)
            {
                // === SLANTED BEAM: use LCS ===
                // Origin follows the slope at startPt + offset along true 3D axis
                stirrupOrigin = host.StartPoint + host.LAxis * startOffset;
                curves = CreateStirrupLoopLCS(stirrupOrigin, host.WAxis, host.HAxis, stW, stH, hCenterOff);
            }
            else
            {
                // === HORIZONTAL BEAM: use absolute Z from solid geometry ===
                XYZ xyOrigin = host.StartPoint + host.LAxis * startOffset;
                stirrupOrigin = new XYZ(xyOrigin.X, xyOrigin.Y, (zMax + zMin) / 2.0);
                curves = CreateStirrupLoopFlat(stirrupOrigin, host.WAxis, stW, stH, hCenterOff);
            }

            double arrayLen = host.Length - 2 * startOffset;

            return new RebarDefinition
            {
                Curves = curves,
                Style = Autodesk.Revit.DB.Structure.RebarStyle.StirrupTie,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = spacing,
                ArrayLength = arrayLen > 0 ? arrayLen : 0,
                ArrayDirection = host.LAxis,
                Normal = host.LAxis,
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                Label = "Stirrup"
            };
        }

        /// <summary>
        /// Flat stirrup loop using BasisZ (for horizontal beams).
        /// </summary>
        public static List<Curve> CreateStirrupLoopFlat(
            XYZ origin, XYZ widthDir,
            double w, double h, double zCenterOff = 0)
        {
            XYZ p1 = origin - widthDir * (w / 2.0) + XYZ.BasisZ * (-h / 2.0 + zCenterOff);
            XYZ p2 = origin + widthDir * (w / 2.0) + XYZ.BasisZ * (-h / 2.0 + zCenterOff);
            XYZ p3 = origin + widthDir * (w / 2.0) + XYZ.BasisZ * (h / 2.0 + zCenterOff);
            XYZ p4 = origin - widthDir * (w / 2.0) + XYZ.BasisZ * (h / 2.0 + zCenterOff);

            return new List<Curve>
            {
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3)
            };
        }

        /// <summary>
        /// LCS stirrup loop using HAxis (for slanted beams).
        /// Perpendicular to the beam slope.
        /// </summary>
        public static List<Curve> CreateStirrupLoopLCS(
            XYZ origin, XYZ wAxis, XYZ hAxis,
            double w, double h, double hCenterOff = 0)
        {
            XYZ p1 = origin - wAxis * (w / 2.0) + hAxis * (-h / 2.0 + hCenterOff);
            XYZ p2 = origin + wAxis * (w / 2.0) + hAxis * (-h / 2.0 + hCenterOff);
            XYZ p3 = origin + wAxis * (w / 2.0) + hAxis * (h / 2.0 + hCenterOff);
            XYZ p4 = origin - wAxis * (w / 2.0) + hAxis * (h / 2.0 + hCenterOff);

            return new List<Curve>
            {
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3)
            };
        }

        /// <summary>
        /// Creates multiple stirrup definitions, one per spacing zone.
        /// Each zone has its own spacing and array length, allowing 
        /// end-zone densification for beams and columns.
        /// </summary>
        public static List<RebarDefinition> CreateZonedBeamStirrups(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            List<SpacingZone> zones,
            string hookStartName,
            string hookEndName,
            double zMin,
            double zMax)
        {
            var defs = new List<RebarDefinition>();

            double stW = host.Width - 2 * host.CoverOther;
            double stH = host.Height - host.CoverTop - host.CoverBottom;
            double hCenterOff = (host.CoverBottom - host.CoverTop) / 2.0;

            foreach (var zone in zones)
            {
                double arrLen = zone.EndOffset - zone.StartOffset;
                if (arrLen <= 0 || zone.Spacing <= 0) continue;

                XYZ stirrupOrigin;
                List<Curve> curves;

                if (host.IsSlanted)
                {
                    stirrupOrigin = host.StartPoint + host.LAxis * zone.StartOffset;
                    curves = CreateStirrupLoopLCS(stirrupOrigin, host.WAxis, host.HAxis, stW, stH, hCenterOff);
                }
                else
                {
                    XYZ xyOrigin = host.StartPoint + host.LAxis * zone.StartOffset;
                    stirrupOrigin = new XYZ(xyOrigin.X, xyOrigin.Y, (zMax + zMin) / 2.0);
                    curves = CreateStirrupLoopFlat(stirrupOrigin, host.WAxis, stW, stH, hCenterOff);
                }

                defs.Add(new RebarDefinition
                {
                    Curves = curves,
                    Style = Autodesk.Revit.DB.Structure.RebarStyle.StirrupTie,
                    BarTypeName = barTypeName,
                    BarDiameter = barDiameter,
                    Spacing = zone.Spacing,
                    ArrayLength = arrLen,
                    ArrayDirection = host.LAxis,
                    Normal = host.LAxis,
                    HookStartName = hookStartName,
                    HookEndName = hookEndName,
                    Label = $"Stirrup ({zone.Label})"
                });
            }

            return defs;
        }
    }
}
