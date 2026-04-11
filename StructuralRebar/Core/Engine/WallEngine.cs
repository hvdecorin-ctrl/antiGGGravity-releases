using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Calculators;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    public class WallEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public WallEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            if (!(host is Wall wall)) return false;
            return ProcessWall(wall, request);
        }

        public (int Processed, int Total) GenerateWallRebar(List<Wall> walls, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Wall Rebar"))
            {
                t.Start();
                foreach (var wall in walls)
                {
                    try
                    {
                        if (request.RemoveExisting)
                            _creationService.DeleteExistingRebar(wall);

                        if (ProcessWall(wall, request))
                            processed++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WallEngine: {wall.Id} failed: {ex.Message}");
                    }
                }
                t.Commit();
            }
            return (processed, walls.Count);
        }

        public (int Processed, int Total) GenerateWallStackRebar(List<Wall> stack, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Multi-Level Wall Rebar"))
            {
                t.Start();
                try
                {
                    processed = ProcessWallStack(stack, request) ? stack.Count : 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WallEngine: Wall stack failed: {ex.Message}");
                }
                t.Commit();
            }
            return (processed, stack.Count);
        }

        public (int Processed, int Total) GenerateWallCornerRebar(List<Wall> walls, RebarRequest request)
        {
            var corners = WallCornerGeometryModule.FindCorners(walls);
            
            // Find wall ends that don't intersect with any other selected wall
            var orphanedEnds = new List<(Wall Wall, XYZ EndPt, XYZ Dir)>();
            if (request.HostType == ElementHostType.WallCornerU && request.AddWallEndUBars)
            {
                foreach (var wall in walls)
                {
                    HostGeometry host = WallGeometryModule.Read(_doc, wall);
                    if (host.Length <= 0) continue;

                    XYZ[] ends = { host.StartPoint, host.EndPoint };
                    // Direction INTO the wall from start end is +LAxis, from end is -LAxis
                    XYZ[] dirs = { host.LAxis, -host.LAxis };

                    for (int i = 0; i < 2; i++)
                    {
                        XYZ endPt = ends[i];
                        bool isCorner = corners.Any(c => 
                            (c.Wall1.Id == wall.Id && c.Point.DistanceTo(endPt) < 1.0) ||
                            (c.Wall2.Id == wall.Id && c.Point.DistanceTo(endPt) < 1.0));

                        if (!isCorner)
                            orphanedEnds.Add((wall, endPt, dirs[i]));
                    }
                }
            }

            int cornerCount = 0;
            int endCount = 0;
            int topCount = 0;
            int botCount = 0;

            // Track walls already cleaned to avoid duplicate deletes
            var cleanedWallIds = new HashSet<ElementId>();

            using (Transaction t = new Transaction(_doc, "Generate Wall Corner Rebar"))
            {
                t.Start();

                // 1. Process corners (intersection U-bars) — only if checkbox is checked
                if (request.HostType == ElementHostType.WallCornerL || 
                    (request.HostType == ElementHostType.WallCornerU && request.AddIntersectUBars))
                {
                    foreach (var corner in corners)
                    {
                        try
                        {
                            if (request.RemoveExisting)
                            {
                                if (cleanedWallIds.Add(corner.Wall1.Id))
                                    _creationService.DeleteExistingRebar(corner.Wall1);
                                if (cleanedWallIds.Add(corner.Wall2.Id))
                                    _creationService.DeleteExistingRebar(corner.Wall2);
                            }

                            bool success = false;
                            if (request.HostType == ElementHostType.WallCornerL)
                                success = ProcessWallCornerL(corner, request);
                            else if (request.HostType == ElementHostType.WallCornerU)
                                success = ProcessWallCornerU(corner, request);

                            if (success) cornerCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Wall Corner failed: {ex.Message}");
                        }
                    }
                }

                // 2. Process orphaned wall ends (Wall End U-bars)
                foreach (var orphanedEnd in orphanedEnds)
                {
                    try
                    {
                        if (request.RemoveExisting && cleanedWallIds.Add(orphanedEnd.Wall.Id))
                            _creationService.DeleteExistingRebar(orphanedEnd.Wall);

                        double barDia = GetBarDiameter(request.WallEndBarTypeName);
                        if (barDia <= 0) continue;

                        var definitions = WallCornerLayoutGenerator.CreateWallEndUBars(
                            orphanedEnd.Wall, orphanedEnd.EndPt, orphanedEnd.Dir, request, barDia);
                        if (definitions.Count > 0)
                        {
                            var ids = _creationService.PlaceRebar(orphanedEnd.Wall, definitions);
                            if (ids.Count > 0) endCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Wall End U-Bar failed: {ex.Message}");
                    }
                }

                if (request.HostType == ElementHostType.WallCornerU && request.AddTopEndUBars)
                {
                    foreach (var wall in walls)
                    {
                        try
                        {
                            if (request.RemoveExisting && cleanedWallIds.Add(wall.Id))
                                _creationService.DeleteExistingRebar(wall);

                            double barDia = GetBarDiameter(request.TopEndBarTypeName);
                            if (barDia <= 0) continue;

                            var (startTrim, endTrim) = FindWallFaceTrimDistances(wall);
                            var definitions = WallCornerLayoutGenerator.CreateWallTopUBars(wall, request, barDia, startTrim, endTrim);
                            if (definitions.Count > 0)
                            {
                                var ids = _creationService.PlaceRebar(wall, definitions);
                                if (ids.Count > 0) topCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Wall Top U-Bar failed: {ex.Message}");
                        }
                    }
                }

                // 4. Process wall bottoms (Bottom U-bars) — per wall, along entire length
                if (request.HostType == ElementHostType.WallCornerU && request.AddBotEndUBars)
                {
                    foreach (var wall in walls)
                    {
                        try
                        {
                            if (request.RemoveExisting && cleanedWallIds.Add(wall.Id))
                                _creationService.DeleteExistingRebar(wall);

                            double barDia = GetBarDiameter(request.BotEndBarTypeName);
                            if (barDia <= 0) continue;

                            var (startTrim, endTrim) = FindWallFaceTrimDistances(wall);
                            var definitions = WallCornerLayoutGenerator.CreateWallBottomUBars(wall, request, barDia, startTrim, endTrim);
                            if (definitions.Count > 0)
                            {
                                var ids = _creationService.PlaceRebar(wall, definitions);
                                if (ids.Count > 0) botCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Wall Bottom U-Bar failed: {ex.Message}");
                        }
                    }
                }

                t.Commit();
            }

            // Build a meaningful result
            int totalProcessed = cornerCount + endCount + topCount + botCount;
            int totalItems = corners.Count + orphanedEnds.Count
                + (request.AddTopEndUBars ? walls.Count : 0)
                + (request.AddBotEndUBars ? walls.Count : 0);
            return (totalProcessed, totalItems > 0 ? totalItems : corners.Count);
        }

        // --- Implementation Methods ---

        private bool ProcessWall(Wall wall, RebarRequest request)
        {
            HostGeometry host = WallGeometryModule.Read(_doc, wall);
            if (host.Length <= 0 || host.Width <= 0 || host.Height <= 0) return false;

            // Find trim distances at intersecting wall faces
            var (startTrim, endTrim) = FindWallFaceTrimDistances(wall);

            var definitions = new List<RebarDefinition>();

            double vDia = GetBarDiameter(request.TransverseBarTypeName);
            double hDia = 0;
            if (request.Layers.Count > 0)
                hDia = GetBarDiameter(request.Layers[0].HorizontalBarTypeName);

            double thickness = host.Width;

            // ==============================================================
            //  LAYER OFFSET CALCULATIONS (matches original WallRebarCommand)
            //  Distances from CENTRE LINE of wall along wallNormal
            //  Exterior face is at +thickness/2
            //  Interior face is at -thickness/2
            // ==============================================================

            // Exterior layer offsets
            double dExtH = (thickness / 2.0) - host.CoverExterior - (hDia / 2.0);
            double dExtV = dExtH - (hDia / 2.0) - (vDia / 2.0);

            // Interior layer offsets
            double dIntH = (thickness / 2.0) - host.CoverInterior - (hDia / 2.0);
            double dIntV = dIntH - (hDia / 2.0) - (vDia / 2.0);

            // Build list of (vOffset, hOffset) tuples
            var layers = new List<(double vOff, double hOff)>();

            string config = request.WallLayerConfig ?? "Centre";
            if (config.Equals("Centre", StringComparison.OrdinalIgnoreCase))
            {
                // Vertical at centre (0), horizontal beside vertical
                layers.Add((0, (vDia / 2.0 + hDia / 2.0)));
            }
            else if (config.Equals("Both Faces", StringComparison.OrdinalIgnoreCase))
            {
                layers.Add((dExtV, dExtH));
                layers.Add((-dIntV, -dIntH));
            }
            else if (config.Equals("External Face", StringComparison.OrdinalIgnoreCase))
            {
                layers.Add((dExtV, dExtH));
            }
            else if (config.Equals("Internal Face", StringComparison.OrdinalIgnoreCase))
            {
                layers.Add((-dIntV, -dIntH));
            }

            // Generate rebar for each layer
            foreach (var (vOff, hOff) in layers)
            {
                // 1. Vertical Bars
                if (!string.IsNullOrEmpty(request.TransverseBarTypeName) && vDia > 0)
                {
                    var vertDef = WallLayoutGenerator.CreateVerticalBars(
                        host, request.TransverseBarTypeName, vDia,
                        request.TransverseSpacing,
                        request.TransverseStartOffset + startTrim,
                        request.TransverseEndOffset + endTrim,
                        host.CoverOther, host.CoverOther, // Top/Bot cover (Other)
                        request.VerticalTopExtension, request.VerticalBottomExtension,
                        vOff,
                        request.TransverseHookStartName, request.TransverseHookEndName,
                        request.TransverseHookStartOut, request.TransverseHookEndOut);

                    if (vertDef != null)
                    {
                        double barLen = (host.SolidZMax - host.SolidZMin) + request.VerticalTopExtension + request.VerticalBottomExtension - 2 * host.CoverOther;
                        
                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                        {
                            var segments = LapSpliceCalculator.SplitBarForLap(
                                barLen, vertDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter), 
                                BarPosition.Other, GetLapSpliceLength(vertDef.BarDiameter, request));

                            if (segments.Count <= 1)
                            {
                                definitions.Add(vertDef);
                            }
                            else
                            {
                                var origLine = vertDef.Curves[0] as Line;
                                if (origLine == null) { definitions.Add(vertDef); continue; }
                                XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                XYZ barStart = origLine.GetEndPoint(0);

                                for (int si = 0; si < segments.Count; si++)
                                {
                                    var seg = segments[si];
                                    XYZ segStart = barStart + barDir * seg.Start;
                                    XYZ segEnd = barStart + barDir * seg.End;

                                    var curves = new List<Curve>();
                                    if (si > 0)
                                    {
                                        double crankOff = LapSpliceCalculator.GetCrankOffset(vertDef.BarDiameter);
                                        double crankRun = LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter);
                                        double lapLen = GetLapSpliceLength(vertDef.BarDiameter, request);
                                        double straightLap = lapLen + crankRun;

                                        // Crank inward (towards center of wall)
                                        XYZ inwardDir = host.WAxis;
                                        if (vOff > 0) inwardDir = -host.WAxis; 
                                        else if (vOff < 0) inwardDir = host.WAxis;
                                        else inwardDir = host.WAxis; // if center, just pick one direction

                                        XYZ ptA = segStart + inwardDir * crankOff;
                                        XYZ ptB = ptA + barDir * straightLap;
                                        XYZ ptC = segStart + barDir * (straightLap + crankRun);

                                        curves.Add(Line.CreateBound(ptA, ptB));
                                        curves.Add(Line.CreateBound(ptB, ptC));
                                        curves.Add(Line.CreateBound(ptC, segEnd));
                                    }
                                    else
                                    {
                                        curves.Add(Line.CreateBound(segStart, segEnd));
                                    }

                                    definitions.Add(new RebarDefinition
                                    {
                                        Curves = curves,
                                        Style = vertDef.Style,
                                        BarTypeName = vertDef.BarTypeName,
                                        BarDiameter = vertDef.BarDiameter,
                                        Spacing = vertDef.Spacing,
                                        ArrayLength = vertDef.ArrayLength,
                                        Normal = vertDef.Normal,
                                        HookStartName = (si == 0) ? vertDef.HookStartName : null,
                                        HookEndName = (si == segments.Count - 1) ? vertDef.HookEndName : null,
                                        HookStartOrientation = vertDef.HookStartOrientation,
                                        HookEndOrientation = vertDef.HookEndOrientation,
                                        Label = "Vertical Bar (lapped)",
                                        FixedCount = vertDef.FixedCount,
                                        DistributionWidth = vertDef.DistributionWidth,
                                        ArrayDirection = vertDef.ArrayDirection
                                    });
                                }
                            }
                        }
                        else
                        {
                            definitions.Add(vertDef);
                        }
                    }
                }

                    // 2. Horizontal Bars
                    if (request.Layers.Count > 0 && hDia > 0)
                    {
                        var layer = request.Layers[0]; // Use first layer template for params

                        // --- NEW: Skip Slabs Implementation ---
                        // Get Z-ranges of the wall that are not occupied by floor slabs
                        var zones = GetValidHorizontalZones(wall, host.SolidZMin + layer.BottomOffset, host.SolidZMax - layer.TopOffset, request.SkipSlabIntersections);

                        foreach (var zone in zones)
                        {
                            // Calculate offsets relative to the full wall for the generator
                            double zoneBotOffset = zone.Min - host.SolidZMin;
                            double zoneTopOffset = host.SolidZMax - zone.Max;

                            // If hooks are specified at an end, don't trim horizontal bars there
                            double hStartCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookStartName) ? startTrim : 0);
                            double hEndCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookEndName) ? endTrim : 0);

                            var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                                host, layer.HorizontalBarTypeName, hDia,
                                layer.HorizontalSpacing, zoneTopOffset, zoneBotOffset,
                                hStartCover,
                                hEndCover,
                                hOff,
                                layer.HookStartName, layer.HookEndName,
                                layer.HookStartOutward, layer.HookEndOutward);

                            if (horizDef != null)
                            {
                                AddHorizontalDefinition(definitions, horizDef, host, request, hOff);
                            }
                        }
                    }

                // 3. Starter Bars
                if (request.EnableStarterBars && vDia > 0)
                {
                    string sType = !string.IsNullOrEmpty(request.StarterBarTypeName) ? request.StarterBarTypeName : request.TransverseBarTypeName;
                    double sDia = GetBarDiameter(sType);

                    double devLen = request.StarterDevLength;
                    if (devLen <= 0)
                    {
                        // Auto-calculate development length if 0
                        devLen = LapSpliceCalculator.CalculateTensionLapLength(
                            sDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
                    }

                    // --- NEW: Check for Footing below ---
                    double footingExt = DetermineStarterExtensionIntoFooting(wall, host);
                    if (footingExt > 0)
                    {
                        devLen = footingExt;
                    }
                    // ------------------------------------

                    var starterDef = WallLayoutGenerator.CreateVerticalBars(
                        host, sType, sDia,
                        request.TransverseSpacing,
                        request.TransverseStartOffset + startTrim,
                        request.TransverseEndOffset + endTrim,
                        host.CoverOther, host.CoverOther, 
                        0, 0,
                        vOff,
                        request.StarterHookEndName, null, 
                        request.TransverseHookStartOut, request.TransverseHookEndOut);

                    if (starterDef != null && starterDef.Curves.Count > 0)
                    {
                        var starterCurves = new List<Curve>();
                        if (starterDef.Curves[0] is Line line)
                        {
                            XYZ origStart = line.GetEndPoint(0);
                            XYZ origEnd = line.GetEndPoint(1);
                            XYZ barDir = (origEnd - origStart).Normalize();

                            // Lap into wall above must comply with design code
                            double starterLap = GetLapSpliceLength(sDia, request);
                            // Take the larger of code-calculated lap and user-specified splice length
                            starterLap = Math.Max(starterLap, request.VerticalContinuousSpliceLength);

                            XYZ newStart = origStart - barDir * devLen;
                            XYZ newEnd = origStart + barDir * starterLap;
                            
                            starterCurves.Add(Line.CreateBound(newStart, newEnd));
                        }
                        starterDef.Curves = starterCurves;
                        starterDef.Label = "Starter Bar";
                        starterDef.Comment = "Starter Bar";
                        definitions.Add(starterDef);
                    }
                }
            }

            var ids = _creationService.PlaceRebar(wall, definitions);
            return ids.Count > 0;
        }

        private bool ProcessWallStack(List<Wall> stack, RebarRequest request)
        {
            if (stack == null || stack.Count == 0) return false;

            bool anySuccess = false;

            for (int i = 0; i < stack.Count; i++)
            {
                var wall = stack[i];
                HostGeometry host = WallGeometryModule.Read(_doc, wall);
                if (host.Length <= 0 || host.Width <= 0 || host.Height <= 0) continue;

                bool isBottom = (i == 0);
                bool isTop = (i == stack.Count - 1);

                if (request.RemoveExisting)
                    _creationService.DeleteExistingRebar(wall);

                // Find trim distances at intersecting wall faces
                // Exclude all walls in the stack so stacked walls (same XY, different Z)
                // are not mistakenly treated as intersecting walls
                var stackIds = new HashSet<ElementId>(stack.Select(w => w.Id));
                var (startTrim, endTrim) = FindWallFaceTrimDistances(wall, stackIds);

                var definitions = new List<RebarDefinition>();

                double vDia = GetBarDiameter(request.TransverseBarTypeName);
                double hDia = 0;
                if (request.Layers.Count > 0)
                    hDia = GetBarDiameter(request.Layers[0].HorizontalBarTypeName);

                double thickness = host.Width;

                // LAYER OFFSET CALCULATIONS
                double dExtH = (thickness / 2.0) - host.CoverExterior - (hDia / 2.0);
                double dExtV = dExtH - (hDia / 2.0) - (vDia / 2.0);
                double dIntH = (thickness / 2.0) - host.CoverInterior - (hDia / 2.0);
                double dIntV = dIntH - (hDia / 2.0) - (vDia / 2.0);

                var layers = new List<(double vOff, double hOff)>();
                string config = request.WallLayerConfig ?? "Centre";
                if (config.Equals("Centre", StringComparison.OrdinalIgnoreCase))
                {
                    layers.Add((0, (vDia / 2.0 + hDia / 2.0)));
                }
                else if (config.Equals("Both Faces", StringComparison.OrdinalIgnoreCase))
                {
                    layers.Add((dExtV, dExtH));
                    layers.Add((-dIntV, -dIntH));
                }
                else if (config.Equals("External Face", StringComparison.OrdinalIgnoreCase))
                {
                    layers.Add((dExtV, dExtH));
                }
                else if (config.Equals("Internal Face", StringComparison.OrdinalIgnoreCase))
                {
                    layers.Add((-dIntV, -dIntH));
                }

                // Standard top/bot extensions
                double botExt = request.VerticalBottomExtension;
                double topExt = request.VerticalTopExtension;

                // Code-compliant lap for wall vertical bars (max of code calc and user entry)
                double vertLap = GetLapSpliceLength(vDia, request);
                vertLap = Math.Max(vertLap, request.VerticalContinuousSpliceLength);

                if (!isBottom) botExt = 0;
                if (!isTop) topExt = vertLap;

                bool crankUpper = string.Equals(request.CrankPosition, "Upper Wall", StringComparison.OrdinalIgnoreCase) || 
                                 string.Equals(request.CrankPosition, "Upper Column", StringComparison.OrdinalIgnoreCase);
                bool crankLower = string.Equals(request.CrankPosition, "Lower Wall", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.CrankPosition, "Lower Column", StringComparison.OrdinalIgnoreCase);

                foreach (var (vOff, hOff) in layers)
                {
                    // 1. Vertical Bars
                    if (!string.IsNullOrEmpty(request.TransverseBarTypeName) && vDia > 0)
                    {
                        var vertDef = WallLayoutGenerator.CreateVerticalBars(
                            host, request.TransverseBarTypeName, vDia,
                            request.TransverseSpacing,
                            request.TransverseStartOffset + startTrim,
                            request.TransverseEndOffset + endTrim,
                            isTop ? host.CoverOther : 0,      // topCover — only at top wall
                            isBottom ? host.CoverOther : 0,    // botCover — only at bottom wall
                            topExt, botExt,
                            vOff,
                            isBottom ? request.TransverseHookStartName : null, 
                            isTop ? request.TransverseHookEndName : null,
                            request.TransverseHookStartOut, request.TransverseHookEndOut);

                        if (vertDef != null)
                        {
                            if (!isBottom && crankUpper)
                            {
                                var origLine = vertDef.Curves[0] as Line;
                                if (origLine != null)
                                {
                                    XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                    XYZ barStart = origLine.GetEndPoint(0);
                                    XYZ barEnd = origLine.GetEndPoint(1);

                                    double crankOff = LapSpliceCalculator.GetCrankOffset(vertDef.BarDiameter);
                                    double crankRun = LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter);
                                    double straightLap = vertLap + crankRun;

                                    XYZ inwardDir = host.WAxis;
                                    if (vOff > 0) inwardDir = -host.WAxis; 
                                    else if (vOff < 0) inwardDir = host.WAxis;

                                    XYZ ptA = barStart + inwardDir * crankOff;
                                    XYZ ptB = ptA + barDir * straightLap;
                                    XYZ ptC = barStart + barDir * (straightLap + crankRun);

                                    var curves = new List<Curve>
                                    {
                                        Line.CreateBound(ptA, ptB),
                                        Line.CreateBound(ptB, ptC),
                                        Line.CreateBound(ptC, barEnd)
                                    };
                                    vertDef.Curves = curves;
                                    vertDef.Label = "Vertical Bar (Cranked Upper)";
                                }
                                definitions.Add(vertDef);
                            }
                            else if (!isTop && crankLower)
                            {
                                var origLine = vertDef.Curves[0] as Line;
                                if (origLine != null)
                                {
                                    XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                    XYZ barStart = origLine.GetEndPoint(0);
                                    XYZ barEnd = origLine.GetEndPoint(1);

                                    double crankOff = LapSpliceCalculator.GetCrankOffset(vertDef.BarDiameter);
                                    double crankRun = LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter);

                                    XYZ inwardDir = host.WAxis;
                                    if (vOff > 0) inwardDir = -host.WAxis; 
                                    else if (vOff < 0) inwardDir = host.WAxis;

                                    XYZ spliceStart = barEnd - barDir * topExt;
                                    XYZ ptA = spliceStart - barDir * crankRun;
                                    XYZ ptB = spliceStart + inwardDir * crankOff;
                                    XYZ ptC = barEnd + inwardDir * crankOff;

                                    var curves = new List<Curve>
                                    {
                                        Line.CreateBound(barStart, ptA),
                                        Line.CreateBound(ptA, ptB),
                                        Line.CreateBound(ptB, ptC)
                                    };
                                    vertDef.Curves = curves;
                                    vertDef.Label = "Vertical Bar (Cranked Lower)";
                                }
                                definitions.Add(vertDef);
                            }
                            else
                            {
                                definitions.Add(vertDef);
                            }
                        }
                    }

                    // 2. Starter Bars (bottom level only)
                    if (isBottom && request.EnableStarterBars && vDia > 0)
                    {
                        string sType = !string.IsNullOrEmpty(request.StarterBarTypeName) ? request.StarterBarTypeName : request.TransverseBarTypeName;
                        double sDia = GetBarDiameter(sType);

                        double devLen = request.StarterDevLength;
                        if (devLen <= 0)
                        {
                            // Auto-calculate development length if 0
                            devLen = LapSpliceCalculator.CalculateTensionLapLength(
                                sDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
                        }

                        // --- NEW: Check for Footing below ---
                        double footingExt = DetermineStarterExtensionIntoFooting(wall, host);
                        if (footingExt > 0)
                        {
                            devLen = footingExt;
                        }
                        // ------------------------------------

                        var starterDef = WallLayoutGenerator.CreateVerticalBars(
                            host, sType, sDia,
                            request.TransverseSpacing,
                            request.TransverseStartOffset + startTrim,
                            request.TransverseEndOffset + endTrim,
                            host.CoverOther, host.CoverOther, 
                            0, 0,
                            vOff,
                            request.StarterHookEndName, null, // Use Starter Hook from request
                            request.TransverseHookStartOut, request.TransverseHookEndOut);

                        if (starterDef != null && starterDef.Curves.Count > 0)
                        {
                            var starterCurves = new List<Curve>();
                            if (starterDef.Curves[0] is Line line)
                            {
                                XYZ origStart = line.GetEndPoint(0);
                                XYZ origEnd = line.GetEndPoint(1);
                                XYZ barDir = (origEnd - origStart).Normalize();

                                // Lap into wall above must comply with design code
                                double starterLap = GetLapSpliceLength(sDia, request);
                                // Take the larger of code-calculated lap and user-specified splice length
                                starterLap = Math.Max(starterLap, request.VerticalContinuousSpliceLength);

                                XYZ newStart = origStart - barDir * devLen;
                                XYZ newEnd = origStart + barDir * starterLap;
                                
                                starterCurves.Add(Line.CreateBound(newStart, newEnd));
                            }
                            starterDef.Curves = starterCurves;
                            starterDef.Label = "Starter Bar";
                            starterDef.Comment = "Starter Bar";
                            definitions.Add(starterDef);
                        }
                    }

                        // 3. Horizontal Bars
                        if (request.Layers.Count > 0 && hDia > 0)
                        {
                            var layer = request.Layers[0];

                            // --- NEW: Skip Slabs Implementation ---
                            var zones = GetValidHorizontalZones(wall, host.SolidZMin + layer.BottomOffset, host.SolidZMax - layer.TopOffset, request.SkipSlabIntersections);

                            foreach (var zone in zones)
                            {
                                double zoneBotOffset = zone.Min - host.SolidZMin;
                                double zoneTopOffset = host.SolidZMax - zone.Max;

                                // If hooks are specified at an end, don't trim horizontal bars there
                                double hStartCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookStartName) ? startTrim : 0);
                                double hEndCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookEndName) ? endTrim : 0);

                                var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                                    host, layer.HorizontalBarTypeName, hDia,
                                    layer.HorizontalSpacing, zoneTopOffset, zoneBotOffset,
                                    hStartCover,
                                    hEndCover,
                                    hOff,
                                    layer.HookStartName, layer.HookEndName,
                                    layer.HookStartOutward, layer.HookEndOutward);

                                if (horizDef != null)
                                {
                                    AddHorizontalDefinition(definitions, horizDef, host, request, hOff);
                                }
                            }
                        }
                    }

                    var ids = _creationService.PlaceRebar(wall, definitions);
                    if (ids.Count > 0) anySuccess = true;
                }

                return anySuccess;
            }

        private bool ProcessWallCornerL(CornerInfo corner, RebarRequest request)
        {
            double barDia = GetBarDiameter(request.VerticalBarTypeName);
            if (barDia <= 0) return false;

            var definitions = WallCornerLayoutGenerator.CreateLBars(corner, request, barDia);
            if (definitions.Count == 0) return false;

            var ids = _creationService.PlaceRebar(corner.Wall1, definitions);
            return ids.Count > 0;
        }

        private bool ProcessWallCornerU(CornerInfo corner, RebarRequest request)
        {
            double barDia = GetBarDiameter(request.VerticalBarTypeName);
            if (barDia <= 0) return false;

            var definitions = WallCornerLayoutGenerator.CreateUBars(corner, request, barDia);
            if (definitions.Count == 0) return false;

            var ids = _creationService.PlaceRebar(corner.Wall1, definitions);
            return ids.Count > 0;
        }

        private (double StartTrim, double EndTrim) FindWallFaceTrimDistances(Wall wall, HashSet<ElementId> excludeIds = null)
        {
            double startTrim = 0;
            double endTrim = 0;

            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line wallLine)) return (0, 0);

            XYZ wallStart = wallLine.GetEndPoint(0);
            XYZ wallEnd = wallLine.GetEndPoint(1);
            XYZ wallDir = new XYZ(wallEnd.X - wallStart.X, wallEnd.Y - wallStart.Y, 0).Normalize();

            // Collect all structural walls in the document
            // Exclude current wall and any walls in the provided excludeIds set
            var allWalls = new FilteredElementCollector(_doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.Id != wall.Id && (excludeIds == null || !excludeIds.Contains(w.Id)))
                .Where(w => {
                    var wLoc = w.Location as LocationCurve;
                    return wLoc != null && wLoc.Curve is Line;
                })
                .ToList();

            double tolerance = 1.0; // 1 ft (~305mm)

            foreach (var other in allWalls)
            {
                LocationCurve otherLoc = other.Location as LocationCurve;
                Line otherLine = otherLoc.Curve as Line;
                double otherThickness = other.Width;

                XYZ oStart = otherLine.GetEndPoint(0);
                XYZ oEnd = otherLine.GetEndPoint(1);

                // Skip parallel walls (stacked above/below) — only trim for intersecting walls
                XYZ otherDir = new XYZ(oEnd.X - oStart.X, oEnd.Y - oStart.Y, 0).Normalize();
                double dot = Math.Abs(wallDir.DotProduct(otherDir));
                if (dot > 0.9) continue; // Parallel wall — not an intersection

                // Check if "other" wall meets this wall's START end
                double dStart0 = Dist2D(wallStart, oStart);
                double dStart1 = Dist2D(wallStart, oEnd);
                if (dStart0 < tolerance || dStart1 < tolerance)
                {
                    // The intersecting wall's face is half its thickness from the intersection point
                    double trim = otherThickness / 2.0;
                    if (trim > startTrim) startTrim = trim;
                }

                // Check if "other" wall meets this wall's END end
                double dEnd0 = Dist2D(wallEnd, oStart);
                double dEnd1 = Dist2D(wallEnd, oEnd);
                if (dEnd0 < tolerance || dEnd1 < tolerance)
                {
                    double trim = otherThickness / 2.0;
                    if (trim > endTrim) endTrim = trim;
                }
            }

            return (startTrim, endTrim);
        }

        private double GetBarDiameter(string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;
            var barType = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(t => string.Equals(t.Name, barTypeName, StringComparison.OrdinalIgnoreCase));

            return barType?.BarModelDiameter ?? 0;
        }

        private double GetLapSpliceLength(double barDia, RebarRequest request, BarPosition position = BarPosition.Other)
        {
            if (string.Equals(request.LapSpliceMode, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                return request.LapSpliceLength;
            }
            return LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, request.Grade, SteelGrade.Grade500E, position);
        }

        private static double Dist2D(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private double DetermineStarterExtensionIntoFooting(Wall wall, HostGeometry host)
        {
            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox == null) return -1;

            // Use the midpoint of the wall base
            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line wallLine)) return -1;

            XYZ midPt = wallLine.Evaluate(0.5, true);
            XYZ pointBelow = new XYZ(midPt.X, midPt.Y, host.SolidZMin - 0.1);

            var foundations = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var f in foundations)
            {
                BoundingBoxXYZ fBox = f.get_BoundingBox(null);
                if (fBox == null) continue;

                // Check if wall base center is within foundation footprint
                bool inFootprint = pointBelow.X >= fBox.Min.X && pointBelow.X <= fBox.Max.X &&
                                   pointBelow.Y >= fBox.Min.Y && pointBelow.Y <= fBox.Max.Y;
                
                // Wall bottom should be sits on foundation top (allow 0.2ft/60mm gap)
                bool onTop = Math.Abs(host.SolidZMin - fBox.Max.Z) < 0.2;

                if (inFootprint && onTop)
                {
                    double coverBot = GeometryUtils.GetCoverDistance(_doc, f, BuiltInParameter.CLEAR_COVER_BOTTOM);
                    
                    // Extension from wall base to reaching bottom cover of footing
                    double extension = host.SolidZMin - (fBox.Min.Z + coverBot);
                    
                    if (extension > 0) return extension;
                }
            }
            return -1;
        }

        private void AddHorizontalDefinition(List<RebarDefinition> definitions, RebarDefinition horizDef, HostGeometry host, RebarRequest request, double hOff)
        {
            double barLen = host.Length - 2 * host.CoverOther;
            if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
            {
                var segments = LapSpliceCalculator.SplitBarForLap(
                    barLen, horizDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(horizDef.BarDiameter),
                    BarPosition.Top, GetLapSpliceLength(horizDef.BarDiameter, request, BarPosition.Top));

                if (segments.Count <= 1)
                {
                    definitions.Add(horizDef);
                }
                else
                {
                    var origLine = horizDef.Curves[0] as Line;
                    if (origLine == null) { definitions.Add(horizDef); return; }
                    XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                    XYZ barStart = origLine.GetEndPoint(0);

                    for (int si = 0; si < segments.Count; si++)
                    {
                        var seg = segments[si];
                        XYZ segStart = barStart + barDir * seg.Start;
                        XYZ segEnd = barStart + barDir * seg.End;

                        var curves = new List<Curve>();
                        if (si > 0)
                        {
                            double crankOff = LapSpliceCalculator.GetCrankOffset(horizDef.BarDiameter);
                            double crankRun = LapSpliceCalculator.GetCrankRun(horizDef.BarDiameter);
                            double lapLen = GetLapSpliceLength(horizDef.BarDiameter, request, BarPosition.Top);
                            double straightLap = lapLen; // Aligned with the new rule in Checkpoint 3

                            XYZ inwardDir = host.WAxis;
                            if (hOff > 0) inwardDir = -host.WAxis;
                            else if (hOff < 0) inwardDir = host.WAxis;
                            else inwardDir = host.WAxis;

                            XYZ ptA = segStart + inwardDir * crankOff;
                            XYZ ptB = ptA + barDir * straightLap;
                            XYZ ptC = segStart + barDir * (straightLap + crankRun);

                            curves.Add(Line.CreateBound(ptA, ptB));
                            curves.Add(Line.CreateBound(ptB, ptC));
                            curves.Add(Line.CreateBound(ptC, segEnd));
                        }
                        else
                        {
                            curves.Add(Line.CreateBound(segStart, segEnd));
                        }

                        definitions.Add(new RebarDefinition
                        {
                            Curves = curves,
                            Style = horizDef.Style,
                            BarTypeName = horizDef.BarTypeName,
                            BarDiameter = horizDef.BarDiameter,
                            Spacing = horizDef.Spacing,
                            ArrayLength = horizDef.ArrayLength,
                            Normal = horizDef.Normal,
                            HookStartName = (si == 0) ? horizDef.HookStartName : null,
                            HookEndName = (si == segments.Count - 1) ? horizDef.HookEndName : null,
                            HookStartOrientation = horizDef.HookStartOrientation,
                            HookEndOrientation = horizDef.HookEndOrientation,
                            Label = "Horizontal Bar (lapped)",
                            FixedCount = horizDef.FixedCount,
                            DistributionWidth = horizDef.DistributionWidth,
                            ArrayDirection = horizDef.ArrayDirection
                        });
                    }
                }
            }
            else
            {
                definitions.Add(horizDef);
            }
        }

        private List<(double Min, double Max)> GetIntersectingSlabRanges(Wall wall)
        {
            var ranges = new List<(double Min, double Max)>();
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = wall.get_Geometry(opt);
            Solid wallSolid = null;
            if (geo != null)
            {
                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid s && s.Volume > 0)
                    {
                        wallSolid = s;
                        break;
                    }
                }
            }
            
            // Build collection of potential "slab-like" elements
            var categories = new List<BuiltInCategory> 
            { 
                BuiltInCategory.OST_Floors, 
                BuiltInCategory.OST_StructuralFoundation, // Catch foundation slabs
                BuiltInCategory.OST_StructuralFraming      // Catch thick beams intersecting walls
            };
            var catFilter = new ElementMulticategoryFilter(categories);

            // Filter by bounding box first for performance
            BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);
            if (wallBox == null) return ranges;
            
            var collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(catFilter)
                .WherePasses(new BoundingBoxIntersectsFilter(new Outline(wallBox.Min, wallBox.Max)));

            // If we have a successful solid, use it with a slight expansion to catch touching elements
            if (wallSolid != null)
            {
                try 
                {
                    // Expand solid by ~6mm to capture elements technically touching but not penetrating
                    Solid expandedWall = BooleanOperationsUtils.ExecuteBooleanOperation(
                        wallSolid, null, BooleanOperationsType.Union); // Clone first

                    collector = collector.WherePasses(new ElementIntersectsSolidFilter(wallSolid)); // Use original for now standard
                }
                catch { /* Fallback to collector state as is (BB filter already applied) */ }
            }

            foreach (var elem in collector)
            {
                // Skip the wall itself if somehow caught (shouldn't be in these categories)
                if (elem.Id == wall.Id) continue;

                BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
                if (bbox != null)
                {
                    // Ensure the element actually overlaps vertically with our wall's core height
                    if (bbox.Max.Z < wallBox.Min.Z || bbox.Min.Z > wallBox.Max.Z) continue;

                    ranges.Add((bbox.Min.Z, bbox.Max.Z));
                }
            }
            return MergeRanges(ranges.OrderBy(r => r.Min).ToList());
        }

        private List<(double Min, double Max)> MergeRanges(List<(double Min, double Max)> sorted)
        {
            if (sorted.Count <= 1) return sorted;
            var merged = new List<(double Min, double Max)>();
            var current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Min <= current.Max + 0.05) // 15mm tolerance
                {
                    current = (current.Min, Math.Max(current.Max, sorted[i].Max));
                }
                else
                {
                    merged.Add(current);
                    current = sorted[i];
                }
            }
            merged.Add(current);
            return merged;
        }

        private List<(double Min, double Max)> GetValidHorizontalZones(Wall wall, double zMin, double zMax, bool skipSlabs)
        {
            var results = new List<(double Min, double Max)>();
            if (!skipSlabs)
            {
                results.Add((zMin, zMax));
                return results;
            }

            var slabRanges = GetIntersectingSlabRanges(wall);
            double currentZ = zMin;
            
            // Clearance buffer to keep rebar away from slab faces (approx 30mm)
            double clearance = 0.1;

            foreach (var slab in slabRanges)
            {
                if (slab.Min >= zMax) break;
                if (slab.Max <= currentZ) continue;

                if (slab.Min > currentZ + 0.1) // Min threshold for a zone
                {
                    double zoneMax = slab.Min - clearance;
                    if (zoneMax > currentZ + 0.05) // Ensure zone still valid after clearance
                    {
                        results.Add((currentZ, zoneMax));
                    }
                }
                currentZ = Math.Max(currentZ, slab.Max + clearance);
            }

            if (currentZ < zMax - 0.1)
            {
                results.Add((currentZ, zMax));
            }

            if (results.Count == 0 && slabRanges.Count == 0)
            {
                results.Add((zMin, zMax));
            }
            return results;
        }
    }
}
