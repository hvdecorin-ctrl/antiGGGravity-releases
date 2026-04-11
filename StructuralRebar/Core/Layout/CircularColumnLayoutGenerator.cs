using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.Core.Calculators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class CircularColumnLayoutGenerator
    {
        public static List<RebarDefinition> Generate(
            Document doc, 
            HostGeometry host, 
            RebarRequest request, 
            double topExt = 0, 
            double botExt = 0,
            bool crankUpper = false,
            bool crankLower = false)
        {
            var definitions = new List<RebarDefinition>();

            // 1. Determine Radius and Geometry
            double hostRadius = GetHostRadius(host);
            if (hostRadius <= 0) return definitions;

            double transverseBarDia = GetBarTypeDiameter(doc, request.TransverseBarTypeName);
            double verticalBarDia = GetBarTypeDiameter(doc, request.VerticalBarTypeName);

            // Inset calculation (strictly respect cover and bar diameters)
            double comfortGap = UnitConversion.MmToFeet(1); // Reduced comfort gap to maximize space
            double transverseRadius = hostRadius - host.CoverExterior - (transverseBarDia / 2.0);
            double rebarRadius = transverseRadius - (transverseBarDia / 2.0) - (verticalBarDia / 2.0) - comfortGap;
            
            if (rebarRadius <= 0) rebarRadius = hostRadius * 0.7;

            XYZ center = host.Origin;
            double zColumnBase = host.SolidZMin;
            double zColumnTop = host.SolidZMax;

            // Longitudinal bar vertical range
            double zStart = zColumnBase + host.CoverBottom - botExt;
            double zEnd = zColumnTop - host.CoverTop + topExt;
            
            // Adjust for safety gap (standard 70mm from top if no extension)
            if (topExt <= 0) zEnd -= UnitConversion.MmToFeet(70);

            int count = request.PileBarCount;
            if (count < 1) return definitions;

            double crankOff = LapSpliceCalculator.GetCrankOffset(verticalBarDia);
            double crankRun = LapSpliceCalculator.GetCrankRun(verticalBarDia);

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                XYZ dirRadial = host.LAxis * Math.Cos(angle) + host.WAxis * Math.Sin(angle);
                
                XYZ pStart = center + dirRadial * rebarRadius + XYZ.BasisZ * zStart;
                XYZ pEnd = center + dirRadial * rebarRadius + XYZ.BasisZ * zEnd;

                List<Curve> curves = new List<Curve>();

                if (crankUpper && botExt > 0)
                {
                    // Crank at bottom of upper column bar (radial shift inward)
                    // ptA: start at offset (closer to center)
                    XYZ ptA = pStart - dirRadial * crankOff;
                    // ptB: end of straight lap area
                    XYZ ptB = ptA + XYZ.BasisZ * crankRun * 4; // Use 4x run as straight overlap
                    // ptC: back at main rebarRadius
                    XYZ ptC = pStart + XYZ.BasisZ * (crankRun * 5);

                    curves.Add(Line.CreateBound(ptA, ptB));
                    curves.Add(Line.CreateBound(ptB, ptC));
                    curves.Add(Line.CreateBound(ptC, pEnd));
                }
                else if (crankLower && topExt > 0)
                {
                    // Crank at top of lower column bar (radial shift inward)
                    XYZ spliceStart = pEnd - XYZ.BasisZ * topExt;
                    XYZ ptA = spliceStart - XYZ.BasisZ * crankRun;
                    XYZ ptB = spliceStart - dirRadial * crankOff;
                    XYZ ptC = pEnd - dirRadial * crankOff;

                    curves.Add(Line.CreateBound(pStart, ptA));
                    curves.Add(Line.CreateBound(ptA, ptB));
                    curves.Add(Line.CreateBound(ptB, ptC));
                }
                else
                {
                    curves.Add(Line.CreateBound(pStart, pEnd));
                }

                XYZ vecToCenter = -dirRadial;
                XYZ tangentNormal = XYZ.BasisZ.CrossProduct(vecToCenter).Normalize();

                definitions.Add(new RebarDefinition
                {
                    Curves = curves,
                    BarTypeName = request.VerticalBarTypeName,
                    Style = RebarStyle.Standard,
                    Label = "Main Bar (Circular)",
                    Comment = "Main Bar",
                    Normal = tangentNormal,
                    BarDiameter = verticalBarDia
                });
            }

            return definitions;
        }

        public static List<RebarDefinition> GenerateStarters(Document doc, HostGeometry host, RebarRequest request, double starterLen, double topExt)
        {
            var definitions = new List<RebarDefinition>();
            
            double hostRadius = GetHostRadius(host);
            if (hostRadius <= 0) return definitions;

            string bTypeName = request.StarterBarTypeName ?? request.VerticalBarTypeName;
            double bDia = GetBarTypeDiameter(doc, bTypeName);
            double tDia = GetBarTypeDiameter(doc, request.TransverseBarTypeName);

            double comfortGap = UnitConversion.MmToFeet(1);
            double rebarRadius = hostRadius - host.CoverExterior - tDia - (bDia / 2.0) - comfortGap;
            if (rebarRadius <= 0) rebarRadius = hostRadius * 0.7;

            XYZ center = host.Origin;
            double zBase = host.SolidZMin;

            double zStart = zBase - starterLen;
            double zEnd = zBase + topExt;

            int count = request.PileBarCount;
            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                XYZ dirRadial = host.LAxis * Math.Cos(angle) + host.WAxis * Math.Sin(angle);
                XYZ pStart = center + dirRadial * rebarRadius + XYZ.BasisZ * zStart;
                XYZ pEnd = center + dirRadial * rebarRadius + XYZ.BasisZ * zEnd;

                XYZ vecToCenter = -dirRadial;
                XYZ tangentNormal = XYZ.BasisZ.CrossProduct(vecToCenter).Normalize();

                definitions.Add(new RebarDefinition
                {
                    Curves = new List<Curve> { Line.CreateBound(pStart, pEnd) },
                    BarTypeName = bTypeName,
                    Style = RebarStyle.Standard,
                    Label = "Starter Bar (Circular)",
                    Comment = "Starter Bar",
                    Normal = tangentNormal,
                    BarDiameter = bDia,
                    HookStartName = request.StarterHookEndName,
                    HookStartOrientation = RebarHookOrientation.Left
                });
            }

            return definitions;
        }

        private static double GetHostRadius(HostGeometry host)
        {
            if (host.BoundaryCurves.Count > 0 && host.BoundaryCurves.Any(c => c is Arc))
            {
                var arcs = host.BoundaryCurves.OfType<Arc>().ToList();
                return arcs.Max(a => a.Radius);
            }
            return Math.Min(host.Width, host.Height) / 2.0;
        }

        private static double GetBarTypeDiameter(Document doc, string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;
            var barType = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(t => t.Name.Equals(barTypeName, StringComparison.OrdinalIgnoreCase));
            return barType?.BarModelDiameter ?? 0;
        }
    }
}
