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
    public class ColumnEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;
        private readonly StandardShapeService _shapeService;

        public ColumnEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
            _shapeService = new StandardShapeService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            if (!(host is FamilyInstance column)) return false;
            return ProcessColumn(column, request);
        }

        public (int Processed, int Total) GenerateColumnRebar(List<FamilyInstance> columns, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Column Rebar"))
            {
                t.Start();
                foreach (var col in columns)
                {
                    try
                    {
                        if (request.RemoveExisting)
                            _creationService.DeleteExistingRebar(col);

                        if (ProcessColumn(col, request))
                            processed++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ColumnEngine: {col.Id} failed: {ex.Message}");
                    }
                }
                t.Commit();
            }
            return (processed, columns.Count);
        }

        public (int Processed, int Total) GenerateColumnStackRebar(List<FamilyInstance> stack, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Column Stack"))
            {
                t.Start();
                try
                {
                    if (ProcessColumnStack(stack, request))
                        processed = stack.Count;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ColumnEngine: Stack failed: {ex.Message}");
                }
                t.Commit();
            }
            return (processed, stack.Count);
        }

        private bool ProcessColumn(FamilyInstance column, RebarRequest request)
        {
            HostGeometry? hostOpt = ColumnGeometryModule.Read(_doc, column);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            if (request.IsCircularColumn)
            {
                // Calculate extensions for single column (starters)
                double topExt = request.VerticalTopExtension;
                double botExt = request.VerticalBottomExtension;
                double footingExt = -1;

                if (request.EnableStarterBars)
                {
                    footingExt = DetermineStarterExtensionIntoFooting(column, host);
                    if (footingExt > 0) botExt = footingExt;
                }

                var circDefs = CircularColumnLayoutGenerator.Generate(_doc, host, request, topExt, botExt);
                
                // Add Circular Starters if foundation detected
                if (request.EnableStarterBars && footingExt > 0)
                {
                    double maxBarDia = GetBarDiameter(request.VerticalBarTypeName);
                    double starterTopExt = GetLapSpliceLength(maxBarDia, request);
                    var circularStarters = CircularColumnLayoutGenerator.GenerateStarters(_doc, host, request, footingExt, starterTopExt);
                    if (circularStarters != null) circDefs.AddRange(circularStarters);
                }

                var circIds = _creationService.PlaceRebar(column, circDefs);
                bool success = circIds.Count > 0;

                // Transverse logic (existing) ...
                if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                {
                    double hostRadius = 0;
                    if (host.BoundaryCurves.Count > 0 && host.BoundaryCurves.Any(c => c is Arc))
                    {
                        var arcs = host.BoundaryCurves.OfType<Arc>().ToList();
                        hostRadius = arcs.Max(a => a.Radius);
                    }
                    else
                    {
                        hostRadius = Math.Min(host.Width, host.Length) / 2.0;
                    }

                    if (hostRadius > 0)
                    {
                        double rebarRadius = hostRadius - host.CoverExterior;
                        if (rebarRadius <= 0) rebarRadius = hostRadius * 0.8;
                        XYZ center = host.Origin;
                        
                        double zStart = host.SolidZMin + host.CoverBottom + UnitConversion.MmToFeet(50);
                        double zEnd = host.SolidZMax - host.CoverTop - UnitConversion.MmToFeet(120); // 70mm gap + 50mm hoop gap
                        
                        // --- NEW: Add ties into footing for circular columns ---
                        if (request.EnableStarterBars && footingExt > 0)
                        {
                            double ftzStart = -footingExt + UnitConversion.MmToFeet(75);
                            double ftzEnd = -UnitConversion.MmToFeet(50);
                            RebarBarType ftBarType = new FilteredElementCollector(_doc)
                                .OfClass(typeof(RebarBarType))
                                .Cast<RebarBarType>()
                                .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));
                            
                            if (ftBarType != null)
                            {
                                try {
                                    var tie = CircularRebarService.CreateCircularTie(_doc, column, center, rebarRadius, ftzStart, ftBarType);
                                    if (tie != null) {
                                        var accessor = tie.GetShapeDrivenAccessor();
                                        accessor.SetLayoutAsMaximumSpacing(request.TransverseSpacing, ftzEnd - ftzStart, true, true, true);
                                    }
                                } catch {}
                            }
                        }
                        // -----------------------------------------------------

                        double dist = zEnd - zStart;
                        RebarBarType tieBarType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(RebarBarType))
                            .Cast<RebarBarType>()
                            .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));

                        RebarShape tieShape = _shapeService.FindShapeRobustly(request.EnableSpiral ? "Shape SP" : "Shape CT");

                        if (tieBarType != null && dist > 0)
// ... (rest of old code)
                        {
                            try 
                            {
                                if (request.EnableSpiral)
                                {
                                    var tie = CircularRebarService.CreateSpiralFromRing(_doc, column, center, rebarRadius, zStart, zEnd, tieBarType, request.TransverseSpacing, tieShape);
                                    if (tie != null) success = true;
                                }
                                else
                                {
                                    // Distribute as a Rebar Set using Maximum Spacing
                                    var tie = CircularRebarService.CreateCircularTie(_doc, column, center, rebarRadius, zStart, tieBarType, tieShape);
                                    if (tie != null)
                                    {
                                        var accessor = tie.GetShapeDrivenAccessor();
                                        accessor.SetLayoutAsMaximumSpacing(
                                            request.TransverseSpacing,
                                            dist,
                                            true,
                                            true,
                                            true
                                        );
                                        success = true;
                                    }
                                }
                            } catch (Exception ex) {
                                System.Diagnostics.Debug.WriteLine($"Error creating circular ties: {ex.Message}");
                            }
                        }
                    }
                }
                
                return success;
            }

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
                                    barLen, vDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(vDef.BarDiameter), 
                                    BarPosition.Other, GetLapSpliceLength(vDef.BarDiameter, request));

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
                                        double lapLen = GetLapSpliceLength(vDef.BarDiameter, request);
                                        double straightLap = lapLen;

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
                                        Label = "Main Vertical Bar (lapped)",
                                        Comment = "Main Bar"
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

                    // --- NEW: Check for Footing below ---
                    double footingExt = DetermineStarterExtensionIntoFooting(column, host);
                    if (footingExt > 0)
                    {
                        starterLen = footingExt;
                    }
                    // ------------------------------------

                    string starterBarTypeX = request.StarterBarTypeName ?? request.VerticalBarTypeNameX;
                    string starterBarTypeY = request.StarterBarTypeName ?? request.VerticalBarTypeNameY;
                    double starterDiaX = GetBarDiameter(starterBarTypeX);
                    double starterDiaY = GetBarDiameter(starterBarTypeY);

                    double tieDia = 0.0328; // fallback 10mm
                    if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                        tieDia = GetBarDiameter(request.TransverseBarTypeName);

                    double starterTopExt = GetLapSpliceLength(maxBarDia, request);

                    var starterDefs = CreateStarterBars(
                        host, starterBarTypeX, starterDiaX, starterBarTypeY, starterDiaY,
                        tieDia, request.ColumnCountX, request.ColumnCountY,
                        starterLen, starterTopExt, request.StarterHookEndName);

                    if (starterDefs != null)
                        definitions.AddRange(starterDefs);

                    // --- NEW: Add ties into footing for starters ---
                    if (footingExt > 0 && !string.IsNullOrEmpty(request.TransverseBarTypeName))
                    {
                        double tDia = GetBarDiameter(request.TransverseBarTypeName);
                        // Range for ties in footing: from footing bottom + cover up to column base
                        double zStart = -footingExt + UnitConversion.MmToFeet(75); // Use 75mm bot cover for footing
                        double zEnd = -UnitConversion.MmToFeet(50); // Stop 50mm below column base

                        var footingTie = ColumnLayoutGenerator.CreateColumnTiesInRange(
                            host, request.TransverseBarTypeName, tDia,
                            request.TransverseSpacing, zStart, zEnd,
                            request.TransverseHookStartName, request.TransverseHookEndName);
                        
                        if (footingTie != null)
                        {
                            footingTie.Label = "Footing Tie";
                            definitions.Add(footingTie);
                        }
                    }
                    // -----------------------------------------------
                }
            }

            var ids = _creationService.PlaceRebar(column, definitions);
            return ids.Count > 0;
        }

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

                bool crankUpper = string.Equals(request.CrankPosition, "Upper Column", StringComparison.OrdinalIgnoreCase);
                bool crankLower = string.Equals(request.CrankPosition, "Lower Column", StringComparison.OrdinalIgnoreCase);

                if (request.IsCircularColumn)
                {
                    double botExt = request.VerticalBottomExtension;
                    double topExt = request.VerticalTopExtension;
                    double maxBarDia = GetBarDiameter(request.VerticalBarTypeName);
                    double footingExt = -1;

                    if (!isBottom) botExt = 0;
                    if (!isTop) topExt = ColumnContinuityCalculator.GetSpliceStartOffset() + GetLapSpliceLength(maxBarDia, request);

                    if (isBottom && request.EnableStarterBars)
                    {
                        footingExt = DetermineStarterExtensionIntoFooting(column, host);
                        if (footingExt > 0) botExt = footingExt;
                    }


                    var circDefs = CircularColumnLayoutGenerator.Generate(_doc, host, request, topExt, botExt, 
                        !isBottom && crankUpper, !isTop && crankLower);
                    
                    // Starters for bottom column
                    if (isBottom && request.EnableStarterBars && footingExt > 0)
                    {
                        double starterTopExt = GetLapSpliceLength(maxBarDia, request);
                        var starterDefs = CircularColumnLayoutGenerator.GenerateStarters(_doc, host, request, footingExt, starterTopExt);
                        if (starterDefs != null) circDefs.AddRange(starterDefs);
                    }

                    var circIds = _creationService.PlaceRebar(column, circDefs);
                    if (circIds.Count > 0) anySuccess = true;

                    // Manually generate Transverse Reinforcement for Circular Columns
                    if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                    {
                        double hostRadius = 0;
                        if (host.BoundaryCurves.Count > 0 && host.BoundaryCurves.Any(c => c is Arc))
                        {
                            var arcs = host.BoundaryCurves.OfType<Arc>().ToList();
                            hostRadius = arcs.Max(a => a.Radius);
                        }
                        else
                        {
                            hostRadius = Math.Min(host.Width, host.Length) / 2.0;
                        }

                        if (hostRadius > 0)
                        {
                            double rebarRadius = hostRadius - host.CoverExterior;
                            if (rebarRadius <= 0) rebarRadius = hostRadius * 0.8;
                            XYZ center = host.Origin;
                            
                            double zStart = host.SolidZMin + host.CoverBottom + UnitConversion.MmToFeet(50);
                            double zEnd = host.SolidZMax - host.CoverTop - UnitConversion.MmToFeet(120); // 70mm gap + 50mm hoop gap
                            
                            // --- NEW: Add ties into footing for circular columns stack ---
                            if (isBottom && request.EnableStarterBars && footingExt > 0)
                            {
                                double ftzStart = -footingExt + UnitConversion.MmToFeet(75);
                                double ftzEnd = -UnitConversion.MmToFeet(50);
                                RebarBarType ftBarType = new FilteredElementCollector(_doc)
                                    .OfClass(typeof(RebarBarType))
                                    .Cast<RebarBarType>()
                                    .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));
                                
                                if (ftBarType != null)
                                {
                                    try {
                                        var tie = CircularRebarService.CreateCircularTie(_doc, column, center, rebarRadius, ftzStart, ftBarType);
                                        if (tie != null) {
                                            var accessor = tie.GetShapeDrivenAccessor();
                                            accessor.SetLayoutAsMaximumSpacing(request.TransverseSpacing, ftzEnd - ftzStart, true, true, true);
                                            anySuccess = true;
                                        }
                                    } catch {}
                                }
                            }
                            // -----------------------------------------------------------

                            double dist = zEnd - zStart;

                            RebarBarType tieBarType = new FilteredElementCollector(_doc)
                                .OfClass(typeof(RebarBarType))
                                .Cast<RebarBarType>()
                                .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));

                            if (tieBarType != null && dist > 0)
                            {
                                try 
                                {
                                    if (request.EnableSpiral)
                                    {
                                        var tie = CircularRebarService.CreateSpiralFromRing(_doc, column, center, rebarRadius, zStart, zEnd, tieBarType, request.TransverseSpacing);
                                        if (tie != null) anySuccess = true;
                                    }
                                    else
                                    {
                                        int numSpaces = (int)Math.Max(1, Math.Floor(dist / request.TransverseSpacing));
                                        for (int j = 0; j <= numSpaces; j++)
                                        {
                                            double z = zStart + j * (dist / numSpaces);
                                            var tie = CircularRebarService.CreateCircularTie(_doc, column, center, rebarRadius, z, tieBarType);
                                            if (tie != null) anySuccess = true;
                                        }
                                    }
                                } catch (Exception ex) {
                                    System.Diagnostics.Debug.WriteLine($"Error creating circular ties in stack: {ex.Message}");
                                }
                            }
                        }
                    }

                    continue;
                }

                var definitions = new List<RebarDefinition>();

                // Get bar diameters
                double vDiaX = GetBarDiameter(request.VerticalBarTypeNameX);
                double vDiaY = GetBarDiameter(request.VerticalBarTypeNameY);
                double tDia = 0.0328; // fallback 10mm
                if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                    tDia = GetBarDiameter(request.TransverseBarTypeName);
                
                double maxBarDiaStandard = Math.Max(vDiaX, vDiaY);

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
                        double lapLen = GetLapSpliceLength(maxBarDiaStandard, request);
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
                                double lapLen = GetLapSpliceLength(vDef.BarDiameter, request);
                                double straightLap = lapLen;

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
                if (isBottom && request.EnableStarterBars && maxBarDiaStandard > 0)
                {
                    double starterLen = request.StarterDevLength > 0
                        ? request.StarterDevLength
                        : ColumnContinuityCalculator.GetStarterBarLength(maxBarDiaStandard, request.DesignCode);

                    // --- NEW: Check for Footing below ---
                    double footingExt = DetermineStarterExtensionIntoFooting(column, host);
                    if (footingExt > 0)
                    {
                        starterLen = footingExt;
                    }
                    // ------------------------------------

                    string starterBarTypeX = request.StarterBarTypeName ?? request.VerticalBarTypeNameX;
                    string starterBarTypeY = request.StarterBarTypeName ?? request.VerticalBarTypeNameY;
                    double starterDiaX = GetBarDiameter(starterBarTypeX);
                    double starterDiaY = GetBarDiameter(starterBarTypeY);

                    var layerTempl = request.Layers.FirstOrDefault() ?? new RebarLayerConfig();

                    double starterTopExt = GetLapSpliceLength(maxBarDiaStandard, request);

                    // Create starter bar definitions extending BELOW the column base and UP into the column by lap length
                    var starterDefs = CreateStarterBars(
                        host, starterBarTypeX, starterDiaX, starterBarTypeY, starterDiaY,
                        tDia, request.ColumnCountX, request.ColumnCountY,
                        starterLen, starterTopExt, request.StarterHookEndName);

                    if (starterDefs != null)
                        definitions.AddRange(starterDefs);

                    // --- NEW: Add ties into footing for starters ---
                    if (footingExt > 0 && !string.IsNullOrEmpty(request.TransverseBarTypeName))
                    {
                        // Range for ties in footing: from footing bottom + cover up to column base
                        double zStart = -footingExt + UnitConversion.MmToFeet(75); // Use 75mm bot cover for footing
                        double zEnd = -UnitConversion.MmToFeet(50); // Stop 50mm below column base

                        var footingTie = ColumnLayoutGenerator.CreateColumnTiesInRange(
                            host, request.TransverseBarTypeName, tDia,
                            request.TransverseSpacing, zStart, zEnd,
                            request.TransverseHookStartName, request.TransverseHookEndName);
                        
                        if (footingTie != null)
                        {
                            footingTie.Label = "Footing Tie";
                            definitions.Add(footingTie);
                        }
                    }
                    // -----------------------------------------------
                }

                var ids = _creationService.PlaceRebar(column, definitions);
                if (ids.Count > 0) anySuccess = true;
            }

            return anySuccess;
        }

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
                    Style = RebarStyle.Standard,
                    BarTypeName = bType,
                    BarDiameter = bDia,
                    Normal = hookNormal,
                    ArrayDirection = arrayDir,
                    FixedCount = count,
                    DistributionWidth = distWidth,
                    HookStartName = hookEndName,  // Hook at bottom of starter
                    HookStartOrientation = (RebarHookOrientation)1, // Left
                    HookEndOrientation = (RebarHookOrientation)1,   // Left
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

        private double GetBarDiameter(string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;
            var barType = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(x => x.Name.Equals(barTypeName, StringComparison.OrdinalIgnoreCase));
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

        private double DetermineStarterExtensionIntoFooting(FamilyInstance column, HostGeometry host)
        {
            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            if (bbox == null) return -1;

            XYZ pointBelow = host.Origin - host.HAxis * 0.1;

            var foundations = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var f in foundations)
            {
                BoundingBoxXYZ fBox = f.get_BoundingBox(null);
                if (fBox == null) continue;

                // Check if column base is within foundation footprint
                bool inFootprint = pointBelow.X >= fBox.Min.X && pointBelow.X <= fBox.Max.X &&
                                   pointBelow.Y >= fBox.Min.Y && pointBelow.Y <= fBox.Max.Y;
                
                // Column bottom should be sits on foundation top (allow 0.2ft/60mm gap)
                bool onTop = Math.Abs(host.SolidZMin - fBox.Max.Z) < 0.2;

                if (inFootprint && onTop)
                {
                    double coverBot = GeometryUtils.GetCoverDistance(_doc, f, BuiltInParameter.CLEAR_COVER_BOTTOM);
                    
                    // Extension from column base to reaching bottom cover of footing
                    double extension = host.SolidZMin - (fBox.Min.Z + coverBot);
                    
                    if (extension > 0) return extension;
                }
            }
            return -1;
        }
    }
}
