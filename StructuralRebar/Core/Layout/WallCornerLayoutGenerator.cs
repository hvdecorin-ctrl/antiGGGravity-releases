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
                        Style = RebarStyle.Standard,
                        BarTypeName = request.TrimmerBarTypeName,
                        Normal = normal1,
                        Label = "Corner Trimmer (U)"
                    });
                }
            }

            return definitions;
        }

        /// <summary>
        /// Creates U-bars at a free (orphaned) wall end.
        /// The U wraps around the exposed wall end:
        ///   leg on ext face → across end face → leg on int face
        /// Arrayed vertically at specified spacing.
        /// </summary>
        public static List<RebarDefinition> CreateWallEndUBars(
            Wall wall, XYZ endPt, XYZ dirIntoWall, RebarRequest request, double barDia)
        {
            var definitions = new List<RebarDefinition>();
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox == null) return definitions;

            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;
            double hRange = (zMax - zMin) - request.TransverseStartOffset - request.TransverseEndOffset;
            if (hRange <= 0) return definitions;

            double thickness = wall.Width;
            double cExt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_INTERIOR);
            double offsetExt = (thickness / 2.0) - cExt - (barDia / 2.0);
            double offsetInt = (thickness / 2.0) - cInt - (barDia / 2.0);

            // Wall normal (perpendicular to wall length in plan)
            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line wallLine)) return definitions;
            XYZ tangent = wallLine.Direction;
            XYZ normal = new XYZ(-tangent.Y, tangent.X, 0).Normalize();

            // Leg lengths from dedicated wall-end group
            double legLen1 = request.WallEndLeg1;
            double legLen2 = request.WallEndLeg2;

            // End cover — shift the U-bar crosspiece inward by cover distance
            double cEnd = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_OTHER);
            XYZ coveredEndPt = endPt + dirIntoWall * (cEnd + barDia / 2.0);

            double zStart = zMin + request.TransverseStartOffset;

            // Build U-shape at z=zStart:
            XYZ p1 = new XYZ(coveredEndPt.X + normal.X * offsetExt, coveredEndPt.Y + normal.Y * offsetExt, zStart);
            XYZ p2 = new XYZ(coveredEndPt.X - normal.X * offsetInt, coveredEndPt.Y - normal.Y * offsetInt, zStart);
            XYZ p_start = p1 + dirIntoWall * legLen1;
            XYZ p_end = p2 + dirIntoWall * legLen2;

            var curves = new List<Curve>
            {
                Line.CreateBound(p_start, p1),  // leg on ext face
                Line.CreateBound(p1, p2),        // across wall end
                Line.CreateBound(p2, p_end)      // leg on int face
            };

            definitions.Add(new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.Standard,
                BarTypeName = request.WallEndBarTypeName,
                BarDiameter = barDia,
                Spacing = request.WallEndSpacing,
                ArrayLength = hRange,
                Normal = XYZ.BasisZ,
                Label = "Wall End U-Bar"
            });

            return definitions;
        }

        /// <summary>
        /// Creates U-bars along the top edge of a wall.
        /// The U wraps over the wall top:
        ///   leg going down on ext face → across wall top → leg going down on int face
        /// Arrayed horizontally along wall length at specified spacing.
        /// </summary>
        public static List<RebarDefinition> CreateWallTopUBars(
            Wall wall, RebarRequest request, double barDia, double startTrim = 0, double endTrim = 0)
        {
            var definitions = new List<RebarDefinition>();
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox == null) return definitions;

            double zMax = bbox.Max.Z;
            double thickness = wall.Width;
            double cExt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_INTERIOR);
            double offsetExt = (thickness / 2.0) - cExt - (barDia / 2.0);
            double offsetInt = (thickness / 2.0) - cInt - (barDia / 2.0);

            // Layer alignment: "Vert Internal" shifts legs inward by 1*barDia
            string layer = request.TopEndLayer ?? "Vert External";
            if (layer == "Vert Internal")
            {
                double inwardShift = 1.0 * barDia;
                offsetExt -= inwardShift;
                offsetInt -= inwardShift;
            }

            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line wallLine)) return definitions;
            XYZ tangent = wallLine.Direction;
            XYZ normal = new XYZ(-tangent.Y, tangent.X, 0).Normalize();

            double legLen1 = request.TopEndLeg1;
            double legLen2 = request.TopEndLeg2;

            // Wall length minus covers and intersection trims
            double cOther = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_OTHER);

            // Z position: crosspiece at host cover from wall top
            double topZ = zMax - cOther - (barDia / 2.0);

            // Distribution offset applies to array first/last bar along wall length
            double wallLength = wallLine.Length;
            double distStart = request.TopBotTopOffset > 0 ? request.TopBotTopOffset : cOther;
            double distEnd = request.TopBotBotOffset > 0 ? request.TopBotBotOffset : cOther;
            double coverStart = startTrim + distStart;
            double coverEnd = endTrim + distEnd;
            double arrayLen = wallLength - coverStart - coverEnd;
            if (arrayLen <= 0) return definitions;

            XYZ wallStart = wallLine.GetEndPoint(0);
            XYZ refPt = wallStart + tangent * coverStart;

            XYZ p_ext_bot = new XYZ(
                refPt.X + normal.X * offsetExt,
                refPt.Y + normal.Y * offsetExt,
                topZ - legLen1);
            XYZ p_ext_top = new XYZ(
                refPt.X + normal.X * offsetExt,
                refPt.Y + normal.Y * offsetExt,
                topZ);
            XYZ p_int_top = new XYZ(
                refPt.X - normal.X * offsetInt,
                refPt.Y - normal.Y * offsetInt,
                topZ);
            XYZ p_int_bot = new XYZ(
                refPt.X - normal.X * offsetInt,
                refPt.Y - normal.Y * offsetInt,
                topZ - legLen2);

            var curves = new List<Curve>
            {
                Line.CreateBound(p_ext_bot, p_ext_top),
                Line.CreateBound(p_ext_top, p_int_top),
                Line.CreateBound(p_int_top, p_int_bot)
            };

            definitions.Add(new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.Standard,
                BarTypeName = request.TopEndBarTypeName,
                BarDiameter = barDia,
                Spacing = request.TopEndSpacing,
                ArrayLength = arrayLen,
                Normal = tangent,
                Label = "Wall Top U-Bar"
            });

            return definitions;
        }

        /// <summary>
        /// Creates U-bars along the bottom edge of a wall.
        /// Mirror of top U-bars: legs extend upward from the bottom.
        /// </summary>
        public static List<RebarDefinition> CreateWallBottomUBars(
            Wall wall, RebarRequest request, double barDia, double startTrim = 0, double endTrim = 0)
        {
            var definitions = new List<RebarDefinition>();
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox == null) return definitions;

            double zMin = bbox.Min.Z;
            double thickness = wall.Width;
            double cExt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_EXTERIOR);
            double cInt = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_INTERIOR);
            double offsetExt = (thickness / 2.0) - cExt - (barDia / 2.0);
            double offsetInt = (thickness / 2.0) - cInt - (barDia / 2.0);

            // Layer alignment: "Vert Internal" shifts legs inward by 1*barDia
            string layer = request.BotEndLayer ?? "Vert External";
            if (layer == "Vert Internal")
            {
                double inwardShift = 1.0 * barDia;
                offsetExt -= inwardShift;
                offsetInt -= inwardShift;
            }

            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line wallLine)) return definitions;
            XYZ tangent = wallLine.Direction;
            XYZ normal = new XYZ(-tangent.Y, tangent.X, 0).Normalize();

            double legLen1 = request.BotEndLeg1;
            double legLen2 = request.BotEndLeg2;

            // Wall length minus covers and intersection trims
            double cOther = GetCoverDist(wall, BuiltInParameter.CLEAR_COVER_OTHER);

            // Z position: crosspiece at host cover from wall bottom
            double botZ = zMin + cOther + (barDia / 2.0);

            // Distribution offset applies to array first/last bar along wall length
            double wallLength = wallLine.Length;
            double distStart = request.TopBotTopOffset > 0 ? request.TopBotTopOffset : cOther;
            double distEnd = request.TopBotBotOffset > 0 ? request.TopBotBotOffset : cOther;
            double coverStart = startTrim + distStart;
            double coverEnd = endTrim + distEnd;
            double arrayLen = wallLength - coverStart - coverEnd;
            if (arrayLen <= 0) return definitions;

            XYZ wallStart = wallLine.GetEndPoint(0);
            XYZ refPt = wallStart + tangent * coverStart;

            // U wraps under the bottom: legs go UP on each face
            XYZ p_ext_top = new XYZ(
                refPt.X + normal.X * offsetExt,
                refPt.Y + normal.Y * offsetExt,
                botZ + legLen1);
            XYZ p_ext_bot = new XYZ(
                refPt.X + normal.X * offsetExt,
                refPt.Y + normal.Y * offsetExt,
                botZ);
            XYZ p_int_bot = new XYZ(
                refPt.X - normal.X * offsetInt,
                refPt.Y - normal.Y * offsetInt,
                botZ);
            XYZ p_int_top = new XYZ(
                refPt.X - normal.X * offsetInt,
                refPt.Y - normal.Y * offsetInt,
                botZ + legLen2);

            var curves = new List<Curve>
            {
                Line.CreateBound(p_ext_top, p_ext_bot),  // ext face leg going down
                Line.CreateBound(p_ext_bot, p_int_bot),  // across wall bottom
                Line.CreateBound(p_int_bot, p_int_top)   // int face leg going up
            };

            definitions.Add(new RebarDefinition
            {
                Curves = curves,
                Style = RebarStyle.Standard,
                BarTypeName = request.BotEndBarTypeName,
                BarDiameter = barDia,
                Spacing = request.BotEndSpacing,
                ArrayLength = arrayLen,
                Normal = tangent,
                Label = "Wall Bottom U-Bar"
            });

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
