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
    public static class PadShapeLayoutGenerator
    {
        public static List<RebarDefinition> CreateMat(HostGeometry host, RebarLayerConfig layer, bool isTop)
        {
            var definitions = new List<RebarDefinition>();
            if (host.BoundaryCurves == null || host.BoundaryCurves.Count == 0) return definitions;

            double spacing = layer.VerticalSpacing;
            double coverSide = host.CoverExterior;
            double coverBot = host.CoverBottom;
            double coverTop = host.CoverTop;
            double barDia = layer.BarDiameter_Backing;

            // Z-level for this mat
            double zPos = isTop ? (host.SolidZMax - coverTop - barDia / 2.0) : (host.SolidZMin + coverBot + barDia / 2.0);

            // We'll generate rebar in two directions: L and W.
            // Direction 1: Parallel to LAxis
            string c1 = isTop ? "Top Bar" : "Btm Bar";
            definitions.AddRange(GenerateDirectionalBars(host, host.LAxis, host.WAxis, zPos, spacing, coverSide, layer, "Dir 1", c1));

            // Direction 2: Parallel to WAxis (placed slightly offset in Z to avoid clashes)
            double zOffset = isTop ? -barDia : barDia;
            string c2 = isTop ? "Top T2" : "Btm B2";
            definitions.AddRange(GenerateDirectionalBars(host, host.WAxis, host.LAxis, zPos + zOffset, spacing, coverSide, layer, "Dir 2", c2));

            return definitions;
        }

        private static List<RebarDefinition> GenerateDirectionalBars(
            HostGeometry host, XYZ barDir, XYZ stepDir, double z, double spacing, double sideCover, RebarLayerConfig layer, string labelSuffix, string comment)
        {
            var results = new List<RebarDefinition>();
            
            // Get the boundary loop in 2D (projected to the Z plane of the bars)
            var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z));
            var projectedCurves = host.BoundaryCurves.Select(c => ClipToPlane(c, plane)).ToList();
            if (projectedCurves.Any(c => c == null)) return results;

            // Bounding box in local space to determine range
            double minStep = double.MaxValue;
            double maxStep = double.MinValue;
            foreach (var curve in projectedCurves)
            {
                foreach (XYZ pt in new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) })
                {
                    double val = pt.DotProduct(stepDir);
                    minStep = Math.Min(minStep, val);
                    maxStep = Math.Max(maxStep, val);
                }
            }

            double start = minStep + sideCover + (layer.BarDiameter_Backing / 2.0);
            double end = maxStep - sideCover - (layer.BarDiameter_Backing / 2.0);

            for (double s = start; s <= end + 0.001; s += spacing)
            {
                // Create a line that indefinitely spans the boundary in the barDir
                XYZ refPt = host.Origin + stepDir * (s - host.Origin.DotProduct(stepDir));
                refPt = new XYZ(refPt.X, refPt.Y, z);
                
                XYZ lineStart = refPt - barDir * 1000; // Giant line for intersection
                XYZ lineEnd = refPt + barDir * 1000;
                Line gridLine = Line.CreateBound(lineStart, lineEnd);

                // Intersect with boundary
                var segments = IntersectLineWithCurves(gridLine, projectedCurves);
                foreach (var seg in segments)
                {
                    // Shorten by side cover at each end of the segment
                    double segLen = seg.Length;
                    if (segLen <= 2 * sideCover) continue;

                    XYZ dir = (seg.GetEndPoint(1) - seg.GetEndPoint(0)).Normalize();
                    XYZ p0 = seg.GetEndPoint(0) + dir * sideCover;
                    XYZ p1 = seg.GetEndPoint(1) - dir * sideCover;

                    if (p0.DistanceTo(p1) < 0.01) continue;

                    // Support for LL Shape (U-Bars) - Similar to Footing Pad
                    XYZ legDir = XYZ.BasisZ; // Default for bottom
                    if (z > (host.SolidZMin + host.SolidZMax) / 2.0) legDir = -XYZ.BasisZ; // Top mat points down

                    double height = host.SolidZMax - host.SolidZMin;
                    double dia = layer.BarDiameter_Backing;
                    double safetyGapMain = UnitConversion.MmToFeet(70);
                    double legLen = layer.OverrideHookLength ? layer.HookLengthOverride : (height - host.CoverTop - host.CoverBottom - dia - safetyGapMain);
                    if (legLen < 0.2) legLen = 0.5;

                    XYZ pStartLeg = p0 + legDir * legLen;
                    XYZ pEndLeg = p1 + legDir * legLen;

                    var curves = new List<Curve>
                    {
                        Line.CreateBound(pStartLeg, p0),
                        Line.CreateBound(p0, p1),
                        Line.CreateBound(p1, pEndLeg)
                    };

                    results.Add(new RebarDefinition
                    {
                        Curves = curves,
                        Style = RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = layer.BarDiameter_Backing,
                        Normal = dir.CrossProduct(legDir).Normalize(), // Normal for the U-shape plane
                        Spacing = 0,
                        FixedCount = 1,
                        Label = $"Pad Mat {labelSuffix}",
                        HookStartName = layer.HookStartName,
                        HookEndName = layer.HookEndName,
                        OverrideHookLength = layer.OverrideHookLength,
                        HookLengthOverride = layer.HookLengthOverride,
                        ShapeNameHint = "Shape LL",
                        Comment = comment
                    });
                }
            }

            return results;
        }

        private static Curve ClipToPlane(Curve c, Plane p)
        {
            // Simplified projection: just set Z to plane's Z
            XYZ p0 = c.GetEndPoint(0);
            XYZ p1 = c.GetEndPoint(1);
            XYZ new0 = new XYZ(p0.X, p0.Y, p.Origin.Z);
            XYZ new1 = new XYZ(p1.X, p1.Y, p.Origin.Z);
            if (new0.IsAlmostEqualTo(new1)) return null;
            return Line.CreateBound(new0, new1);
        }

        private static List<Line> IntersectLineWithCurves(Line line, List<Curve> boundary)
        {
            var intersectionPoints = new List<XYZ>();
            foreach (var edge in boundary)
            {
                IntersectionResultArray results;
                SetComparisonResult res = line.Intersect(edge, out results);
                if (res == SetComparisonResult.Overlap || res == SetComparisonResult.Disjoint)
                {
                    if (results != null)
                    {
                        foreach (IntersectionResult ir in results)
                        {
                            intersectionPoints.Add(ir.XYZPoint);
                        }
                    }
                }
            }

            // Sort points along the line
            XYZ origin = line.GetEndPoint(0);
            XYZ dir = (line.GetEndPoint(1) - origin).Normalize();
            var sortedPoints = intersectionPoints
                .Select(p => new { Point = p, Distance = (p - origin).DotProduct(dir) })
                .OrderBy(x => x.Distance)
                .Select(x => x.Point)
                .ToList();

            // Deduplicate points
            var uniquePoints = new List<XYZ>();
            foreach (var p in sortedPoints)
            {
                if (!uniquePoints.Any(existing => existing.IsAlmostEqualTo(p)))
                    uniquePoints.Add(p);
            }

            var segments = new List<Line>();
            // Every pair of points (0-1, 2-3, ...) is a segment inside the boundary 
            // (Assumes a simple closed loop, or multiple loops that don't overlap strangely)
            for (int i = 0; i + 1 < uniquePoints.Count; i += 2)
            {
                // Verify midpoint is inside (optional but robust)
                segments.Add(Line.CreateBound(uniquePoints[i], uniquePoints[i + 1]));
            }

            return segments;
        }

        public static List<RebarDefinition> CreateSideRebars(HostGeometry host, string typeName, double sideDia, double spacing, bool overrideLeg, double legLen, double mainBarDia)
        {
            var definitions = new List<RebarDefinition>();
            if (host.BoundaryCurves == null || host.BoundaryCurves.Count == 0) return definitions;

            // Vertical range for side bars - similar to Footing Pad
            double cTop = host.CoverTop;
            double cBot = host.CoverBottom;
            double sideZTop = (host.SolidZMax - cTop) - mainBarDia * 2;
            double sideZBot = (host.SolidZMin + cBot) + mainBarDia * 2;
            double availableHeight = sideZTop - sideZBot;

            if (availableHeight <= 0) return definitions;

            // Row calculation
            int numSpaces = (int)Math.Ceiling(availableHeight / spacing);
            int rowCount = numSpaces - 1;
            if (rowCount < 0) rowCount = 0;

            // Calculate signed area to determine orientation (CCW vs CW)
            double signedArea = 0;
            foreach (var curve in host.BoundaryCurves)
            {
                XYZ p1 = curve.GetEndPoint(0);
                XYZ p2 = curve.GetEndPoint(1);
                signedArea += (p1.X * p2.Y - p2.X * p1.Y);
            }
            bool isCCW = signedArea > 0;

            foreach (var curve in host.BoundaryCurves)
            {
                if (!(curve is Line edge)) continue;

                XYZ edgeDir = (edge.GetEndPoint(1) - edge.GetEndPoint(0)).Normalize();
                
                // For a CCW loop, the inward normal is BasisZ.CrossProduct(edgeDir)
                // For a CW loop, the inward normal is edgeDir.CrossProduct(BasisZ)
                XYZ inwardDir = isCCW ? 
                    XYZ.BasisZ.CrossProduct(edgeDir).Normalize() : 
                    edgeDir.CrossProduct(XYZ.BasisZ).Normalize();
                
                // PLACEMENT LOGIC: Side bar sits on internal face of main bar
                double offsetInward = host.CoverExterior + mainBarDia + sideDia / 2.0;
                XYZ offset = inwardDir * offsetInward;

                // Adjust ends by side cover to avoid sticking out
                XYZ p0_base = edge.GetEndPoint(0) + offset + edgeDir * (host.CoverExterior + mainBarDia);
                XYZ p1_base = edge.GetEndPoint(1) + offset - edgeDir * (host.CoverExterior + mainBarDia);

                if (p0_base.DistanceTo(p1_base) < 0.1) continue;

                for (int row = 1; row <= rowCount; row++)
                {
                    double z = sideZBot + (availableHeight / numSpaces) * row;
                    XYZ p0 = new XYZ(p0_base.X, p0_base.Y, z);
                    XYZ p1 = new XYZ(p1_base.X, p1_base.Y, z);

                    definitions.Add(new RebarDefinition
                    {
                        Curves = new List<Curve> { Line.CreateBound(p0, p1) },
                        Style = RebarStyle.Standard,
                        BarTypeName = typeName,
                        BarDiameter = sideDia,
                        Normal = XYZ.BasisZ,
                        Spacing = 0,
                        FixedCount = 1,
                        Label = "Side Rebar",
                        Comment = "Side Bar"
                    });
                }
            }

            return definitions;
        }
    }
}
