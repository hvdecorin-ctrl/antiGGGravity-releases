using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class CircularColumnLayoutGenerator
    {
        public static List<RebarDefinition> Generate(HostGeometry host, RebarRequest request)
        {
            var definitions = new List<RebarDefinition>();

            // 1. Determine Radius
            double hostRadius = 0;
            if (host.BoundaryCurves.Count > 0 && host.BoundaryCurves.Any(c => c is Arc))
            {
                var arcs = host.BoundaryCurves.OfType<Arc>().ToList();
                hostRadius = arcs.Max(a => a.Radius);
            }
            else
            {
                // Fallback to minimal dimension for rectangular or unknown piles
                hostRadius = Math.Min(host.Width, host.Length) / 2.0;
            }

            if (hostRadius <= 0) return definitions; // Cannot generate in zero-size host

            double rebarRadius = hostRadius - host.CoverExterior;
            if (rebarRadius <= 0) rebarRadius = hostRadius * 0.8; // Safety fallback
            XYZ center = host.Origin;
            
            // 2. Main Longitudinal Bars
            double zStart = host.SolidZMin + host.CoverBottom;
            double zEnd = host.SolidZMax - host.CoverTop;
            
            // Safety gap for main bars (70mm)
            double safetyGap = UnitConversion.MmToFeet(70);
            zEnd -= safetyGap;

            int count = request.PileBarCount;
            if (zEnd - zStart > UnitConversion.MmToFeet(100))
            {
                for (int i = 0; i < count; i++)
                {
                    double angle = (2 * Math.PI / count) * i;
                    double dx = rebarRadius * Math.Cos(angle);
                    double dy = rebarRadius * Math.Sin(angle);
                    XYZ p0 = new XYZ(center.X + dx, center.Y + dy, zStart);
                    XYZ p1 = new XYZ(p0.X, p0.Y, zEnd);

                    // Normal must be perpendicular to the bar. Tangent to the circle forms radial plane for hooks.
                    XYZ vecToCenter = new XYZ(-dx, -dy, 0).Normalize();
                    XYZ tangentNormal = XYZ.BasisZ.CrossProduct(vecToCenter).Normalize();

                    definitions.Add(new RebarDefinition
                    {
                        Curves = new List<Curve> { Line.CreateBound(p0, p1) },
                        BarTypeName = request.VerticalBarTypeName,
                        Style = RebarStyle.Standard,
                        Label = "Main Bar",
                        Comment = "Main Bar",
                        Normal = tangentNormal
                    });
                }
            }

            // Transverse Reinforcement for Circular Columns is handled directly in RebarEngine.cs
            // via the custom CreateCircularTie and CreateSpiralFromRing methods.
            
            return definitions;
        }
    }
}
