using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class StripFootingLayoutGenerator
    {
        public static RebarDefinition CreateStirrup(
            HostGeometry host,
            string barTypeName,
            double barDiameter,
            double spacing,
            double startOffset,
            string hookStartName,
            string hookEndName)
        {
            double totalLen = host.Length;
            // Array length = total length minus start offset at BOTH ends as simplified logic
            double arrayLen = totalLen - 2 * startOffset;
            if (arrayLen <= 0 || spacing <= 0) return null;

            XYZ basisL = host.LAxis;
            XYZ basisW = host.WAxis;
            XYZ basisH = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double height = host.Height;
            double cTop = host.CoverTop;
            double cBot = host.CoverBottom;
            double cSide = host.CoverExterior;

            double stW = width - 2 * cSide;
            double stH = height - cTop - cBot;
            if (stW <= 0 || stH <= 0) return null;

            // Start position of the set
            XYZ setStart = host.StartPoint + basisL * startOffset;
            // Center the stirrup vertically considering uneven covers
            double zCenterOff = (cBot - cTop) / 2.0;

            // Points in Local Section (Width, height)
            XYZ p1 = new XYZ(-stW / 2.0, 0, -stH / 2.0 + zCenterOff);
            XYZ p2 = new XYZ(stW / 2.0, 0, -stH / 2.0 + zCenterOff);
            XYZ p3 = new XYZ(stW / 2.0, 0, stH / 2.0 + zCenterOff);
            XYZ p4 = new XYZ(-stW / 2.0, 0, stH / 2.0 + zCenterOff);

            XYZ ToGlobal(XYZ pt) => setStart + basisW * pt.X + basisH * pt.Z;

            List<Curve> curves = new List<Curve>
            {
                Line.CreateBound(ToGlobal(p3), ToGlobal(p4)),
                Line.CreateBound(ToGlobal(p4), ToGlobal(p1)),
                Line.CreateBound(ToGlobal(p1), ToGlobal(p2)),
                Line.CreateBound(ToGlobal(p2), ToGlobal(p3))
            };

            return new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.StirrupTie,
                BarTypeName = barTypeName,
                BarDiameter = barDiameter,
                Spacing = spacing,
                ArrayLength = arrayLen,
                Normal = basisL,
                HookStartName = hookStartName,
                HookEndName = hookEndName,
                HookStartOrientation = RebarHookOrientation.Left,
                HookEndOrientation = RebarHookOrientation.Left,
                Label = "Footing Stirrup"
            };
        }

        public static RebarDefinition? CreateLongitudinalLayer(
            HostGeometry host,
            RebarLayerConfig layer,
            double transDia,
            double startOff,
            double endOff)
        {
            if (layer.VerticalCount < 1) return null;

            XYZ basisL = host.LAxis;
            XYZ basisW = host.WAxis;
            XYZ basisH = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double height = host.Height;
            double cTop = host.CoverTop;
            double cBot = host.CoverBottom;
            double cSide = host.CoverExterior;

            // Inner width between stirrups
            double distWidth = width - 2 * (cSide + transDia);
            if (distWidth <= 0) return null;

            // Determine Z position
            double barDia = layer.BarDiameter_Backing; 
            // In the RebarRequest/Layer mapping, we might need to ensure diameters are resolved.
            // Let's assume diameter is passed or available.
            
            double zPos;
            RebarHookOrientation orient;
            if (layer.Side == RebarSide.Top)
            {
                zPos = (height / 2.0) - cTop - transDia - barDia / 2.0;
                orient = RebarHookOrientation.Left; // Pointing inward
            }
            else
            {
                zPos = -(height / 2.0) + cBot + transDia + barDia / 2.0;
                orient = RebarHookOrientation.Right; // Pointing inward
            }

            // Start/End points of the FIRST bar in the set (Far Left)
            // Now using host.CoverExterior instead of manual startOff/endOff
            XYZ barStart = host.StartPoint + basisL * host.CoverExterior + basisH * zPos - basisW * (distWidth / 2.0);
            XYZ barEnd = host.EndPoint - basisL * host.CoverExterior + basisH * zPos - basisW * (distWidth / 2.0);

            List<Curve> curves = new List<Curve> { Line.CreateBound(barStart, barEnd) };

            return new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.Standard,
                BarTypeName = layer.VerticalBarTypeName,
                BarDiameter = barDia,
                FixedCount = layer.VerticalCount,
                DistributionWidth = distWidth,
                ArrayDirection = basisW,
                Normal = basisW, // Planar normal for distribution
                HookStartName = layer.HookStartName,
                HookEndName = layer.HookEndName,
                HookStartOrientation = orient,
                HookEndOrientation = orient,
                Label = $"Footing Long. {layer.Side}"
            };
        }
    }
}
