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
    public static class BoredPileLayoutGenerator
    {
        public static List<RebarDefinition> Generate(Document doc, HostGeometry host, RebarRequest request, double? overrideZEnd = null)
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

            // Spiral/hoop centerline radius (used by transverse reinforcement)
            double transverseRadius = hostRadius - host.CoverExterior;
            if (transverseRadius <= 0) transverseRadius = hostRadius * 0.8;

            // Resolve diameters to calculate correct inset
            double transverseBarDia = 0;
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                var tBarType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));
                if (tBarType != null) transverseBarDia = tBarType.BarModelDiameter;
                // Fallback if BarModelDiameter is 0
                if (transverseBarDia <= 0 && tBarType != null) {
                    var p = tBarType.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                    if (p != null) transverseBarDia = p.AsDouble();
                }
            }

            double verticalBarDia = 0;
            if (!string.IsNullOrEmpty(request.VerticalBarTypeName))
            {
                var vBarType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(t => t.Name.Equals(request.VerticalBarTypeName, StringComparison.OrdinalIgnoreCase));
                if (vBarType != null) verticalBarDia = vBarType.BarModelDiameter;
                // Fallback if BarModelDiameter is 0
                if (verticalBarDia <= 0 && vBarType != null) {
                    var p = vBarType.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                    if (p != null) verticalBarDia = p.AsDouble();
                }
            }

            // Calculate correct inset so vertical bar is inside spiral/hoop
            // Center-to-center distance = (TransverseDia / 2) + (VerticalDia / 2) + comfort gap
            double comfortGap = UnitConversion.MmToFeet(2); // 2mm gap
            double inset = (transverseBarDia / 2.0) + (verticalBarDia / 2.0) + comfortGap;
            
            double rebarRadius = transverseRadius - inset;
            if (rebarRadius <= 0) rebarRadius = transverseRadius * 0.7; // Safety fallback
            XYZ center = host.Origin;
            
            // 2. Main Longitudinal Bars
            double zStart = host.SolidZMin + host.CoverBottom;
            double zEnd = host.SolidZMax - host.CoverTop;
            
            if (overrideZEnd.HasValue)
            {
                zEnd = overrideZEnd.Value;
            }
            else
            {
                // Comply with host cover strictly
                zEnd = host.SolidZMax - host.CoverTop;
            }

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
                        Comment = "Pile Main Bar",
                        Normal = tangentNormal,
                        // Hook Bottom (start = bottom of vertical bar)
                        HookStartName = !string.IsNullOrEmpty(request.TransverseHookEndName) ? request.TransverseHookEndName : null,
                        // Hook Top (end = top of vertical bar) 
                        HookEndName = !string.IsNullOrEmpty(request.TransverseHookStartName) ? request.TransverseHookStartName : null,
                    });
                }
            }

            // Transverse Reinforcement for Bored Piles is handled directly in RebarEngine.cs
            // via the custom CreateCircularTie and CreateSpiralFromRing methods.
            
            return definitions;
        }
    }
}
