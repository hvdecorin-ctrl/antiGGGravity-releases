using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Layout
{
    public static class WallCornerLayoutGenerator
    {
        public static List<RebarDefinition> CreateLBars(
            CornerInfo corner,
            RebarRequest request,
            double barDia)
        {
            var definitions = new List<RebarDefinition>();
            
            double thickness = corner.Thickness;
            double zMin = corner.ZMin;
            double zMax = corner.ZMax;
            XYZ cp = corner.Point;
            XYZ dir1 = corner.Dir1;
            XYZ dir2 = corner.Dir2;

            // Directions for normals (orient toward outer corner)
            XYZ adjNormal1 = corner.Normal1;
            if (corner.Normal1.DotProduct(-dir2) < 0) adjNormal1 = -corner.Normal1;

            XYZ adjNormal2 = corner.Normal2;
            if (corner.Normal2.DotProduct(-dir1) < 0) adjNormal2 = -corner.Normal2;

            double cExt = GetCoverDist(corner.Wall1, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GetCoverDist(corner.Wall1, BuiltInParameter.CLEAR_COVER_INTERIOR); 

            double hExt = (thickness / 2.0) - cExt - (barDia / 2.0);
            double hInt = (thickness / 2.0) - cInt - (barDia / 2.0);

            var layerOffsets = new List<double>();
            string config = request.WallLayerConfig;
            if (config == "Centre") layerOffsets.Add(barDia); // Offset by bar diameter to avoid vertical wall bars / trimmers
            else if (config == "Both faces") { layerOffsets.Add(hExt); layerOffsets.Add(-hInt); }
            else if (config == "External face") layerOffsets.Add(hExt);
            else if (config == "Internal face") layerOffsets.Add(-hInt);

            double spacing = request.VerticalSpacing;
            double hRange = (zMax - zMin) - request.TransverseStartOffset - request.TransverseEndOffset;
            if (hRange <= 0) return definitions;

            foreach (double offsetDist in layerOffsets)
            {
                XYZ cornerOffset = cp + adjNormal1 * offsetDist + adjNormal2 * offsetDist;
                XYZ p_corner = new XYZ(cornerOffset.X, cornerOffset.Y, zMin + request.TransverseStartOffset);
                XYZ p_start = p_corner + dir1 * request.LegLength1;
                XYZ p_end = p_corner + dir2 * request.LegLength2;

                var curves = new List<Curve> {
                    Line.CreateBound(p_start, p_corner),
                    Line.CreateBound(p_corner, p_end)
                };

                definitions.Add(new RebarDefinition
                {
                    Curves = curves,
                    Style = RebarStyle.Standard,
                    BarTypeName = request.VerticalBarTypeName,
                    BarDiameter = barDia,
                    Spacing = spacing,
                    ArrayLength = hRange,
                    Normal = XYZ.BasisZ,
                    HookStartName = null, // Hooks could be added to DTO if needed
                    HookEndName = null,
                    Label = "Wall Corner L"
                });
            }

            // Trimmers
            if (request.AddTrimmers && !string.IsNullOrEmpty(request.TrimmerBarTypeName))
            {
                definitions.Add(new RebarDefinition
                {
                    Curves = new List<Curve> { 
                        Line.CreateBound(
                            new XYZ(cp.X, cp.Y, zMin + request.TransverseStartOffset),
                            new XYZ(cp.X, cp.Y, zMax - request.TransverseEndOffset))
                    },
                    Style = RebarStyle.Standard,
                    BarTypeName = request.TrimmerBarTypeName,
                    Normal = adjNormal1,
                    Label = "Wall Corner Trimmer"
                });
            }

            return definitions;
        }

        public static List<RebarDefinition> CreateUBars(
            CornerInfo corner,
            RebarRequest request,
            double barDia)
        {
            var definitions = new List<RebarDefinition>();
            
            XYZ cp = corner.Point;
            XYZ dir1 = corner.Dir1;
            XYZ dir2 = corner.Dir2;
            XYZ normal1 = corner.Normal1;
            XYZ normal2 = corner.Normal2;

            double t1 = corner.Thickness; 
            // In a better DTO, we'd have thicknesses for both walls, but for now assuming same
            double t2 = corner.Thickness;

            double cExt1 = GetCoverDist(corner.Wall1, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cExt2 = GetCoverDist(corner.Wall2, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            
            // Offset inside by exactly half a bar diameter to fit inside vertical bars
            double offset1 = (t1 / 2.0) - cExt1 - (barDia / 2.0);
            double offset2 = (t2 / 2.0) - cExt2 - (barDia / 2.0);

            double spacing = request.VerticalSpacing;
            double zMin = corner.ZMin;
            double zMax = corner.ZMax;
            double hRange = (zMax - zMin) - request.TransverseStartOffset - request.TransverseEndOffset;
            if (hRange <= 0) return definitions;

            // U-bar for Wall 1
            definitions.Add(CreateSingleUDef(corner.Wall1, request.VerticalBarTypeName, barDia, 
                cp, dir1, dir2, normal1, normal2, 
                zMin + request.TransverseStartOffset, request.LegLength1, request.LegLength1, 
                spacing, hRange, offset1, offset2, "Wall 1 U-Bar"));

            // U-bar for Wall 2
            definitions.Add(CreateSingleUDef(corner.Wall2, request.VerticalBarTypeName, barDia, 
                cp, dir2, dir1, normal2, normal1, 
                zMin + request.TransverseStartOffset, request.LegLength2, request.LegLength2, 
                spacing, hRange, offset2, offset1, "Wall 2 U-Bar"));

            // Trimmers
            if (request.AddTrimmers && !string.IsNullOrEmpty(request.TrimmerBarTypeName))
            {
                var trimmerPositions = GetTrimmerPositions(cp, dir1, dir2, normal1, normal2, offset1, offset2, barDia);
                foreach (var pos in trimmerPositions)
                {
                    definitions.Add(new RebarDefinition
                    {
                        Curves = new List<Curve> { 
                            Line.CreateBound(
                                new XYZ(pos.X, pos.Y, zMin + request.TransverseStartOffset),
                                new XYZ(pos.X, pos.Y, zMax - request.TransverseEndOffset))
                        },
                        BarTypeName = request.TrimmerBarTypeName,
                        Normal = normal1,
                        Label = "Corner Trimmer (U)"
                    });
                }
            }

            return definitions;
        }

        private static RebarDefinition CreateSingleUDef(
            Wall host, string barType, double dia,
            XYZ cp, XYZ d1, XYZ d2, XYZ n1, XYZ n2,
            double zStart, double leg1, double leg2,
            double spacing, double hRange, double off1, double off2, string label)
        {
            XYZ p1 = cp + n1 * off1 - d1 * off2;
            XYZ p2 = cp - n1 * off1 - d1 * off2;
            XYZ p_start = p1 + d1 * leg1;
            XYZ p_end = p2 + d1 * leg2;

            p1 = new XYZ(p1.X, p1.Y, zStart);
            p2 = new XYZ(p2.X, p2.Y, zStart);
            p_start = new XYZ(p_start.X, p_start.Y, zStart);
            p_end = new XYZ(p_end.X, p_end.Y, zStart);

            return new RebarDefinition
            {
                Curves = new List<Curve> {
                    Line.CreateBound(p_start, p1),
                    Line.CreateBound(p1, p2),
                    Line.CreateBound(p2, p_end)
                },
                Style = RebarStyle.Standard,
                BarTypeName = barType,
                BarDiameter = dia,
                Spacing = spacing,
                ArrayLength = hRange,
                Normal = XYZ.BasisZ,
                Label = label
            };
        }

        private static List<XYZ> GetTrimmerPositions(XYZ cp, XYZ d1, XYZ d2, XYZ n1, XYZ n2, double off1, double off2, double barDia)
        {
            XYZ w1Ext = cp + n1 * off1;
            XYZ w1Int = cp - n1 * off1;
            XYZ w2Ext = cp + n2 * off2;
            XYZ w2Int = cp - n2 * off2;

            var rawPositions = new List<XYZ> {
                LineIntersection2D(w1Ext, d1, w2Ext, d2),
                LineIntersection2D(w1Ext, d1, w2Int, d2),
                LineIntersection2D(w1Int, d1, w2Ext, d2),
                LineIntersection2D(w1Int, d1, w2Int, d2)
            };

            var trimmerPositions = new List<XYZ>();
            double centerX = rawPositions.Average(p => p?.X ?? cp.X);
            double centerY = rawPositions.Average(p => p?.Y ?? cp.Y);
            XYZ center = new XYZ(centerX, centerY, cp.Z);

            double inwardOff = barDia * 2;
            foreach (var pos in rawPositions.Where(p => p != null))
            {
                XYZ toCenter = (center - pos).Normalize() * inwardOff;
                trimmerPositions.Add(pos + toCenter);
            }
            return trimmerPositions;
        }

        private static XYZ LineIntersection2D(XYZ p1, XYZ d1, XYZ p2, XYZ d2)
        {
            double cross = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(cross) < 1e-10) return null;
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double t = (dx * d2.Y - dy * d2.X) / cross;
            return new XYZ(p1.X + t * d1.X, p1.Y + t * d1.Y, p1.Z);
        }

        private static double GetCoverDist(Wall wall, BuiltInParameter param)
        {
            Parameter coverParam = wall.get_Parameter(param);
            if (coverParam != null)
            {
                ElementId coverId = coverParam.AsElementId();
                if (coverId != ElementId.InvalidElementId)
                {
                    RebarCoverType coverType = wall.Document.GetElement(coverId) as RebarCoverType;
                    if (coverType != null)
                    {
                        return coverType.CoverDistance;
                    }
                }
            }
            return 50.0 / 304.8; // 50mm fallback
        }
    }
}
