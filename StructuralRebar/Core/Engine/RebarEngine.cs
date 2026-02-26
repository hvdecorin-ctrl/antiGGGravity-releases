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

        public (int Processed, int Total) GenerateColumnRebar(
            List<FamilyInstance> columns, RebarRequest request)
        {
            return GenerateRebarInternal(columns, request, "Generate Column Rebar");
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
                        Label = segments.Count > 1 ? "Top Layer (lapped)" : "Top Layer"
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
                        Label = segments.Count > 1 ? "Bottom Layer (lapped)" : "Bottom Layer"
                    };
                    definitions.Add(segDef);
                }

                botZ += (barDia + minLayerGap);
                botLayerIdx++;
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

            // For slanted beams, bar positions are offsets from section center along HAxis
            // HAxis is perpendicular to the beam in the vertical plane (local "up")
            // Top offset = positive along HAxis, Bottom offset = negative along HAxis

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
                    layer.HookStartName, layer.HookEndName, "Top Layer");

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
                    layer.HookStartName, layer.HookEndName, "Bottom Layer");

                if (def != null) definitions.Add(def);
                botOffset += (barDia + minLayerGap);
            }
        }

        private bool ProcessWall(Wall wall, RebarRequest request)
        {
            HostGeometry host = WallGeometryModule.Read(_doc, wall);
            if (host.Length <= 0 || host.Width <= 0 || host.Height <= 0) return false;

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
            if (config == "Centre")
            {
                // Vertical at centre (0), horizontal beside vertical
                double avgCover = (host.CoverExterior + host.CoverInterior) / 2.0;
                layers.Add((0, (vDia / 2.0 + hDia / 2.0)));
            }
            else if (config == "Both faces")
            {
                layers.Add((dExtV, dExtH));
                layers.Add((-dIntV, -dIntH));
            }
            else if (config == "External face")
            {
                layers.Add((dExtV, dExtH));
            }
            else if (config == "Internal face")
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
                        request.TransverseSpacing, request.TransverseStartOffset, request.TransverseEndOffset,
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
                                barLen, vertDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(vertDef.BarDiameter));

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
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(vertDef.BarDiameter, request.DesignCode);
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
                        host.CoverOther, host.CoverOther,
                        hOff,
                        layer.HookStartName, layer.HookEndName,
                        layer.HookStartOutward, layer.HookEndOutward);

                    if (horizDef != null)
                    {
                        double barLen = host.Length - 2 * host.CoverOther;
                        
                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                        {
                            var segments = LapSpliceCalculator.SplitBarForLap(
                                barLen, horizDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(horizDef.BarDiameter));

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
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(horizDef.BarDiameter, request.DesignCode);
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
            }

            var ids = _creationService.PlaceRebar(wall, definitions);
            return ids.Count > 0;
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
                    // For Column, we use the first layer in the list to store Hook settings
                    var layerTempl = request.Layers.FirstOrDefault() ?? new RebarLayerConfig();

                    var vertDefs = ColumnLayoutGenerator.CreateColumnVerticals(
                        host, 
                        request.VerticalBarTypeNameX, vDiaX,
                        request.VerticalBarTypeNameY, vDiaY,
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

            var ids = _creationService.PlaceRebar(column, definitions);
            return ids.Count > 0;
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
            int count = 0;

            using (Transaction t = new Transaction(_doc, "Generate Wall Corner Rebar"))
            {
                t.Start();

                foreach (var corner in corners)
                {
                    try
                    {
                        if (request.RemoveExisting)
                        {
                            // Delete rebar from both walls involved in the corner
                            _creationService.DeleteExistingRebar(corner.Wall1);
                            _creationService.DeleteExistingRebar(corner.Wall2);
                        }

                        bool success = false;
                        if (request.HostType == ElementHostType.WallCornerL)
                            success = ProcessWallCornerL(corner, request);
                        else if (request.HostType == ElementHostType.WallCornerU)
                            success = ProcessWallCornerU(corner, request);

                        if (success) count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Wall Corner failed: {ex.Message}");
                    }
                }

                t.Commit();
            }

            return (count, corners.Count);
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
    }
}
