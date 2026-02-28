using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    /// <summary>
    /// Generates longitudinal (parallel) bar RebarDefinitions.
    /// Dual-mode: absolute Z for horizontal beams, LCS offset for slanted beams.
    /// </summary>
    public static class ParallelLayoutGenerator
    {
        /// <summary>
        /// Creates a longitudinal bar layer for a HORIZONTAL beam.
        /// Uses absolute world Z positioning (from solid geometry bbox).
        /// </summary>
        public static RebarDefinition CreateLayerFlat(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            int count,
            double absoluteZ,
            double transDiameter,
            bool isTop,
            string hookStartName = null,
            string hookEndName = null,
            string label = "Longitudinal",
            bool overrideHookLength = false,
            double hookLengthOverride = 0)
        {
            if (count < 1) return null;

            double innerOffset = host.CoverOther + transDiameter;
            double distWidth = host.Width - 2 * innerOffset;

            XYZ s = host.StartPoint + host.LAxis * host.CoverOther;
            XYZ e = host.EndPoint - host.LAxis * host.CoverOther;

            // XY from curve endpoints, Z from absolute position
            XYZ barStart = new XYZ(s.X, s.Y, absoluteZ) - host.WAxis * (distWidth / 2.0);
            XYZ barEnd = new XYZ(e.X, e.Y, absoluteZ) - host.WAxis * (distWidth / 2.0);

            return BuildDefinition(host, barTypeName, barDiameter, count, distWidth,
                barStart, barEnd, isTop, hookStartName, hookEndName, label,
                overrideHookLength, hookLengthOverride);
        }

        /// <summary>
        /// Creates a longitudinal bar layer for a SLANTED beam.
        /// Uses HAxis offset from start/end points (follows slope).
        /// </summary>
        public static RebarDefinition CreateLayerLCS(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            int count,
            double heightOffset,
            double transDiameter,
            bool isTop,
            string hookStartName = null,
            string hookEndName = null,
            string label = "Longitudinal",
            bool overrideHookLength = false,
            double hookLengthOverride = 0,
            string comment = null)
        {
            if (count < 1) return null;

            double innerOffset = host.CoverOther + transDiameter;
            double distWidth = host.Width - 2 * innerOffset;

            XYZ s = host.StartPoint + host.LAxis * host.CoverOther;
            XYZ e = host.EndPoint - host.LAxis * host.CoverOther;

            // Position using HAxis (follows slope) and WAxis (horizontal offset)
            XYZ barStart = s + host.HAxis * heightOffset - host.WAxis * (distWidth / 2.0);
            XYZ barEnd = e + host.HAxis * heightOffset - host.WAxis * (distWidth / 2.0);

            return BuildDefinition(host, barTypeName, barDiameter, count, distWidth,
                barStart, barEnd, isTop, hookStartName, hookEndName, label,
                overrideHookLength, hookLengthOverride, comment);
        }

        private static RebarDefinition BuildDefinition(
            HostGeometry host,
            string barTypeName, double barDiameter, int count, double distWidth,
            XYZ barStart, XYZ barEnd, bool isTop,
            string hookStartName, string hookEndName, string label,
            bool overrideHookLength = false, double hookLengthOverride = 0, string comment = null)
        {
            Curve barLine = Line.CreateBound(barStart, barEnd);
            RebarHookOrientation orient = isTop
                ? RebarHookOrientation.Left
                : RebarHookOrientation.Right;

            return new RebarDefinition
            {
                Curves = new List<Curve> { barLine },
                Style = RebarStyle.Standard,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = 0,
                ArrayLength = 0,
                ArrayDirection = host.WAxis,
                FixedCount = count,
                DistributionWidth = distWidth,
                Normal = host.WAxis,
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                HookStartOrientation = orient,
                HookEndOrientation = orient,
                OverrideHookLength = overrideHookLength,
                HookLengthOverride = hookLengthOverride,
                Label = label,
                Comment = comment
            };
        }
    }
}
