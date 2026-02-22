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
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = topZ - barDia / 2.0;

                // Check if bar needs splitting for 12m lap
                double barLen = host.Length - 2 * host.CoverOther;
                var segments = LapSpliceCalculator.SplitBarForLap(barLen, barDia, request.DesignCode);

                foreach (var seg in segments)
                {
                    // Create a sub-bar for this segment
                    double innerOffset = host.CoverOther + transDia;
                    double distWidthSeg = host.Width - 2 * innerOffset;

                    XYZ s = host.StartPoint + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = host.StartPoint + host.LAxis * (host.CoverOther + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    Curve barLine = Line.CreateBound(barStart, barEnd);
                    var segDef = new RebarDefinition
                    {
                        Curves = new List<Curve> { barLine },
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        Spacing = 0,
                        ArrayLength = 0,
                        ArrayDirection = host.WAxis,
                        FixedCount = count,
                        DistributionWidth = distWidthSeg,
                        Normal = host.WAxis,
                        HookStartName = (seg.Start == 0) ? layer.HookStartName : null,
                        HookEndName = (seg.End >= barLen - 0.001) ? layer.HookEndName : null,
                        Label = segments.Count > 1 ? "Top Layer (lapped)" : "Top Layer"
                    };
                    definitions.Add(segDef);
                }

                topZ -= (barDia + minLayerGap);
            }

            // Bottom layers
            double botZ = zMin + host.CoverBottom + transDia;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0))
            {
                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = botZ + barDia / 2.0;

                // Check if bar needs splitting for 12m lap
                double barLen = host.Length - 2 * host.CoverOther;
                var segments = LapSpliceCalculator.SplitBarForLap(barLen, barDia, request.DesignCode);

                foreach (var seg in segments)
                {
                    XYZ s = host.StartPoint + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = host.StartPoint + host.LAxis * (host.CoverOther + seg.End);
                    double innerOffset = host.CoverOther + transDia;
                    double distWidthSeg = host.Width - 2 * innerOffset;
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    Curve barLine = Line.CreateBound(barStart, barEnd);
                    var segDef = new RebarDefinition
                    {
                        Curves = new List<Curve> { barLine },
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        Spacing = 0,
                        ArrayLength = 0,
                        ArrayDirection = host.WAxis,
                        FixedCount = count,
                        DistributionWidth = distWidthSeg,
                        Normal = host.WAxis,
                        HookStartName = (seg.Start == 0) ? layer.HookStartName : null,
                        HookEndName = (seg.End >= barLen - 0.001) ? layer.HookEndName : null,
                        Label = segments.Count > 1 ? "Bottom Layer (lapped)" : "Bottom Layer"
                    };
                    definitions.Add(segDef);
                }

                botZ += (barDia + minLayerGap);
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

                    if (vertDef != null) definitions.Add(vertDef);
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

                    if (horizDef != null) definitions.Add(horizDef);
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

                        if (barLen > LapSpliceCalculator.MaxStockLengthFt && maxBarDia > 0)
                        {
                            var lappedDefs = new List<RebarDefinition>();
                            foreach (var vDef in vertDefs)
                            {
                                var segments = LapSpliceCalculator.SplitBarForLap(
                                    barLen, vDef.BarDiameter, request.DesignCode);

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
                                    Curve segLine = Line.CreateBound(segStart, segEnd);

                                    var segDef = new RebarDefinition
                                    {
                                        Curves = new List<Curve> { segLine },
                                        Style = vDef.Style,
                                        BarTypeName = vDef.BarTypeName,
                                        BarDiameter = vDef.BarDiameter,
                                        Normal = vDef.Normal,
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
                    var longDef = StripFootingLayoutGenerator.CreateLongitudinalLayer(
                        host, layer, transDia, startOff, endOff);
                    if (longDef != null) definitions.Add(longDef);
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
