using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Calculators;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    /// <summary>
    /// Main orchestrator for rebar generation.
    /// Dual-mode: flat Z for horizontal beams, LCS for slanted beams.
    /// </summary>
    public class RebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public RebarEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public (int Processed, int Total) GenerateBeamRebar(
            List<FamilyInstance> beams, RebarRequest request)
        {
            return GenerateRebarInternal(beams, request, "Generate Beam Rebar");
        }

        public (int Processed, int Total) GenerateWallRebar(
            List<Wall> walls, RebarRequest request)
        {
            return GenerateRebarInternal(walls, request, "Generate Wall Rebar");
        }

        public (int Processed, int Total) GenerateWallStackRebar(
            List<Wall> stack, RebarRequest request)
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
                    System.Diagnostics.Debug.WriteLine($"RebarEngine: Wall stack failed: {ex.Message}");
                }

                t.Commit();
            }

            return (processed, stack.Count);
        }

        public (int Processed, int Total) GenerateColumnRebar(
            List<FamilyInstance> columns, RebarRequest request)
        {
            return GenerateRebarInternal(columns, request, "Generate Column Rebar");
        }

        public (int Processed, int Total) GenerateColumnStackRebar(
            List<FamilyInstance> stack, RebarRequest request)
        {
            int processed = 0;

            using (Transaction t = new Transaction(_doc, "Generate Multi-Level Column Rebar"))
            {
                t.Start();

                try
                {
                    processed = ProcessColumnStack(stack, request) ? stack.Count : 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RebarEngine: Column stack failed: {ex.Message}");
                }

                t.Commit();
            }

            return (processed, stack.Count);
        }

        public (int Processed, int Total) GenerateStripFootingRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Strip Footing Rebar");
        }

        public (int Processed, int Total) GenerateFootingPadRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Footing Pad Rebar");
        }


        private (int Processed, int Total) GenerateRebarInternal<T>(
            List<T> elements, RebarRequest request, string transactionName) where T : Element
        {
            int processed = 0;

            using (Transaction t = new Transaction(_doc, transactionName))
            {
                t.Start();

                foreach (var element in elements)
                {
                    try
                    {
                        if (request.RemoveExisting)
                            _creationService.DeleteExistingRebar(element);

                        bool success = false;
                        if (element is FamilyInstance fi && request.HostType == ElementHostType.Beam)
                            success = ProcessBeam(fi, request);
                        else if (element is Wall wall && request.HostType == ElementHostType.Wall)
                            success = ProcessWall(wall, request);
                        else if (element is FamilyInstance col && request.HostType == ElementHostType.Column)
                            success = ProcessColumn(col, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.StripFooting)
                            success = ProcessStripFooting(element, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.FootingPad)
                            success = ProcessFootingPad(element, request);

                        if (success) processed++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"RebarEngine: {element.Id} failed: {ex.Message}");
                    }
                }

                t.Commit();
            }

            return (processed, elements.Count);
        }

        private bool ProcessBeam(FamilyInstance beam, RebarRequest request)
        {
            HostGeometry host = BeamGeometryModule.Read(_doc, beam);
            if (host.Length <= 0 || host.Width <= 0 || host.Height <= 0) return false;

            var definitions = new List<RebarDefinition>();

            // Get tranverse bar diameter
            double transDia = 0;
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                transDia = GetBarDiameter(request.TransverseBarTypeName);

            double minLayerGap = UnitConversion.MmToFeet(20);

            if (host.IsSlanted)
                ProcessSlantedBeam(host, beam, request, transDia, minLayerGap, definitions);
            else
                ProcessHorizontalBeam(host, beam, request, transDia, minLayerGap, definitions);

            var ids = _creationService.PlaceRebar(beam, definitions);
            return ids.Count > 0;
        }

        // ==============================================================
        //  HORIZONTAL BEAM — absolute Z from solid geometry bounds
        // ==============================================================
        private void ProcessHorizontalBeam(HostGeometry host, FamilyInstance beam,
            RebarRequest request, double transDia, double minLayerGap,
            List<RebarDefinition> definitions)
        {
            // Z bounds from solid geometry
            double zMin = host.SolidZMin;
            double zMax = host.SolidZMax;

            // Validate with parameter height
            double paramHeight = GetParamHeight(beam);
            if (paramHeight > 0)
            {
                double solidHeight = zMax - zMin;
                if (Math.Abs(solidHeight - paramHeight) > 0.01)
                {
                    double zMid = (zMax + zMin) / 2.0;
                    zMin = zMid - paramHeight / 2.0;
                    zMax = zMid + paramHeight / 2.0;
                }
            }

            // Stirrups
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                if (request.EnableZoneSpacing)
                {
                    // Zone-based stirrups (end zone densification)
                    var zones = ZoneSpacingCalculator.CalculateBeamZones(
                        host.Length, host.Height, request.TransverseSpacing,
                        request.TransverseStartOffset, request.DesignCode);
                    var zonedDefs = StirrupLayoutGenerator.CreateZonedBeamStirrups(
                        host, request.TransverseBarTypeName, transDia,
                        zones, request.TransverseHookStartName, request.TransverseHookEndName,
                        zMin, zMax);
                    definitions.AddRange(zonedDefs);
                }
                else
                {
                    // Uniform spacing (original behaviour)
                    var stirrupDef = StirrupLayoutGenerator.CreateBeamStirrup(
                        host, request.TransverseBarTypeName, transDia,
                        request.TransverseSpacing, request.TransverseStartOffset,
                        request.TransverseHookStartName, request.TransverseHookEndName,
                        zMin, zMax);
                    definitions.Add(stirrupDef);
                }
            }

            // Top layers
            double topZ = zMax - host.CoverTop - transDia;
            int topLayerIdx = 0;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = topZ - barDia / 2.0;

                // Check if bar needs splitting — stagger laps between layers
                double barLen = host.Length - 2 * host.CoverOther;
var segments = request.EnableLapSplice 
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: true, layerIndex: topLayerIdx)
                    : new List<(double Start, double End)> { (0.0, barLen) };

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    double innerOffset = host.CoverOther + transDia;
                    double distWidthSeg = host.Width - 2 * innerOffset;

                    XYZ s = host.StartPoint + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = host.StartPoint + host.LAxis * (host.CoverOther + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    // Build curves: cranked start for segments after the first
                    var curves = new List<Curve>();
                    if (si > 0 && segments.Count > 1)
                    {
                        // Cranked bar: straight at offset → angled 1:6 → straight at main
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode);
                        double straightLap = lapLen + crankRun; // entire overlap at offset, crank outside

                        XYZ crankDir = -host.HAxis; // Top bars offset downward (into beam)

                        // ptA: start at offset Z (lowered)
                        XYZ ptA = barStart + crankDir * crankOff;
                        // ptB: end of straight at offset, where crank begins
                        XYZ ptB = ptA + host.LAxis * straightLap;
                        // ptC: end of angled transition, back at main Z
                        XYZ ptC = barStart + host.LAxis * (straightLap + crankRun);

                        curves.Add(Line.CreateBound(ptA, ptB));    // straight at offset level
                        curves.Add(Line.CreateBound(ptB, ptC));    // angled crank (offset→main)
                        curves.Add(Line.CreateBound(ptC, barEnd)); // straight at main level
                    }
                    else
                    {
                        curves.Add(Line.CreateBound(barStart, barEnd));
                    }

                    var segDef = new RebarDefinition
                    {
                        Curves = curves,
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        Spacing = 0,
                        ArrayLength = 0,
                        ArrayDirection = host.WAxis,
                        FixedCount = count,
                        DistributionWidth = distWidthSeg,
                        Normal = host.WAxis,
                        HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                        HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                        HookStartName = (seg.Start == 0) ? layer.HookStartName : null,
                        HookEndName = (seg.End >= barLen - 0.001) ? layer.HookEndName : null,
                        OverrideHookLength = layer.OverrideHookLength,
                        HookLengthOverride = layer.HookLengthOverride,
                        Label = segments.Count > 1 ? "Top Layer (lapped)" : "Top Layer",
                        Comment = "Top Bar"
                    };
                    definitions.Add(segDef);
                }

                topZ -= (barDia + minLayerGap);
                topLayerIdx++;
            }

            // Bottom layers
            double botZ = zMin + host.CoverBottom + transDia;
            int botLayerIdx = 0;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = botZ + barDia / 2.0;

                // Check if bar needs splitting — stagger laps between layers
                double barLen = host.Length - 2 * host.CoverOther;
var segments = request.EnableLapSplice 
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: false, layerIndex: botLayerIdx)
                    : new List<(double Start, double End)> { (0.0, barLen) };

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    XYZ s = host.StartPoint + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = host.StartPoint + host.LAxis * (host.CoverOther + seg.End);
                    double innerOffset = host.CoverOther + transDia;
                    double distWidthSeg = host.Width - 2 * innerOffset;
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    // Build curves: cranked start for segments after the first
                    var curves = new List<Curve>();
                    if (si > 0 && segments.Count > 1)
                    {
                        // Cranked bar: straight at offset → angled 1:6 → straight at main
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode);
                        double straightLap = lapLen + crankRun; // entire overlap at offset, crank outside

                        XYZ crankDir = host.HAxis; // Bottom bars offset upward (into beam)

                        // ptA: start at offset Z (raised)
                        XYZ ptA = barStart + crankDir * crankOff;
                        // ptB: end of straight at offset, where crank begins
                        XYZ ptB = ptA + host.LAxis * straightLap;
                        // ptC: end of angled transition, back at main Z
                        XYZ ptC = barStart + host.LAxis * (straightLap + crankRun);

                        curves.Add(Line.CreateBound(ptA, ptB));    // straight at offset level
                        curves.Add(Line.CreateBound(ptB, ptC));    // angled crank (offset→main)
                        curves.Add(Line.CreateBound(ptC, barEnd)); // straight at main level
                    }
                    else
                    {
                        curves.Add(Line.CreateBound(barStart, barEnd));
                    }

                    var segDef = new RebarDefinition
                    {
                        Curves = curves,
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        Spacing = 0,
                        ArrayLength = 0,
                        ArrayDirection = host.WAxis,
                        FixedCount = count,
                        DistributionWidth = distWidthSeg,
                        Normal = host.WAxis,
                        HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                        HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                        HookStartName = (seg.Start == 0) ? layer.HookStartName : null,
                        HookEndName = (seg.End >= barLen - 0.001) ? layer.HookEndName : null,
                        OverrideHookLength = layer.OverrideHookLength,
                        HookLengthOverride = layer.HookLengthOverride,
                        Label = segments.Count > 1 ? "Bottom Layer (lapped)" : "Bottom Layer",
                        Comment = "Btm Bar"
                    };
                    definitions.Add(segDef);
                }

                botZ += (barDia + minLayerGap);
                botLayerIdx++;
            }

            // Side bars (skin reinforcement)
            if (request.EnableSideRebar && request.SideRebarRows > 0 && !string.IsNullOrEmpty(request.SideRebarTypeName))
            {
                double sideDia = GetBarDiameter(request.SideRebarTypeName);
                int rows = request.SideRebarRows;

                // Evenly space rows between top and bottom rebar zones
                double sideZTop = zMax - host.CoverTop - transDia - sideDia;
                double sideZBot = zMin + host.CoverBottom + transDia + sideDia;
                double availableHeight = sideZTop - sideZBot;
                double rowSpacing = availableHeight / (rows + 1);

                double barLen = host.Length - 2 * host.CoverOther;

                // Side bars use generic stock-length splitting only (no code-based lap zones)
                var segments = (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                    ? LapSpliceCalculator.SplitBarForLap(barLen, sideDia, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(sideDia))
                    : new List<(double Start, double End)> { (0.0, barLen) };

                // Distribution width: distance between the two side bar positions
                double innerOffset = host.CoverOther + transDia;
                double distWidth = host.Width - 2 * innerOffset;

                for (int row = 1; row <= rows; row++)
                {
                    double z = sideZBot + rowSpacing * row;

                    for (int si = 0; si < segments.Count; si++)
                    {
                        var seg = segments[si];
                        XYZ s = host.StartPoint + host.LAxis * (host.CoverOther + seg.Start);
                        XYZ e = host.StartPoint + host.LAxis * (host.CoverOther + seg.End);

                        // Position bar at near side — FixedCount=2 distributes to both faces
                        XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidth / 2.0);
                        XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidth / 2.0);

                        var curves = new List<Curve>();

                        if (si > 0 && segments.Count > 1)
                        {
                            double crankOff = LapSpliceCalculator.GetCrankOffset(sideDia);
                            double crankRun = LapSpliceCalculator.GetCrankRun(sideDia);
                            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(sideDia, request.DesignCode);
                            double straightLap = lapLen + crankRun;

                            XYZ crankDir = host.HAxis; // Offset upward for side bars
                            XYZ ptA = barStart + crankDir * crankOff;
                            XYZ ptB = ptA + host.LAxis * straightLap;
                            XYZ ptC = barStart + host.LAxis * (straightLap + crankRun);
                            curves.Add(Line.CreateBound(ptA, ptB));
                            curves.Add(Line.CreateBound(ptB, ptC));
                            curves.Add(Line.CreateBound(ptC, barEnd));
                        }
                        else
                        {
                            curves.Add(Line.CreateBound(barStart, barEnd));
                        }

                        definitions.Add(new RebarDefinition
                        {
                            Curves = curves,
                            Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                            BarTypeName = request.SideRebarTypeName,
                            BarDiameter = sideDia,
                            Spacing = 0,
                            ArrayLength = 0,
                            ArrayDirection = host.WAxis,
                            FixedCount = 2,
                            DistributionWidth = distWidth,
                            Normal = host.WAxis,
                            Label = $"Side Bar R{row}",
                            Comment = "Side Bar"
                        });
                    }
                }
            }
        }

        // ==============================================================
        //  SLANTED BEAM — LCS positioning, rebar follows the slope
        // ==============================================================
        private void ProcessSlantedBeam(HostGeometry host, FamilyInstance beam,
            RebarRequest request, double transDia, double minLayerGap,
            List<RebarDefinition> definitions)
        {
            // Stirrups — origin at startPt along slope, loop in WAxis × HAxis plane
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                if (request.EnableZoneSpacing)
                {
                    // Zone-based stirrups for slanted beams
                    var zones = ZoneSpacingCalculator.CalculateBeamZones(
                        host.Length, host.Height, request.TransverseSpacing,
                        request.TransverseStartOffset, request.DesignCode);
                    var zonedDefs = StirrupLayoutGenerator.CreateZonedBeamStirrups(
                        host, request.TransverseBarTypeName, transDia,
                        zones, request.TransverseHookStartName, request.TransverseHookEndName,
                        0, 0); // zMin/zMax ignored for slanted (uses LCS internally)
                    definitions.AddRange(zonedDefs);
                }
                else
                {
                    // Uniform spacing (original behaviour)
                    var stirrupDef = StirrupLayoutGenerator.CreateBeamStirrup(
                        host, request.TransverseBarTypeName, transDia,
                        request.TransverseSpacing, request.TransverseStartOffset,
                        request.TransverseHookStartName, request.TransverseHookEndName,
                        0, 0); // zMin/zMax ignored for slanted (uses LCS internally)
                    definitions.Add(stirrupDef);
                }
            }

            // Top layers
            double topOffset = host.Height / 2.0 - host.CoverTop - transDia;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double offset = topOffset - barDia / 2.0;
                var def = ParallelLayoutGenerator.CreateLayerLCS(
                    host, layer.VerticalBarTypeName, barDia,
                    count, offset, transDia, true,
                    layer.HookStartName, layer.HookEndName, "Top Layer",
                    layer.OverrideHookLength, layer.HookLengthOverride, "Top Bar");

                if (def != null) definitions.Add(def);
                topOffset -= (barDia + minLayerGap);
            }

            // Bottom layers
            double botOffset = -(host.Height / 2.0 - host.CoverBottom - transDia);
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double offset = botOffset + barDia / 2.0;
                var def = ParallelLayoutGenerator.CreateLayerLCS(
                    host, layer.VerticalBarTypeName, barDia,
                    count, offset, transDia, false,
                    layer.HookStartName, layer.HookEndName, "Bottom Layer",
                    layer.OverrideHookLength, layer.HookLengthOverride, "Btm Bar");

                if (def != null) definitions.Add(def);
                botOffset += (barDia + minLayerGap);
            }
        }

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
                double avgCover = (host.CoverExterior + host.CoverInterior) / 2.0;
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
                                barLen, vertDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter), BarPosition.Other);

                            if (segments.Count <= 1)
                            {
                                definitions.Add(vertDef);
                            }
                            else
                            {
                                var origLine = vertDef.Curves[0] as Line;
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
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(vertDef.BarDiameter, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
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
                                        Label = "Vertical Bar (lapped)"
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
                    var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                        host, layer.HorizontalBarTypeName, hDia,
                        layer.HorizontalSpacing, layer.TopOffset, layer.BottomOffset,
                        host.CoverOther + startTrim,
                        host.CoverOther + endTrim,
                        hOff,
                        layer.HookStartName, layer.HookEndName,
                        layer.HookStartOutward, layer.HookEndOutward);

                    if (horizDef != null)
                    {
                        double barLen = host.Length - 2 * host.CoverOther;
                        
                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                        {
                            var segments = LapSpliceCalculator.SplitBarForLap(
                                barLen, horizDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(horizDef.BarDiameter), BarPosition.Top);

                            if (segments.Count <= 1)
                            {
                                definitions.Add(horizDef);
                            }
                            else
                            {
                                var origLine = horizDef.Curves[0] as Line;
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
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(horizDef.BarDiameter, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Top);
                                        double straightLap = lapLen + crankRun;

                                        // Crank inward (towards center of wall)
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
                                        Label = "Horizontal Bar (lapped)"
                                    });
                                }
                            }
                        }
                        else
                        {
                            definitions.Add(horizDef);
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
                            double starterLap = LapSpliceCalculator.CalculateTensionLapLength(
                                sDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
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

        // ==============================================================
        //  MULTI-LEVEL WALL STACK — splice bars across levels
        // ==============================================================
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
                double codeLap = LapSpliceCalculator.CalculateTensionLapLength(
                    vDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
                double vertLap = Math.Max(codeLap, request.VerticalContinuousSpliceLength);

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
                                double starterLap = LapSpliceCalculator.CalculateTensionLapLength(
                                    sDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
                                // Take the larger of code-calculated lap and user-specified splice length
                                starterLap = Math.Max(starterLap, request.VerticalContinuousSpliceLength);

                                XYZ newStart = origStart - barDir * devLen;
                                XYZ newEnd = origStart + barDir * starterLap;
                                
                                starterCurves.Add(Line.CreateBound(newStart, newEnd));
                            }
                            starterDef.Curves = starterCurves;
                            starterDef.Label = "Starter Bar";
                            definitions.Add(starterDef);
                        }
                    }

                    // 3. Horizontal Bars
                    if (request.Layers.Count > 0 && hDia > 0)
                    {
                        var layer = request.Layers[0];
                        var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                            host, layer.HorizontalBarTypeName, hDia,
                            layer.HorizontalSpacing, layer.TopOffset, layer.BottomOffset,
                            host.CoverOther + startTrim,
                            host.CoverOther + endTrim,
                            hOff,
                            layer.HookStartName, layer.HookEndName,
                            layer.HookStartOutward, layer.HookEndOutward);

                        if (horizDef != null)
                        {
                            double barLen = host.Length - 2 * host.CoverOther;
                            if (barLen > LapSpliceCalculator.MaxStockLengthFt)
                            {
                                var segments = LapSpliceCalculator.SplitBarForLap(
                                    barLen, horizDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(horizDef.BarDiameter), BarPosition.Top);
                                
                                if (segments.Count <= 1)
                                {
                                    definitions.Add(horizDef);
                                }
                                else
                                {
                                    var origLine = horizDef.Curves[0] as Line;
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
                                            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(horizDef.BarDiameter, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Top);
                                            double straightLap = lapLen + crankRun;

                                            XYZ inwardDir = host.WAxis;
                                            if (hOff > 0) inwardDir = -host.WAxis; 
                                            else if (hOff < 0) inwardDir = host.WAxis;
                                            
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
                                            Label = "Horizontal Bar (lapped)"
                                        });
                                    }
                                }
                            }
                            else
                            {
                                definitions.Add(horizDef);
                            }
                        }
                    }
                }

                var ids = _creationService.PlaceRebar(wall, definitions);
                if (ids.Count > 0) anySuccess = true;
            }

            return anySuccess;
        }

        private bool ProcessColumn(FamilyInstance column, RebarRequest request)
        {
            HostGeometry? hostOpt = ColumnGeometryModule.Read(_doc, column);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            // 1. Ties (Transverse)
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                double tDia = GetBarDiameter(request.TransverseBarTypeName);

                if (request.EnableZoneSpacing)
                {
                    // Zone-based ties (confinement end zones)
                    double maxDim = Math.Max(host.Width, host.Height);
                    double mainBarDia = 0;
                    if (!string.IsNullOrEmpty(request.VerticalBarTypeNameX))
                        mainBarDia = Math.Max(mainBarDia, GetBarDiameter(request.VerticalBarTypeNameX));
                    if (!string.IsNullOrEmpty(request.VerticalBarTypeNameY))
                        mainBarDia = Math.Max(mainBarDia, GetBarDiameter(request.VerticalBarTypeNameY));

                    double clearHeight = host.Length - request.TransverseStartOffset - request.TransverseEndOffset;
                    var zones = ZoneSpacingCalculator.CalculateColumnZones(
                        clearHeight, maxDim, mainBarDia > 0 ? mainBarDia : tDia, tDia, request.DesignCode);
                    var zonedDefs = ColumnLayoutGenerator.CreateZonedColumnTies(
                        host, request.TransverseBarTypeName, tDia,
                        request.TransverseStartOffset,
                        zones, request.TransverseHookStartName, request.TransverseHookEndName);
                    definitions.AddRange(zonedDefs);
                }
                else
                {
                    var tieDef = ColumnLayoutGenerator.CreateColumnTie(
                        host, request.TransverseBarTypeName, tDia,
                        request.TransverseSpacing, request.TransverseStartOffset, request.TransverseEndOffset,
                        request.TransverseHookStartName, request.TransverseHookEndName);
                    if (tieDef != null) definitions.Add(tieDef);
                }
            }

            // 2. Vertical Main Bars
            if (!string.IsNullOrEmpty(request.VerticalBarTypeNameX) || !string.IsNullOrEmpty(request.VerticalBarTypeNameY))
            {
                double vDiaX = GetBarDiameter(request.VerticalBarTypeNameX);
                double vDiaY = GetBarDiameter(request.VerticalBarTypeNameY);

                if (vDiaX > 0 || vDiaY > 0)
                {
                    double tDia = 0.0328; // fallback 10mm
                    if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                        tDia = GetBarDiameter(request.TransverseBarTypeName);

                    // For Column, we use the first layer in the list to store Hook settings
                    var layerTempl = request.Layers.FirstOrDefault() ?? new RebarLayerConfig();

                    var vertDefs = ColumnLayoutGenerator.CreateColumnVerticals(
                        host, 
                        request.VerticalBarTypeNameX, vDiaX,
                        request.VerticalBarTypeNameY, vDiaY,
                        tDia,
                        request.ColumnCountX, request.ColumnCountY,
                        request.VerticalTopExtension, request.VerticalBottomExtension,
                        layerTempl.HookStartName, layerTempl.HookEndName,
                        layerTempl.HookStartOutward, layerTempl.HookEndOutward);

                    if (vertDefs != null)
                    {
                        // Check if vertical bars need lap splice splitting (column height > 12m)
                        double barLen = host.Length + request.VerticalTopExtension + request.VerticalBottomExtension - 2 * host.CoverExterior;
                        double maxBarDia = Math.Max(vDiaX, vDiaY);

                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt && maxBarDia > 0)
                        {
                            var lappedDefs = new List<RebarDefinition>();
                            foreach (var vDef in vertDefs)
                            {
                                var segments = LapSpliceCalculator.SplitBarForLap(
                                    barLen, vDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(vDef.BarDiameter));

                                if (segments.Count <= 1)
                                {
                                    lappedDefs.Add(vDef);
                                    continue;
                                }

                                // Get bar direction from the original curve
                                var origLine = vDef.Curves[0] as Line;
                                if (origLine == null) { lappedDefs.Add(vDef); continue; }
                                XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                XYZ barStart = origLine.GetEndPoint(0);

                                for (int si = 0; si < segments.Count; si++)
                                {
                                    var seg = segments[si];
                                    XYZ segStart = barStart + barDir * seg.Start;
                                    XYZ segEnd = barStart + barDir * seg.End;

                                    // Build curves: cranked start for segments after the first
                                    var curves = new List<Curve>();
                                    if (si > 0)
                                    {
                                        // Cranked bar: straight at offset → angled 1:6 → straight at main
                                        double crankOff = LapSpliceCalculator.GetCrankOffset(vDef.BarDiameter);
                                        double crankRun = LapSpliceCalculator.GetCrankRun(vDef.BarDiameter);
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(vDef.BarDiameter, request.DesignCode);
                                        double straightLap = lapLen + crankRun; // entire overlap at offset, crank outside

                                        XYZ inDir = -vDef.Normal.CrossProduct(barDir).Normalize();
                                        XYZ crankDir = inDir;

                                        // ptA: start at offset position
                                        XYZ ptA = segStart + crankDir * crankOff;
                                        // ptB: end of straight at offset
                                        XYZ ptB = ptA + barDir * straightLap;
                                        // ptC: back at main position
                                        XYZ ptC = segStart + barDir * (straightLap + crankRun);

                                        curves.Add(Line.CreateBound(ptA, ptB));    // straight at offset
                                        curves.Add(Line.CreateBound(ptB, ptC));    // angled crank
                                        curves.Add(Line.CreateBound(ptC, segEnd));  // straight at main
                                    }
                                    else
                                    {
                                        curves.Add(Line.CreateBound(segStart, segEnd));
                                    }

                                    var segDef = new RebarDefinition
                                    {
                                        Curves = curves,
                                        Style = vDef.Style,
                                        BarTypeName = vDef.BarTypeName,
                                        BarDiameter = vDef.BarDiameter,
                                        Normal = vDef.Normal,
                                        Spacing = vDef.Spacing,
                                        ArrayLength = vDef.ArrayLength,
                                        ArrayDirection = vDef.ArrayDirection,
                                        FixedCount = vDef.FixedCount,
                                        DistributionWidth = vDef.DistributionWidth,
                                        HookStartName = (si == 0) ? vDef.HookStartName : null,
                                        HookEndName = (si == segments.Count - 1) ? vDef.HookEndName : null,
                                        HookStartOrientation = vDef.HookStartOrientation,
                                        HookEndOrientation = vDef.HookEndOrientation,
                                        Label = "Main Vertical Bar (lapped)"
                                    };
                                    lappedDefs.Add(segDef);
                                }
                            }
                            definitions.AddRange(lappedDefs);
                        }
                        else
                        {
                            definitions.AddRange(vertDefs);
                        }
                    }
                }
            }

            // 3. Starter Bars (single column)
            if (request.EnableStarterBars)
            {
                double vDiaX = GetBarDiameter(request.VerticalBarTypeNameX);
                double vDiaY = GetBarDiameter(request.VerticalBarTypeNameY);
                double maxBarDia = Math.Max(vDiaX, vDiaY);

                if (maxBarDia > 0)
                {
                    double starterLen = request.StarterDevLength > 0
                        ? request.StarterDevLength
                        : ColumnContinuityCalculator.GetStarterBarLength(maxBarDia, request.DesignCode);

                    string starterBarTypeX = request.StarterBarTypeName ?? request.VerticalBarTypeNameX;
                    string starterBarTypeY = request.StarterBarTypeName ?? request.VerticalBarTypeNameY;
                    double starterDiaX = GetBarDiameter(starterBarTypeX);
                    double starterDiaY = GetBarDiameter(starterBarTypeY);

                    double tieDia = 0.0328; // fallback 10mm
                    if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                        tieDia = GetBarDiameter(request.TransverseBarTypeName);

                    double starterTopExt = LapSpliceCalculator.CalculateTensionLapLength(maxBarDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);

                    var starterDefs = CreateStarterBars(
                        host, starterBarTypeX, starterDiaX, starterBarTypeY, starterDiaY,
                        tieDia, request.ColumnCountX, request.ColumnCountY,
                        starterLen, starterTopExt, request.StarterHookEndName);

                    if (starterDefs != null)
                        definitions.AddRange(starterDefs);
                }
            }

            var ids = _creationService.PlaceRebar(column, definitions);
            return ids.Count > 0;
        }

        // ==============================================================
        //  MULTI-LEVEL COLUMN STACK — splice bars across levels
        // ==============================================================
        private bool ProcessColumnStack(List<FamilyInstance> stack, RebarRequest request)
        {
            if (stack == null || stack.Count == 0) return false;

            bool anySuccess = false;

            for (int i = 0; i < stack.Count; i++)
            {
                var column = stack[i];
                HostGeometry? hostOpt = ColumnGeometryModule.Read(_doc, column);
                if (!hostOpt.HasValue) continue;
                HostGeometry host = hostOpt.Value;

                bool isBottom = (i == 0);
                bool isTop = (i == stack.Count - 1);

                if (request.RemoveExisting)
                    _creationService.DeleteExistingRebar(column);

                var definitions = new List<RebarDefinition>();

                // Get bar diameters
                double vDiaX = GetBarDiameter(request.VerticalBarTypeNameX);
                double vDiaY = GetBarDiameter(request.VerticalBarTypeNameY);
                double tDia = 0.0328; // fallback 10mm
                if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                    tDia = GetBarDiameter(request.TransverseBarTypeName);
                double maxBarDia = Math.Max(vDiaX, vDiaY);

                // ── 1. TIES (same as single-column, reuse existing logic) ──
                if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                {
                    if (request.EnableZoneSpacing)
                    {
                        double maxDim = Math.Max(host.Width, host.Height);
                        double mainBarDia = Math.Max(vDiaX, vDiaY);
                        double clearHeight = host.Length - request.TransverseStartOffset - request.TransverseEndOffset;
                        var zones = ZoneSpacingCalculator.CalculateColumnZones(
                            clearHeight, maxDim, mainBarDia > 0 ? mainBarDia : tDia, tDia, request.DesignCode);
                        var zonedDefs = ColumnLayoutGenerator.CreateZonedColumnTies(
                            host, request.TransverseBarTypeName, tDia,
                            request.TransverseStartOffset,
                            zones, request.TransverseHookStartName, request.TransverseHookEndName);
                        definitions.AddRange(zonedDefs);
                    }
                    else
                    {
                        var tieDef = ColumnLayoutGenerator.CreateColumnTie(
                            host, request.TransverseBarTypeName, tDia,
                            request.TransverseSpacing, request.TransverseStartOffset, request.TransverseEndOffset,
                            request.TransverseHookStartName, request.TransverseHookEndName);
                        if (tieDef != null) definitions.Add(tieDef);
                    }
                }

                // ── 2. VERTICAL BARS with multi-level splice logic ──
                bool crankUpper = string.Equals(request.CrankPosition, "Upper Column", StringComparison.OrdinalIgnoreCase);
                bool crankLower = string.Equals(request.CrankPosition, "Lower Column", StringComparison.OrdinalIgnoreCase);
                if (vDiaX > 0 || vDiaY > 0)
                {
                    var layerTempl = request.Layers.FirstOrDefault() ?? new RebarLayerConfig();

                    // Calculate extensions for this level
                    double botExt = request.VerticalBottomExtension;
                    double topExt = request.VerticalTopExtension;

                    // For non-bottom columns: bars start at column base (no extension below)
                    // The column below projects its bars UP into this column for the overlap
                    if (!isBottom)
                    {
                        botExt = 0;
                    }

                    // For non-top columns: bars extend up into upper column by splice length
                    if (!isTop)
                    {
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(maxBarDia, request.DesignCode, ConcreteGrade.C30, SteelGrade.Grade500E, BarPosition.Other);
                        topExt = ColumnContinuityCalculator.GetSpliceStartOffset() + lapLen;
                    }

                    var vertDefs = ColumnLayoutGenerator.CreateColumnVerticals(
                        host,
                        request.VerticalBarTypeNameX, vDiaX,
                        request.VerticalBarTypeNameY, vDiaY,
                        tDia,
                        request.ColumnCountX, request.ColumnCountY,
                        topExt, botExt,
                        isBottom ? layerTempl.HookStartName : null,  // Hook at bottom only on lowest column
                        isTop ? layerTempl.HookEndName : null,       // Hook at top only on highest column
                        layerTempl.HookStartOutward, layerTempl.HookEndOutward);

                    if (vertDefs != null)
                    {
                        // ── CRANK AT BOTTOM OF UPPER COLUMN BARS ──
                        // Shape: offset start → straight lap → 1:6 crank → straight to top
                        if (!isBottom && crankUpper)
                        {
                            var crankedDefs = new List<RebarDefinition>();
                            foreach (var vDef in vertDefs)
                            {
                                var origLine = vDef.Curves[0] as Line;
                                if (origLine == null) { crankedDefs.Add(vDef); continue; }

                                XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                XYZ barStart = origLine.GetEndPoint(0);
                                XYZ barEnd = origLine.GetEndPoint(1);

                                double crankOff = LapSpliceCalculator.GetCrankOffset(vDef.BarDiameter);
                                double crankRun = LapSpliceCalculator.GetCrankRun(vDef.BarDiameter);
                                double lapLen = LapSpliceCalculator.CalculateTensionLapLength(vDef.BarDiameter, request.DesignCode);
                                double straightLap = lapLen + crankRun;

                                XYZ inDir = -vDef.Normal.CrossProduct(barDir).Normalize();

                                XYZ ptA = barStart + inDir * crankOff;
                                XYZ ptB = ptA + barDir * straightLap;
                                XYZ ptC = barStart + barDir * (straightLap + crankRun);

                                var curves = new List<Curve>
                                {
                                    Line.CreateBound(ptA, ptB),
                                    Line.CreateBound(ptB, ptC),
                                    Line.CreateBound(ptC, barEnd)
                                };

                                crankedDefs.Add(new RebarDefinition
                                {
                                    Curves = curves,
                                    Style = vDef.Style,
                                    BarTypeName = vDef.BarTypeName,
                                    BarDiameter = vDef.BarDiameter,
                                    Normal = vDef.Normal,
                                    ArrayDirection = vDef.ArrayDirection,
                                    FixedCount = vDef.FixedCount,
                                    DistributionWidth = vDef.DistributionWidth,
                                    Spacing = vDef.Spacing,
                                    ArrayLength = vDef.ArrayLength,
                                    HookStartName = null,
                                    HookEndName = vDef.HookEndName,
                                    HookStartOrientation = vDef.HookStartOrientation,
                                    HookEndOrientation = vDef.HookEndOrientation,
                                    Label = vDef.Label + " (Cranked)",
                                    Comment = vDef.Comment
                                });
                            }
                            definitions.AddRange(crankedDefs);
                        }
                        // ── CRANK AT TOP OF LOWER COLUMN BARS ──
                        // Shape: straight → 1:6 crank (main → offset) → straight at offset
                        else if (!isTop && crankLower)
                        {
                            var crankedDefs = new List<RebarDefinition>();
                            foreach (var vDef in vertDefs)
                            {
                                var origLine = vDef.Curves[0] as Line;
                                if (origLine == null) { crankedDefs.Add(vDef); continue; }

                                XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                XYZ barStart = origLine.GetEndPoint(0);
                                XYZ barEnd = origLine.GetEndPoint(1);

                                double crankOff = LapSpliceCalculator.GetCrankOffset(vDef.BarDiameter);
                                double crankRun = LapSpliceCalculator.GetCrankRun(vDef.BarDiameter);

                                XYZ inDir = -vDef.Normal.CrossProduct(barDir).Normalize();

                                // Crank near the top of the lower column
                                // spliceStart = the point where the bar exits the column into the upper zone
                                XYZ spliceStart = barEnd - barDir * topExt;
                                // ptA: crank starts crankRun below splice start (main position)
                                XYZ ptA = spliceStart - barDir * crankRun;
                                // ptB: after 1:6 crank, now at offset position (splice start height)
                                XYZ ptB = spliceStart + inDir * crankOff;
                                // ptC: bar end at offset position (into upper column)
                                XYZ ptC = barEnd + inDir * crankOff;

                                var curves = new List<Curve>
                                {
                                    Line.CreateBound(barStart, ptA),  // straight at main
                                    Line.CreateBound(ptA, ptB),       // angled crank (main → offset)
                                    Line.CreateBound(ptB, ptC)        // straight at offset
                                };

                                crankedDefs.Add(new RebarDefinition
                                {
                                    Curves = curves,
                                    Style = vDef.Style,
                                    BarTypeName = vDef.BarTypeName,
                                    BarDiameter = vDef.BarDiameter,
                                    Normal = vDef.Normal,
                                    ArrayDirection = vDef.ArrayDirection,
                                    FixedCount = vDef.FixedCount,
                                    DistributionWidth = vDef.DistributionWidth,
                                    Spacing = vDef.Spacing,
                                    ArrayLength = vDef.ArrayLength,
                                    HookStartName = vDef.HookStartName,
                                    HookEndName = null,
                                    HookStartOrientation = vDef.HookStartOrientation,
                                    HookEndOrientation = vDef.HookEndOrientation,
                                    Label = vDef.Label + " (Cranked)",
                                    Comment = vDef.Comment
                                });
                            }
                            definitions.AddRange(crankedDefs);
                        }
                        // ── NO CRANK — straight bars ──
                        else
                        {
                            definitions.AddRange(vertDefs);
                        }
                    }
                }

                // ── 3. STARTER BARS (bottom column only) ──
                if (isBottom && request.EnableStarterBars && maxBarDia > 0)
                {
                    double starterLen = request.StarterDevLength > 0
                        ? request.StarterDevLength
                        : ColumnContinuityCalculator.GetStarterBarLength(maxBarDia, request.DesignCode);

                    string starterBarTypeX = request.StarterBarTypeName ?? request.VerticalBarTypeNameX;
                    string starterBarTypeY = request.StarterBarTypeName ?? request.VerticalBarTypeNameY;
                    double starterDiaX = GetBarDiameter(starterBarTypeX);
                    double starterDiaY = GetBarDiameter(starterBarTypeY);

                    var layerTempl = request.Layers.FirstOrDefault() ?? new RebarLayerConfig();

                    double starterTopExt = LapSpliceCalculator.CalculateTensionLapLength(maxBarDia, request.DesignCode);

                    // Create starter bar definitions extending BELOW the column base and UP into the column by lap length
                    var starterDefs = CreateStarterBars(
                        host, starterBarTypeX, starterDiaX, starterBarTypeY, starterDiaY,
                        tDia, request.ColumnCountX, request.ColumnCountY,
                        starterLen, starterTopExt, request.StarterHookEndName);

                    if (starterDefs != null)
                        definitions.AddRange(starterDefs);
                }

                // ── 4. PLACE REBAR ──
                var ids = _creationService.PlaceRebar(column, definitions);
                if (ids.Count > 0) anySuccess = true;
            }

            return anySuccess;
        }

        /// <summary>
        /// Creates starter bar definitions that extend below the column base into the foundation.
        /// Mirrors the vertical bar positions but projects downward.
        /// </summary>
        private List<RebarDefinition> CreateStarterBars(
            HostGeometry host,
            string barTypeNameX, double barDiameterX,
            string barTypeNameY, double barDiameterY,
            double tDia, int nx, int ny,
            double starterLength, double topExtension, string hookEndName)
        {
            var definitions = new List<RebarDefinition>();
            if (nx < 1 && ny < 1) return definitions;

            XYZ basisX = host.LAxis;
            XYZ basisY = host.WAxis;
            XYZ basisZ = host.HAxis;
            XYZ origin = host.Origin;

            double width = host.Width;
            double depth = host.Height;
            double coverSide = host.CoverExterior;

            double maxBarDia = Math.Max(barDiameterX, barDiameterY);
            double innerOff = coverSide + tDia + maxBarDia / 2.0;

            double xFirst = -width / 2.0 + innerOff;
            double xLast = width / 2.0 - innerOff;
            double yFirst = -depth / 2.0 + innerOff;
            double yLast = depth / 2.0 - innerOff;

            double stepY = ny > 1 ? (depth - 2 * innerOff) / (ny - 1) : 0;

            // Starter bars run from (column base - starterLength) up to (column base + topExtension)
            double barBot = -starterLength;  // below column base
            double barTop = topExtension;    // lap length into column

            void AddStarterSet(int count, XYZ startPos, XYZ arrayDir, double distWidth, 
                              string bType, double bDia, XYZ outDir, string label)
            {
                if (count < 1) return;

                XYZ hookNormal = basisZ.CrossProduct(outDir);
                XYZ pStart = startPos + basisZ * barBot;
                XYZ pEnd = startPos + basisZ * barTop;

                definitions.Add(new RebarDefinition
                {
                    Curves = new List<Curve> { Line.CreateBound(pStart, pEnd) },
                    Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                    BarTypeName = bType,
                    BarDiameter = bDia,
                    Normal = hookNormal,
                    ArrayDirection = arrayDir,
                    FixedCount = count,
                    DistributionWidth = distWidth,
                    HookStartName = hookEndName,  // Hook at bottom of starter
                    HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                    HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                    Label = label,
                    Comment = "Starter Bar"
                });
            }

            // Mirror the same layout as CreateColumnVerticals
            // 1. Bottom Face (y = yFirst)
            if (nx > 0)
            {
                XYZ pos = origin + basisX * xFirst + basisY * yFirst;
                double dist = nx > 1 ? xLast - xFirst : 0;
                AddStarterSet(nx, pos, basisX, dist, barTypeNameX, barDiameterX, -basisY, "Starter Bar (Bottom Face)");
            }

            // 2. Top Face (y = yLast)
            if (nx > 0 && ny > 1)
            {
                XYZ pos = origin + basisX * xLast + basisY * yLast;
                double dist = nx > 1 ? xLast - xFirst : 0;
                AddStarterSet(nx, pos, -basisX, dist, barTypeNameX, barDiameterX, basisY, "Starter Bar (Top Face)");
            }

            // 3. Left Face Inner
            int nyInner = ny - 2;
            if (nyInner > 0 && nx > 0)
            {
                XYZ pos = origin + basisX * xFirst + basisY * (yLast - stepY);
                double dist = nyInner > 1 ? stepY * (nyInner - 1) : 0;
                AddStarterSet(nyInner, pos, -basisY, dist, barTypeNameY, barDiameterY, -basisX, "Starter Bar (Left Face)");
            }

            // 4. Right Face Inner
            if (nyInner > 0 && nx > 1)
            {
                XYZ pos = origin + basisX * xLast + basisY * (yFirst + stepY);
                double dist = nyInner > 1 ? stepY * (nyInner - 1) : 0;
                AddStarterSet(nyInner, pos, basisY, dist, barTypeNameY, barDiameterY, basisX, "Starter Bar (Right Face)");
            }

            return definitions;
        }

        private bool ProcessStripFooting(Element foundation, RebarRequest request)
        {
            HostGeometry? hostOpt = StripFootingGeometryModule.Read(_doc, foundation);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            // 1. Stirrups (Transverse)
            double transDia = 0;
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                transDia = GetBarDiameter(request.TransverseBarTypeName);
                var stirrupDef = StripFootingLayoutGenerator.CreateStirrup(
                    host, request.TransverseBarTypeName, transDia,
                    request.TransverseSpacing, request.TransverseStartOffset,
                    request.TransverseHookStartName, request.TransverseHookEndName);

                if (stirrupDef != null) definitions.Add(stirrupDef);
            }

            // 2. Longitudinal Layers
            // Offsets are stored in StockLength/Backing in this DTO (temporary mapping)
            double startOff = request.StockLength;
            double endOff = request.StockLength_Backing;

            foreach (var layer in request.Layers)
            {
                layer.BarDiameter_Backing = GetBarDiameter(layer.VerticalBarTypeName);
                if (layer.BarDiameter_Backing > 0)
                {
                    double barLen = host.Length - 2 * host.CoverExterior;
                    
                    var longDef = StripFootingLayoutGenerator.CreateLongitudinalLayer(
                        host, layer, transDia, startOff, endOff);

                    if (longDef != null)
                    {
                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                        {
                            var segments = LapSpliceCalculator.SplitBarForLap(
                                barLen, longDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(longDef.BarDiameter));

                            if (segments.Count <= 1)
                            {
                                definitions.Add(longDef);
                            }
                            else
                            {
                                var origLine = longDef.Curves[0] as Line;
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
                                        double crankOff = LapSpliceCalculator.GetCrankOffset(longDef.BarDiameter);
                                        double crankRun = LapSpliceCalculator.GetCrankRun(longDef.BarDiameter);
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(longDef.BarDiameter, request.DesignCode);
                                        double straightLap = lapLen + crankRun;

                                        // Crank vertically inward (up for bottom, down for top)
                                        XYZ crankDir = (layer.Side == RebarSide.Top) ? -host.HAxis : host.HAxis;

                                        XYZ ptA = segStart + crankDir * crankOff;
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
                                        Style = longDef.Style,
                                        BarTypeName = longDef.BarTypeName,
                                        BarDiameter = longDef.BarDiameter,
                                        FixedCount = longDef.FixedCount,
                                        DistributionWidth = longDef.DistributionWidth,
                                        ArrayDirection = longDef.ArrayDirection,
                                        Normal = longDef.Normal,
                                        HookStartName = (si == 0) ? longDef.HookStartName : null,
                                        HookEndName = (si == segments.Count - 1) ? longDef.HookEndName : null,
                                        HookStartOrientation = longDef.HookStartOrientation,
                                        HookEndOrientation = longDef.HookEndOrientation,
                                        Label = $"Footing Long. {layer.Side} (lapped)"
                                    });
                                }
                            }
                        }
                        else
                        {
                            definitions.Add(longDef);
                        }
                    }
                }
            }

            var ids = _creationService.PlaceRebar(foundation, definitions);
            return ids.Count > 0;
        }

        private bool ProcessFootingPad(Element foundation, RebarRequest request)
        {
            HostGeometry? hostOpt = FootingPadGeometryModule.Read(_doc, foundation);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            foreach (var layer in request.Layers)
            {
                layer.BarDiameter_Backing = GetBarDiameter(layer.VerticalBarTypeName);
                if (layer.BarDiameter_Backing > 0)
                {
                    bool isTop = (layer.Side == RebarSide.Top);
                    var matDefs = FootingPadLayoutGenerator.CreateMat(host, layer, isTop);
                    if (matDefs != null) definitions.AddRange(matDefs);
                }
            }

            var ids = _creationService.PlaceRebar(foundation, definitions);
            return ids.Count > 0;
        }

        public (int Processed, int Total) GenerateWallCornerRebar(
            List<Wall> walls, RebarRequest request)
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

        private double GetBarDiameter(string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;

            var barType = new FilteredElementCollector(_doc)
                .OfClass(typeof(Autodesk.Revit.DB.Structure.RebarBarType))
                .Cast<Autodesk.Revit.DB.Structure.RebarBarType>()
                .FirstOrDefault(t => string.Equals(t.Name, barTypeName, StringComparison.OrdinalIgnoreCase));

            return barType?.BarModelDiameter ?? 0;
        }

        private double GetParamHeight(Element elem)
        {
            foreach (string name in new[] { "Height", "h", "Depth" })
            {
                Parameter p = elem.LookupParameter(name);
                if (p == null)
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        Element elemType = _doc.GetElement(typeId);
                        if (elemType != null) p = elemType.LookupParameter(name);
                    }
                }
                if (p != null && p.HasValue) return p.AsDouble();
            }
            return 0;
        }

        /// <summary>
        /// Finds how much to trim from each end of a wall due to intersecting walls.
        /// Returns (startTrim, endTrim) — the distance from each wall end to the face
        /// of the intersecting wall. Returns 0 if no intersection at that end.
        /// </summary>
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

        private static double Dist2D(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
