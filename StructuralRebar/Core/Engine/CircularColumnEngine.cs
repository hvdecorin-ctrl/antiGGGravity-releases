using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.Core.Calculators;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    /// <summary>
    /// Dedicated engine for Circular Columns.
    /// Separated from ColumnEngine to prevent any impact on standard rectangular column logic.
    /// Uses the same proven approach as BoredPileEngine for transverse reinforcement
    /// (CircularRebarService for hoops/spirals).
    /// </summary>
    public class CircularColumnEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;
        private readonly StandardShapeService _shapeService;

        public CircularColumnEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
            _shapeService = new StandardShapeService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            if (!(host is FamilyInstance column)) return false;

            HostGeometry? hostOpt = ColumnGeometryModule.Read(_doc, column);
            if (!hostOpt.HasValue) return false;
            HostGeometry geo = hostOpt.Value;

            return ProcessSingleColumn(column, geo, request,
                isBottom: true, isTop: true,
                crankUpper: false, crankLower: false);
        }

        // ── Multi-Level: Generate an entire column stack ──
        public (int Processed, int Total) GenerateColumnStackRebar(List<FamilyInstance> stack, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Circular Column Stack"))
            {
                t.Start();
                try
                {
                    if (ProcessColumnStack(stack, request))
                        processed = stack.Count;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CircularColumnEngine: Stack failed: {ex.Message}");
                }
                t.Commit();
            }
            return (processed, stack.Count);
        }

        // ── Single-Column entry (wraps in transaction) ──
        public (int Processed, int Total) GenerateColumnRebar(List<FamilyInstance> columns, RebarRequest request)
        {
            int processed = 0;
            using (Transaction t = new Transaction(_doc, "Generate Circular Column Rebar"))
            {
                t.Start();
                foreach (var col in columns)
                {
                    try
                    {
                        HostGeometry? hostOpt = ColumnGeometryModule.Read(_doc, col);
                        if (!hostOpt.HasValue) continue;
                        HostGeometry geo = hostOpt.Value;

                        if (request.RemoveExisting)
                            _creationService.DeleteExistingRebar(col);

                        if (ProcessSingleColumn(col, geo, request,
                                isBottom: true, isTop: true,
                                crankUpper: false, crankLower: false))
                            processed++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CircularColumnEngine: {ex.Message}");
                    }
                }
                t.Commit();
            }
            return (processed, columns.Count);
        }

        // ═══════════════════════════════════════════════
        // CORE: Process a single circular column level
        // ═══════════════════════════════════════════════
        private bool ProcessSingleColumn(
            FamilyInstance column,
            HostGeometry host,
            RebarRequest request,
            bool isBottom, bool isTop,
            bool crankUpper, bool crankLower)
        {
            bool success = false;

            // ── 1. Geometry ────────────────────────────────
            double hostRadius = GetHostRadius(host);
            if (hostRadius <= 0) return false;

            double transverseRadius = hostRadius - host.CoverExterior;
            if (transverseRadius <= 0) transverseRadius = hostRadius * 0.8;

            double transverseBarDia = GetBarDiameter(request.TransverseBarTypeName);
            double verticalBarDia = GetBarDiameter(request.VerticalBarTypeName);

            double comfortGap = UnitConversion.MmToFeet(2);
            double inset = (transverseBarDia / 2.0) + (verticalBarDia / 2.0) + comfortGap;
            double rebarRadius = transverseRadius - inset;
            if (rebarRadius <= 0) rebarRadius = transverseRadius * 0.7;

            XYZ center = host.Origin;
            double zBase = host.SolidZMin;
            double zTop = host.SolidZMax;

            // ── 2. Vertical Bars ───────────────────────────
            double zStart = zBase + host.CoverBottom;
            double zEnd = zTop - host.CoverTop - UnitConversion.MmToFeet(70); // 70mm safety gap

            // Manual extensions from UI
            if (request.VerticalBottomExtension > 0)
                zStart = zBase - request.VerticalBottomExtension;
            if (request.VerticalTopExtension > 0)
                zEnd = zTop + request.VerticalTopExtension;

            // For top column (or single column): detect slab above and extend bars into it
            double slabExtension = 0;
            if (isTop)
            {
                slabExtension = DetectSlabAboveExtension(column, host);
                if (slabExtension > 0)
                {
                    zEnd = zTop - host.CoverTop + slabExtension;
                }
            }

            // Multi-level extensions
            double topExt = 0;
            double botExt = 0;

            if (!isTop)
            {
                // Not the top column → extend bars upward for lap splice
                double lap = GetLapSpliceLength(verticalBarDia, request);
                topExt = ColumnContinuityCalculator.GetSpliceStartOffset() + lap;
                zEnd = zTop - host.CoverTop + topExt;
            }

            // Starters / Foundation detection (but do NOT extend VERTICAL bars into footing)
            double footingExt = -1;
            if (isBottom && request.EnableStarterBars)
            {
                footingExt = DetermineStarterExtensionIntoFooting(column, host);
                // footingExt is used only for starter bars and footing ties, not vertical bars
            }

            // Resolve hooks from Layers[0]
            string hookTopName = null;
            string hookBotName = null;
            bool hookTopOutward = false;
            bool hookBotOutward = false;
            if (request.Layers != null && request.Layers.Count > 0)
            {
                hookTopName = request.Layers[0].HookEndName;
                hookBotName = request.Layers[0].HookStartName;
                hookTopOutward = request.Layers[0].HookEndOutward;
                hookBotOutward = request.Layers[0].HookStartOutward;
            }

            // Generate vertical bar definitions (original proven approach)
            var definitions = GenerateVerticalBars(host, request, center, rebarRadius,
                zStart, zEnd, verticalBarDia, crankUpper, crankLower, topExt, botExt,
                hookTopName, hookBotName, hookTopOutward, hookBotOutward, isTop);

            // ── 3. Starter Bars ────────────────────────────
            if (isBottom && request.EnableStarterBars && footingExt > 0)
            {
                double starterTopExt = GetLapSpliceLength(verticalBarDia, request);
                var starterDefs = GenerateStarterBars(host, request, center, rebarRadius,
                    footingExt, starterTopExt, verticalBarDia);
                definitions.AddRange(starterDefs);
            }

            // Place vertical + starter bars
            var ids = _creationService.PlaceRebar(column, definitions);
            if (ids.Count > 0) success = true;

            // ── 4. Transverse Reinforcement (Hoops / Spiral) ──
            // Use CircularRebarService directly (same as BoredPileEngine)
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName) && hostRadius > 0)
            {
                double hoopRadius = hostRadius - host.CoverExterior;
                if (hoopRadius <= 0) hoopRadius = hostRadius * 0.8;

                double tZstart = zBase + host.CoverBottom + UnitConversion.MmToFeet(50);
                double tZend = zTop - host.CoverTop - UnitConversion.MmToFeet(120);
                double dist = tZend - tZstart;

                RebarBarType tieBarType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));

                RebarShape tieShape = _shapeService.FindShapeRobustly(
                    request.EnableSpiral ? "Shape SP" : "Shape CT");

                if (tieBarType != null && dist > 0)
                {
                    try
                    {
                        if (request.EnableSpiral)
                        {
                            var tie = CircularRebarService.CreateSpiralFromRing(
                                _doc, column, center, hoopRadius,
                                tZstart, tZend, tieBarType, request.TransverseSpacing, tieShape);
                            if (tie != null) success = true;
                        }
                        else if (request.EnableZoneSpacing)
                        {
                            // Zone-based distribution (3-zone confinement)
                            double mainBarDia = GetBarDiameter(request.VerticalBarTypeName);
                            double transDia = GetBarDiameter(request.TransverseBarTypeName);
                            double maxDim = hostRadius * 2; // diameter for circular

                            var zones = ZoneSpacingCalculator.CalculateColumnZones(
                                dist, maxDim, mainBarDia > 0 ? mainBarDia : transDia, transDia, request.DesignCode);

                            double zoneStart = tZstart;
                            foreach (var zone in zones)
                            {
                                double zoneEnd = zoneStart + zone.Length;
                                double zoneDist = zone.Length;
                                if (zoneDist > UnitConversion.MmToFeet(50))
                                {
                                    var tie = CircularRebarService.CreateCircularTie(
                                        _doc, column, center, hoopRadius, zoneStart, tieBarType, tieShape);
                                    if (tie != null)
                                    {
                                        var accessor = tie.GetShapeDrivenAccessor();
                                        accessor.SetLayoutAsMaximumSpacing(
                                            zone.Spacing, zoneDist, true, true, true);
                                        success = true;
                                    }
                                }
                                zoneStart = zoneEnd;
                            }
                        }
                        else
                        {
                            var tie = CircularRebarService.CreateCircularTie(
                                _doc, column, center, hoopRadius, tZstart, tieBarType, tieShape);
                            if (tie != null)
                            {
                                var accessor = tie.GetShapeDrivenAccessor();
                                accessor.SetLayoutAsMaximumSpacing(
                                    request.TransverseSpacing, dist, true, true, true);
                                success = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CircularColumnEngine Ties: {ex.Message}");
                    }
                }

                // ── 4b. Ties inside footing (for starters) ──
                if (isBottom && request.EnableStarterBars && footingExt > 0 && tieBarType != null)
                {
                    try
                    {
                        double ftzStart = zBase - footingExt + UnitConversion.MmToFeet(75);
                        double ftzEnd = zBase - UnitConversion.MmToFeet(50);
                        double ftDist = ftzEnd - ftzStart;

                        if (ftDist > 0)
                        {
                            var ftTie = CircularRebarService.CreateCircularTie(
                                _doc, column, center, hoopRadius, ftzStart, tieBarType, tieShape);
                            if (ftTie != null)
                            {
                                var accessor = ftTie.GetShapeDrivenAccessor();
                                accessor.SetLayoutAsMaximumSpacing(
                                    request.TransverseSpacing, ftDist, true, true, true);
                            }
                        }
                    }
                    catch { }
                }
            }

            return success;
        }

        // ═══════════════════════════════════════════════
        // Multi-Level Stack Processing
        // ═══════════════════════════════════════════════
        private bool ProcessColumnStack(List<FamilyInstance> stack, RebarRequest request)
        {
            if (stack == null || stack.Count == 0) return false;
            bool anySuccess = false;

            bool crankUpper = string.Equals(request.CrankPosition, "Upper Bar", StringComparison.OrdinalIgnoreCase);
            bool crankLower = string.Equals(request.CrankPosition, "Lower Bar", StringComparison.OrdinalIgnoreCase);

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

                if (ProcessSingleColumn(column, host, request,
                        isBottom, isTop,
                        !isBottom && crankUpper,
                        !isTop && crankLower))
                    anySuccess = true;
            }

            return anySuccess;
        }

        // ═══════════════════════════════════════════════
        // Vertical Bar Generation (original proven logic)
        // ═══════════════════════════════════════════════
        private List<RebarDefinition> GenerateVerticalBars(
            HostGeometry host, RebarRequest request,
            XYZ center, double rebarRadius,
            double zStart, double zEnd,
            double verticalBarDia,
            bool crankUpper, bool crankLower,
            double topExt, double botExt,
            string hookTopName, string hookBotName,
            bool hookTopOutward, bool hookBotOutward,
            bool isTopColumn)
        {
            var definitions = new List<RebarDefinition>();
            int count = request.PileBarCount;
            if (count < 1 || zEnd - zStart <= UnitConversion.MmToFeet(100)) return definitions;

            double crankOff = LapSpliceCalculator.GetCrankOffset(verticalBarDia);
            double crankRun = LapSpliceCalculator.GetCrankRun(verticalBarDia);

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                double dx = rebarRadius * Math.Cos(angle);
                double dy = rebarRadius * Math.Sin(angle);

                XYZ pStart = new XYZ(center.X + dx, center.Y + dy, zStart);
                XYZ pEnd = new XYZ(center.X + dx, center.Y + dy, zEnd);

                XYZ dirRadial = new XYZ(Math.Cos(angle), Math.Sin(angle), 0);
                List<Curve> curves = new List<Curve>();

                if (crankUpper && botExt > 0)
                {
                    XYZ ptA = pStart - dirRadial * crankOff;
                    XYZ ptB = ptA + XYZ.BasisZ * crankRun * 4;
                    XYZ ptC = pStart + XYZ.BasisZ * (crankRun * 5);
                    curves.Add(Line.CreateBound(ptA, ptB));
                    curves.Add(Line.CreateBound(ptB, ptC));
                    curves.Add(Line.CreateBound(ptC, pEnd));
                }
                else if (crankLower && topExt > 0)
                {
                    XYZ spliceStart = pEnd - XYZ.BasisZ * topExt;
                    XYZ ptA = spliceStart - XYZ.BasisZ * crankRun;
                    XYZ ptB = spliceStart - dirRadial * crankOff;
                    XYZ ptC = pEnd - dirRadial * crankOff;
                    curves.Add(Line.CreateBound(pStart, ptA));
                    curves.Add(Line.CreateBound(ptA, ptB));
                    curves.Add(Line.CreateBound(ptB, ptC));
                }
                else
                {
                    curves.Add(Line.CreateBound(pStart, pEnd));
                }

                XYZ vecToCenter = new XYZ(-dx, -dy, 0).Normalize();
                XYZ tangentNormal = XYZ.BasisZ.CrossProduct(vecToCenter).Normalize();

                // For circular columns: Left = bend inward, Right = bend outward
                var hookBotOri = hookBotOutward ? RebarHookOrientation.Right : RebarHookOrientation.Left;
                var hookTopOri = hookTopOutward ? RebarHookOrientation.Right : RebarHookOrientation.Left;

                definitions.Add(new RebarDefinition
                {
                    Curves = curves,
                    BarTypeName = request.VerticalBarTypeName,
                    Style = RebarStyle.Standard,
                    Label = "Main Bar (Circular)",
                    Comment = "Main Bar",
                    Normal = tangentNormal,
                    BarDiameter = verticalBarDia,
                    // Hook at bottom
                    HookStartName = hookBotName,
                    HookStartOrientation = hookBotOri,
                    // Hook at top only for top column (or single column)
                    HookEndName = isTopColumn ? hookTopName : null,
                    HookEndOrientation = hookTopOri
                });
            }

            return definitions;
        }

        // ═══════════════════════════════════════════════
        // Starter Bar Generation
        // ═══════════════════════════════════════════════
        private List<RebarDefinition> GenerateStarterBars(
            HostGeometry host, RebarRequest request,
            XYZ center, double rebarRadius,
            double starterLen, double topExt,
            double verticalBarDia)
        {
            var definitions = new List<RebarDefinition>();
            int count = request.PileBarCount;
            if (count < 1) return definitions;

            string bTypeName = request.StarterBarTypeName ?? request.VerticalBarTypeName;
            double bDia = GetBarDiameter(bTypeName);

            double zBase = host.SolidZMin;
            double sZstart = zBase - starterLen;
            double sZend = zBase + topExt;

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                double dx = rebarRadius * Math.Cos(angle);
                double dy = rebarRadius * Math.Sin(angle);

                XYZ pStart = new XYZ(center.X + dx, center.Y + dy, sZstart);
                XYZ pEnd = new XYZ(center.X + dx, center.Y + dy, sZend);

                XYZ vecToCenter = new XYZ(-dx, -dy, 0).Normalize();
                XYZ tangentNormal = XYZ.BasisZ.CrossProduct(vecToCenter).Normalize();

                definitions.Add(new RebarDefinition
                {
                    Curves = new List<Curve> { Line.CreateBound(pStart, pEnd) },
                    BarTypeName = bTypeName,
                    Style = RebarStyle.Standard,
                    Label = "Starter Bar (Circular)",
                    Comment = "Starter Bar",
                    Normal = tangentNormal,
                    BarDiameter = bDia,
                    HookStartName = request.StarterHookEndName,
                    HookStartOrientation = RebarHookOrientation.Right  // Bend outward (away from center)
                });
            }

            return definitions;
        }

        // ═══════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════
        private double GetHostRadius(HostGeometry host)
        {
            if (host.BoundaryCurves.Count > 0 && host.BoundaryCurves.Any(c => c is Arc))
            {
                var arcs = host.BoundaryCurves.OfType<Arc>().ToList();
                return arcs.Max(a => a.Radius);
            }
            return Math.Min(host.Width, host.Height) / 2.0;
        }

        private double GetBarDiameter(string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;
            var barType = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(x => x.Name.Equals(barTypeName, StringComparison.OrdinalIgnoreCase));
            if (barType == null) return 0;
            double d = barType.BarModelDiameter;
            if (d <= 0)
            {
                var p = barType.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                if (p != null) d = p.AsDouble();
            }
            return d;
        }

        private double GetLapSpliceLength(double barDia, RebarRequest request)
        {
            if (string.Equals(request.LapSpliceMode, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                return request.LapSpliceLength;
            }
            return LapSpliceCalculator.CalculateTensionLapLength(
                barDia, request.DesignCode, request.Grade,
                SteelGrade.Grade500E, BarPosition.Other);
        }

        private double DetermineStarterExtensionIntoFooting(FamilyInstance column, HostGeometry host)
        {
            XYZ pointBelow = host.Origin - host.HAxis * 0.1;

            var foundations = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var f in foundations)
            {
                BoundingBoxXYZ fBox = f.get_BoundingBox(null);
                if (fBox == null) continue;

                bool inFootprint = pointBelow.X >= fBox.Min.X && pointBelow.X <= fBox.Max.X &&
                                   pointBelow.Y >= fBox.Min.Y && pointBelow.Y <= fBox.Max.Y;
                bool onTop = Math.Abs(host.SolidZMin - fBox.Max.Z) < 0.2;

                if (inFootprint && onTop)
                {
                    double coverBot = GeometryUtils.GetCoverDistance(_doc, f, BuiltInParameter.CLEAR_COVER_BOTTOM);
                    double extension = host.SolidZMin - (fBox.Min.Z + coverBot);
                    if (extension > 0) return extension;
                }
            }
            return -1;
        }
        private double DetectSlabAboveExtension(FamilyInstance column, HostGeometry host)
        {
            double searchRadius = UnitConversion.MmToFeet(100);
            double colTopZ = host.SolidZMax;
            XYZ origin = host.Origin;

            XYZ searchMin = new XYZ(origin.X - searchRadius, origin.Y - searchRadius, colTopZ - 0.01);
            XYZ searchMax = new XYZ(origin.X + searchRadius, origin.Y + searchRadius, colTopZ + UnitConversion.MmToFeet(1000));

            try
            {
                Outline searchOutline = new Outline(searchMin, searchMax);
                var slabs = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .WherePasses(new BoundingBoxIntersectsFilter(searchOutline))
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var slab in slabs)
                {
                    if (slab.Id == column.Id) continue;
                    var bb = slab.get_BoundingBox(null);
                    if (bb == null) continue;

                    // Check slab bottom is near column top (within 60mm tolerance)
                    if (Math.Abs(bb.Min.Z - colTopZ) < UnitConversion.MmToFeet(60))
                    {
                        double cTop = GeometryUtils.GetCoverDistance(_doc, slab, BuiltInParameter.CLEAR_COVER_TOP);
                        double slabThickness = bb.Max.Z - bb.Min.Z;
                        // Extension = slab thickness - top cover - 40mm gap
                        double ext = slabThickness - cTop - UnitConversion.MmToFeet(40);
                        if (ext > 0) return ext;
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
