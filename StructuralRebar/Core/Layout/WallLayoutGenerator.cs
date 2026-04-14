using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    /// <summary>
    /// Generates wall rebar definitions.
    /// Each method creates ONE RebarDefinition that is arrayed via SetLayoutAsMaximumSpacing.
    /// Matches the original WallRebarCommand approach: single bar + array = maximum spacing.
    /// </summary>
    public static class WallLayoutGenerator
    {
        /// <summary>
        /// Creates a SINGLE vertical bar definition that will be arrayed along the wall length.
        /// The bar is positioned at the start offset, and SetLayoutAsMaximumSpacing distributes
        /// copies along totalLen at the given spacing.
        /// </summary>
        public static RebarDefinition CreateVerticalBars(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double spacing,
            double startOffset,
            double endOffset,
            double topCover,
            double botCover,
            double topExt,
            double botExt,
            double sideOffset,
            string hookStartName,
            string hookEndName,
            bool hookStartOut,
            bool hookEndOut)
        {
            double totalLen = host.Length - startOffset - endOffset;
            if (totalLen <= 0 || spacing <= 0) return null;

            XYZ lAxis = host.LAxis;
            XYZ wAxis = host.WAxis; // Normal to wall
            double zMin = host.SolidZMin;
            double zMax = host.SolidZMax;

            // Hook orientation based on side
            RebarHookOrientation orientS, orientE;
            if (sideOffset >= 0) // Exterior or Centre
            {
                orientS = hookStartOut ? (RebarHookOrientation)(-1) : (RebarHookOrientation)1;
                orientE = hookEndOut ? (RebarHookOrientation)(-1) : (RebarHookOrientation)1;
            }
            else // Interior
            {
                orientS = hookStartOut ? (RebarHookOrientation)1 : (RebarHookOrientation)(-1);
                orientE = hookEndOut ? (RebarHookOrientation)1 : (RebarHookOrientation)(-1);
            }

            // Position the FIRST bar at the start offset
            XYZ point = host.StartPoint + lAxis * startOffset + wAxis * sideOffset;

            // Vertical extent
            double zStart = zMin + botCover - botExt;
            double zEnd = zMax - topCover + topExt;

            XYZ startXYZ = new XYZ(point.X, point.Y, zStart);
            XYZ endXYZ = new XYZ(point.X, point.Y, zEnd);

            Curve barLine = Line.CreateBound(startXYZ, endXYZ);

            return new RebarDefinition
            {
                Curves = new List<Curve> { barLine },
                Style = RebarStyle.Standard,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = spacing,
                ArrayLength = totalLen,
                Normal = lAxis, // Normal = wallDir for vertical bars (array along wall)
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                HookStartOrientation = orientS,
                HookEndOrientation = orientE,
                Label = "Vertical Bar",
                Comment = "Vertical Bar"
            };
        }

        /// <summary>
        /// Creates a SINGLE horizontal bar definition that will be arrayed vertically.
        /// The bar is positioned at the bottom offset Z, and SetLayoutAsMaximumSpacing distributes
        /// copies upward within hRange at the given spacing.
        /// </summary>
        public static RebarDefinition CreateHorizontalBars(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double spacing,
            double topOffset,
            double botOffset,
            double startCover,
            double endCover,
            double sideOffset,
            string hookStartName,
            string hookEndName,
            bool hookStartOut,
            bool hookEndOut)
        {
            double hRange = host.Height - topOffset - botOffset;
            if (hRange <= 0 || spacing <= 0) return null;

            XYZ lAxis = host.LAxis;
            XYZ wAxis = host.WAxis;
            double zMin = host.SolidZMin;

            // Hook orientation based on side
            RebarHookOrientation orientS, orientE;
            if (sideOffset >= 0) // Exterior or Centre
            {
                orientS = hookStartOut ? (RebarHookOrientation)1 : (RebarHookOrientation)(-1);
                orientE = hookEndOut ? (RebarHookOrientation)1 : (RebarHookOrientation)(-1);
            }
            else // Interior
            {
                orientS = hookStartOut ? (RebarHookOrientation)(-1) : (RebarHookOrientation)1;
                orientE = hookEndOut ? (RebarHookOrientation)(-1) : (RebarHookOrientation)1;
            }

            // Position: bottom bar at z = zMin + botOffset
            double zStartVal = zMin + botOffset;

            XYZ p0 = host.StartPoint;
            XYZ p1 = host.EndPoint;

            XYZ startXYZ = new XYZ(p0.X, p0.Y, zStartVal) + lAxis * startCover + wAxis * sideOffset;
            XYZ endXYZ = new XYZ(p1.X, p1.Y, zStartVal) - lAxis * endCover + wAxis * sideOffset;

            Curve barLine = Line.CreateBound(startXYZ, endXYZ);

            return new RebarDefinition
            {
                Curves = new List<Curve> { barLine },
                Style = RebarStyle.Standard,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = spacing,
                ArrayLength = hRange,
                Normal = XYZ.BasisZ, // Normal = Z for horizontal bars (array vertically)
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                HookStartOrientation = orientS,
                HookEndOrientation = orientE,
                Label = "Horizontal Bar",
                Comment = "Horizontal Bar"
            };
        }
    }
}
