using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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

        public (int Processed, int Total) GenerateContinuousBeamRebar(
            List<FamilyInstance> spans, RebarRequest request)
        {
            int processedCount = 0;
            using (Transaction t = new Transaction(_doc, "Generate Continuous Beam Rebar"))
            {
                t.Start();
                try
                {
                    if (ProcessContinuousBeam(spans, request))
                        processedCount = spans.Count;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RebarEngine: Continuous beam failed: {ex.Message}");
                }
                t.Commit();
            }
            return (processedCount, spans.Count);
        }

        private bool ProcessContinuousBeam(List<FamilyInstance> spans, RebarRequest request)
        {
            if (spans == null || spans.Count == 0) return false;

            // Read geometry for all span beams
            var hostList = new List<(FamilyInstance beam, HostGeometry host)>();
            foreach (var span in spans)
            {
                var host = BeamGeometryModule.Read(_doc, span);
                if (host.Length <= 0 || host.Width <= 0 || host.Height <= 0) continue;
                hostList.Add((span, host));
            }
            if (hostList.Count == 0) return false;

            double transDia = 0;
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
                transDia = GetBarDiameter(request.TransverseBarTypeName);
            double minLayerGap = (request.LayerGap > 0) ? request.LayerGap : UnitConversion.MmToFeet(25);

            var firstBeam = hostList[0].beam;
            var firstHost = hostList[0].host;
            var lastHost = hostList[hostList.Count - 1].host;

            // === FIND TRUE OVERALL BEAM ENDPOINTS ===
            // Project all endpoints onto the first beam's LAxis to find the absolute extremes.
            // This prevents issues if chained beams were drawn in opposite directions.
            XYZ refDir = firstHost.LAxis;
            XYZ refPt = firstHost.StartPoint;
            
            XYZ trueStartPt = refPt;
            XYZ trueEndPt = refPt;
            double minProj = double.MaxValue;
            double maxProj = double.MinValue;

            foreach (var (_, host) in hostList)
            {
                double p1 = (host.StartPoint - refPt).DotProduct(refDir);
                double p2 = (host.EndPoint - refPt).DotProduct(refDir);
                
                if (p1 < minProj) { minProj = p1; trueStartPt = host.StartPoint; }
                if (p2 < minProj) { minProj = p2; trueStartPt = host.EndPoint; }
                if (p1 > maxProj) { maxProj = p1; trueEndPt = host.StartPoint; }
                if (p2 > maxProj) { maxProj = p2; trueEndPt = host.EndPoint; }
            }

            XYZ continuousDir = (trueEndPt - trueStartPt).Normalize();
            if (continuousDir.IsZeroLength()) continuousDir = firstHost.LAxis;

            // === DETECT ALL SUPPORTS & CLEAR SPANS ===
            // Scan the entire continuous axis from trueStartPt to trueEndPt
            var excludeIds = hostList.Select(x => x.beam.Id).ToList();
            double minZ = firstHost.SolidZMin - 2.0;
            double maxZ = firstHost.SolidZMax + 2.0;
            var allSupports = BeamSpanResolver.FindSupportsAlongLine(_doc, trueStartPt, trueEndPt, firstHost.Width, excludeIds, minZ, maxZ);

            // Override with UI-specified geometric supports if in BeamAdvance mode
            if (request.HostType == ElementHostType.BeamAdvance && request.SupportOverrides != null && request.SupportOverrides.Count > 0)
            {
                allSupports = request.SupportOverrides.Select(o => new BeamSpanResolver.SupportInfo
                {
                    CenterOffset = o.CenterOffset,
                    NearFaceOffset = o.NearFaceOffset,
                    FarFaceOffset = o.FarFaceOffset,
                    SupportWidth = o.SupportWidth,
                    IsEndSupport = o.IsEndSupport
                }).ToList();
            }

            // Determine end column extensions (bar extends to far face of end supports)
            // Use FindSupportExtension for BOTH single and multi-beam to accurately find far faces from cutback endpoints
            double startExtension = FindSupportExtension(_doc, trueStartPt, -continuousDir, excludeIds);
            double endExtension = FindSupportExtension(_doc, trueEndPt, continuousDir, excludeIds);

            // The full continuous bar origin and length (including extensions into end columns)
            XYZ barOriginPt = trueStartPt - continuousDir * startExtension;
            double totalLength = trueStartPt.DistanceTo(trueEndPt) + startExtension + endExtension;

            // Calculate clear spans natively for the entire continuous axis
            var clearSpans = new List<(double Start, double End)>();
            var intermediates = allSupports.Where(s => !s.IsEndSupport).OrderBy(s => s.CenterOffset).ToList();
            
            // Note: FindSupportsAlongLine offsets are relative to lineStart (trueStartPt)
            // Absolute coordinates relative to barOriginPt are offset + startExtension
            double prevEnd = startExtension;
            foreach (var sup in intermediates)
            {
                // Verify the support actually cuts through the beam logic
                double currentStart = startExtension + sup.NearFaceOffset;
                if (currentStart > prevEnd) clearSpans.Add((prevEnd, currentStart));
                prevEnd = startExtension + sup.FarFaceOffset;
            }
            double finalEnd = totalLength - endExtension;
            if (finalEnd > prevEnd) clearSpans.Add((prevEnd, finalEnd));

            // Failsafe if spans are somehow crushed
            if (clearSpans.Count == 0) clearSpans.Add((startExtension, totalLength - endExtension));

            // Detect cantilever: if the last support's far face is not at the true end of the beam line
            double baseLength = trueStartPt.DistanceTo(trueEndPt);
            bool isStartCantilever = allSupports.Count > 0 && allSupports[0].NearFaceOffset > 0.1;
            bool isEndCantilever = allSupports.Count > 0 && allSupports.Last().FarFaceOffset < baseLength - 0.1;

            if (request.SupportOverrides != null && request.SupportOverrides.Count > 0)
            {
                isStartCantilever = request.SupportOverrides.First().IsCantilever;
                isEndCantilever = request.SupportOverrides.Last().IsCantilever;
            }

            // === FIND TRUE OVERALL BEAM ENVELOPE ===
            // For continuous bars to stay inside ALL spans, we need the "inner" envelope:
            // Top bars must stay below the LOWEST top face in the chain.
            // Bottom bars must stay above the HIGHEST bottom face.
            double minZMax = double.MaxValue;
            double maxZMin = double.MinValue;
            foreach (var (_, host) in hostList)
            {
                if (host.SolidZMax < minZMax) minZMax = host.SolidZMax;
                if (host.SolidZMin > maxZMin) maxZMin = host.SolidZMin;
            }

            // Cover at each end (inside the support column far face)
            double coverStart = firstHost.CoverOther;
            double coverEnd = firstHost.CoverOther;
            double barLen = totalLength - coverStart - coverEnd;

            // Shift full clear spans relative to the bar's internal coordinate 0 (which starts at coverStart)
            // AND ensure they are capped within [0, barLen]
            var shiftedSpans = clearSpans
                .Select(s => (Math.Max(0, s.Start - coverStart), Math.Min(barLen, s.End - coverStart)))
                .Where(s => s.Item2 > s.Item1 + 0.001)
                .ToList();

            // === 1. PER-SPAN STIRRUPS ===
            // Delete existing rebar for ALL host elements to prevent duplicates
            if (request.RemoveExisting)
            {
                foreach (var (b, _) in hostList) _creationService.DeleteExistingRebar(b);
            }

            var rebarByHost = new System.Collections.Generic.Dictionary<Element, List<RebarDefinition>>();
            foreach (var (beam, _) in hostList) rebarByHost[beam] = new List<RebarDefinition>();

            void AssignToBestHost(double relMidPoint, RebarDefinition def)
            {
                XYZ mid3D = barOriginPt + continuousDir * relMidPoint;
                Element best = firstBeam;
                double minDist = double.MaxValue;
                foreach (var (beam, host) in hostList)
                {
                    XYZ bc = (host.StartPoint + host.EndPoint) / 2.0;
                    double d = mid3D.DistanceTo(bc);
                    if (d < minDist) { minDist = d; best = beam; }
                }
                rebarByHost[best].Add(def);
            }

            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                foreach (var spanBound in clearSpans)
                {
                    double spanLen = spanBound.End - spanBound.Start;
                    if (spanLen <= UnitConversion.MmToFeet(100)) continue;

                    // Find the host beam that best represents this span to get correct Z-bounds/Width
                    XYZ spanMid = barOriginPt + continuousDir * ((spanBound.Start + spanBound.End) / 2.0);
                    HostGeometry spanHost = firstHost;
                    double minDist = double.MaxValue;
                    foreach (var (_, h) in hostList) {
                        double d = spanMid.DistanceTo((h.StartPoint + h.EndPoint) / 2.0);
                        if (d < minDist) { minDist = d; spanHost = h; }
                    }

                    double zMin = spanHost.SolidZMin;
                    double zMax = spanHost.SolidZMax;
                    double stW = spanHost.Width - 2 * spanHost.CoverOther;
                    double stH = spanHost.Height - spanHost.CoverTop - spanHost.CoverBottom;
                    double hCenterOff = (spanHost.CoverBottom - spanHost.CoverTop) / 2.0;

                    double offset = request.TransverseStartOffset;
                    if (offset > spanLen / 3.0) offset = spanLen / 6.0;

                    if (request.EnableZoneSpacing)
                    {
                        var zones = ZoneSpacingCalculator.CalculateBeamZones(
                            spanLen, spanHost.Height, request.TransverseSpacing,
                            offset, request.DesignCode);

                        foreach (var zone in zones)
                        {
                            double arrLen = zone.EndOffset - zone.StartOffset;
                            if (arrLen <= 0 || zone.Spacing <= 0) continue;

                            XYZ xyOrigin = barOriginPt + continuousDir * (spanBound.Start + zone.StartOffset);
                            XYZ stirrupOrigin = new XYZ(xyOrigin.X, xyOrigin.Y, (zMax + zMin) / 2.0);
                            var curves = StirrupLayoutGenerator.CreateStirrupLoopFlat(
                                stirrupOrigin, spanHost.WAxis, stW, stH, hCenterOff);

                            AssignToBestHost(spanBound.Start + (zone.StartOffset + zone.EndOffset) / 2.0, new RebarDefinition
                            {
                                Curves = curves,
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.StirrupTie,
                                BarTypeName = request.TransverseBarTypeName,
                                BarDiameter = transDia,
                                Spacing = zone.Spacing,
                                ArrayLength = arrLen,
                                ArrayDirection = spanHost.LAxis,
                                Normal = spanHost.LAxis,
                                HookStartName = request.TransverseHookStartName,
                                HookEndName = request.TransverseHookEndName,
                                Label = $"Stirrup ({zone.Label})"
                            });
                        }
                    }
                    else
                    {
                        // Uniform spacing per virtual span
                        XYZ xyOrigin = barOriginPt + continuousDir * (spanBound.Start + offset);
                        XYZ stirrupOrigin = new XYZ(xyOrigin.X, xyOrigin.Y, (zMax + zMin) / 2.0);
                        var curves = StirrupLayoutGenerator.CreateStirrupLoopFlat(
                            stirrupOrigin, spanHost.WAxis, stW, stH, hCenterOff);

                        double arrLen = spanLen - 2 * offset;
                        if (arrLen > 0)
                        {
                            AssignToBestHost((spanBound.Start + spanBound.End) / 2.0, new RebarDefinition
                            {
                                Curves = curves,
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.StirrupTie,
                                BarTypeName = request.TransverseBarTypeName,
                                BarDiameter = transDia,
                                Spacing = request.TransverseSpacing,
                                ArrayLength = arrLen,
                                ArrayDirection = spanHost.LAxis,
                                Normal = spanHost.LAxis,
                                HookStartName = request.TransverseHookStartName,
                                HookEndName = request.TransverseHookEndName,
                                Label = "Stirrup"
                            });
                        }
                    }
                }
            }

            // === 2. LONGITUDINAL BARS (using overall inner envelope) ===
            double distWidth = firstHost.Width - 2 * firstHost.CoverOther - 2 * transDia; // Basic reference width

            // Top layers
            double topZ = minZMax - firstHost.CoverTop - transDia;
            int topLayerIdx = 0;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0))
            {
                if (request.HostType == ElementHostType.BeamAdvance && !layer.IsContinuous)
                    continue; // Skip global additional bars; handled by distinct SupportOverrides instead.

                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = topZ - barDia / 2.0;

                // Split into segments based on whether it is a continuous or additional bar
                List<(double Start, double End)> segments;

                if (layer.IsContinuous)
                {
                    segments = request.EnableLapSplice
                        ? LapSpliceCalculator.SplitContinuousBarForLap(barLen, barDia, request.DesignCode, true, topLayerIdx, shiftedSpans)
                        : new List<(double Start, double End)> { (0.0, barLen) };
                }
                else
                {
                    // Hogging (Top Additional) Bars
                    segments = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.CalculateTopAdditionalSegments(barLen, shiftedSpans, isStartCantilever, isEndCantilever);
                }

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    double innerOffset = firstHost.CoverOther + transDia + barDia / 2.0;
                    double distWidthL = firstHost.Width - 2 * innerOffset;

                    XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                    XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidthL / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidthL / 2.0);

                    var curves = new List<Curve>();
                    if (layer.IsContinuous && si > 0 && segments.Count > 1)
                    {
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = GetLapSpliceLength(barDia, request, BarPosition.Top);
                        // Add 2x barDia visual allowance so the crank bend radius physically matches the straight bar tip
                        double straightLap = lapLen + crankRun + barDia * 2.0;

                        XYZ crankDir = -firstHost.HAxis;
                        XYZ ptA = barStart + crankDir * crankOff;
                        XYZ ptB = ptA + continuousDir * straightLap;
                        XYZ ptC = barStart + continuousDir * (straightLap + crankRun);

                        curves.Add(Line.CreateBound(ptA, ptB));
                        curves.Add(Line.CreateBound(ptB, ptC));
                        curves.Add(Line.CreateBound(ptC, barEnd));
                    }
                    else
                    {
                        curves.Add(Line.CreateBound(barStart, barEnd));
                    }

                    AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                    {
                        Curves = curves,
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        FixedCount = count,
                        DistributionWidth = distWidthL,
                        ArrayDirection = firstHost.WAxis,
                        Normal = firstHost.WAxis,
                        HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                        HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                        HookStartName = (Math.Abs(seg.Start - 0) < 0.001) ? layer.HookStartName : null,
                        HookEndName = (Math.Abs(seg.End - barLen) < 0.001) ? layer.HookEndName : null,
                        OverrideHookLength = layer.OverrideHookLength,
                        HookLengthOverride = layer.HookLengthOverride,
                        Label = layer.IsContinuous ? (segments.Count > 1 ? "Top Continuous (lapped)" : "Top Continuous") : "Top Additional (Hogging)",
                        Comment = (topLayerIdx == 0) ? "Top Bar" : (topLayerIdx == 1 ? "Top T2" : (topLayerIdx == 2 ? "Top T3" : "Top Bar"))
                    });
                }

                // Effective clear gap: at least the user setting, but not less than the bar diameter (code requirement for db > 25mm)
                double effectiveGap = Math.Max(minLayerGap, barDia);
                topZ -= (barDia + effectiveGap);
                topLayerIdx++;
            }

            // --- 2a-Override. BEAM ADVANCE TOP BARS (T2, T3) ---
            if (request.HostType == ElementHostType.BeamAdvance && request.SupportOverrides != null)
            {
                // Identify global T2/T3 placeholders from standard Beam settings
                var topLayers = request.Layers.Where(l => (l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0) && !l.IsContinuous).ToList();
                var globalT2 = topLayers.ElementAtOrDefault(0);
                var globalT3 = topLayers.ElementAtOrDefault(1);

                // Identify first continuous layer for hook template
                var t1Layer = request.Layers.FirstOrDefault(l => (l.Face == RebarLayerFace.Exterior || l.VerticalOffset > 0) && l.IsContinuous);

                // T2 Layer
                if ((request.SupportOverrides.Any(o => o.T2_Count > 0) || globalT2 != null))
                {
                    double maxT2Dia = globalT2 != null ? GetBarDiameter(globalT2.VerticalBarTypeName) : 0;
                    if (request.SupportOverrides.Any(o => o.T2_Count > 0))
                        maxT2Dia = Math.Max(maxT2Dia, GetBarDiameter(request.SupportOverrides.First(o => o.T2_Count > 0).T2_BarTypeName));
                    
                    if (maxT2Dia > 0)
                    {
                        double z = topZ - maxT2Dia / 2.0;
                        for (int i = 0; i < request.SupportOverrides.Count; i++)
                        {
                            var over = request.SupportOverrides[i];
                            int count = over.T2_Count;
                            string barType = over.T2_BarTypeName;

                            // Fallback to global if 0
                            if (count <= 0 && globalT2 != null)
                            {
                                count = (int)globalT2.VerticalSpacing;
                                barType = globalT2.VerticalBarTypeName;
                            }

                            if (count <= 0 || string.IsNullOrEmpty(barType)) continue;

                            var segOpt = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetTopSegmentForSupport(i, barLen, shiftedSpans, isStartCantilever, isEndCantilever);
                            if (!segOpt.HasValue) continue;

                            var seg = segOpt.Value;
                            double outDia = GetBarDiameter(barType);

                            XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                            XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                            XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidth / 2.0);
                            XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidth / 2.0);

                            AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                            {
                                Curves = new List<Curve> { Line.CreateBound(barStart, barEnd) },
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                                BarTypeName = barType,
                                BarDiameter = outDia,
                                FixedCount = count,
                                DistributionWidth = distWidth,
                                ArrayDirection = firstHost.WAxis,
                                Normal = firstHost.WAxis,
                                HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                                HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                                // Apply Hooks at End Supports
                                HookStartName = (t1Layer != null && Math.Abs(seg.Start - 0) < 0.001) ? t1Layer.HookStartName : null,
                                HookEndName = (t1Layer != null && Math.Abs(seg.End - barLen) < 0.01) ? t1Layer.HookEndName : null,
                                Label = $"T2 @ {over.SupportName}",
                                Comment = "Top T2"
                            });
                        }
                        topZ -= (maxT2Dia + Math.Max(minLayerGap, maxT2Dia));
                    }
                }

                // T3 Layer
                if ((request.SupportOverrides.Any(o => o.T3_Count > 0) || globalT3 != null))
                {
                    double maxT3Dia = globalT3 != null ? GetBarDiameter(globalT3.VerticalBarTypeName) : 0;
                    if (request.SupportOverrides.Any(o => o.T3_Count > 0))
                        maxT3Dia = Math.Max(maxT3Dia, GetBarDiameter(request.SupportOverrides.First(o => o.T3_Count > 0).T3_BarTypeName));

                    if (maxT3Dia > 0)
                    {
                        double z = topZ - maxT3Dia / 2.0;
                        for (int i = 0; i < request.SupportOverrides.Count; i++)
                        {
                            var over = request.SupportOverrides[i];
                            int count = over.T3_Count;
                            string barType = over.T3_BarTypeName;

                            // Fallback to global if 0
                            if (count <= 0 && globalT3 != null)
                            {
                                count = (int)globalT3.VerticalSpacing;
                                barType = globalT3.VerticalBarTypeName;
                            }

                            if (count <= 0 || string.IsNullOrEmpty(barType)) continue;

                            var segOpt = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetTopSegmentForSupport(i, barLen, shiftedSpans, isStartCantilever, isEndCantilever);
                            if (!segOpt.HasValue) continue;

                            var seg = segOpt.Value;
                            double outDia = GetBarDiameter(barType);

                            XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                            XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                            XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidth / 2.0);
                            XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidth / 2.0);

                            AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                            {
                                Curves = new List<Curve> { Line.CreateBound(barStart, barEnd) },
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                                BarTypeName = barType,
                                BarDiameter = outDia,
                                FixedCount = count,
                                DistributionWidth = distWidth,
                                ArrayDirection = firstHost.WAxis,
                                Normal = firstHost.WAxis,
                                HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                                HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Left,
                                // Apply Hooks at End Supports
                                HookStartName = (t1Layer != null && Math.Abs(seg.Start - 0) < 0.001) ? t1Layer.HookStartName : null,
                                HookEndName = (t1Layer != null && Math.Abs(seg.End - barLen) < 0.01) ? t1Layer.HookEndName : null,
                                Label = $"T3 @ {over.SupportName}",
                                Comment = "Top T3"
                            });
                        }
                        topZ -= (maxT3Dia + Math.Max(minLayerGap, maxT3Dia));
                    }
                }
            }

            // --- 2b. CONTINUOUS BOTTOM BARS (B1, B2) ---
            double botZ = maxZMin + firstHost.CoverBottom + transDia;
            int botLayerIdx = 0;
            foreach (var layer in request.Layers.Where(l =>
                l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0))
            {
                if (request.HostType == ElementHostType.BeamAdvance && !layer.IsContinuous)
                    continue; // Skip global B2/B3, handled below

                double barDia = GetBarDiameter(layer.VerticalBarTypeName);
                int count = (int)(layer.VerticalSpacing);
                if (count < 1) continue;

                double z = botZ + barDia / 2.0;

                List<(double Start, double End)> segments;

                if (layer.IsContinuous)
                {
                    segments = request.EnableLapSplice
                        ? LapSpliceCalculator.SplitContinuousBarForLap(barLen, barDia, request.DesignCode, false, botLayerIdx, shiftedSpans)
                        : new List<(double Start, double End)> { (0.0, barLen) };
                }
                else
                {
                    // Sagging (Bottom Additional) Bars
                    // B2 is typical the 2nd layer (botLayerIdx == 1). Extend if it's B2.
                    bool isB2Standard = (botLayerIdx == 1);
                    segments = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.CalculateBottomAdditionalSegments(shiftedSpans, isStartCantilever, isEndCantilever, isB2Standard);
                }

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    double innerOffset = firstHost.CoverOther + transDia + barDia / 2.0;
                    double distWidthL = firstHost.Width - 2 * innerOffset;

                    XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                    XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidthL / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidthL / 2.0);

                    var curves = new List<Curve>();
                    if (layer.IsContinuous && si > 0 && segments.Count > 1)
                    {
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, antiGGGravity.StructuralRebar.Constants.ConcreteGrade.C30, antiGGGravity.StructuralRebar.Constants.SteelGrade.Grade500E, antiGGGravity.StructuralRebar.Constants.BarPosition.Bottom);
                        // Add 2x barDia visual allowance so the crank bend radius physically matches the straight bar tip
                        double straightLap = lapLen + crankRun + barDia * 2.0;

                        XYZ crankDir = firstHost.HAxis;
                        XYZ ptA = barStart + crankDir * crankOff;
                        XYZ ptB = ptA + continuousDir * straightLap;
                        XYZ ptC = barStart + continuousDir * (straightLap + crankRun);

                        curves.Add(Line.CreateBound(ptA, ptB));
                        curves.Add(Line.CreateBound(ptB, ptC));
                        curves.Add(Line.CreateBound(ptC, barEnd));
                    }
                    else
                    {
                        curves.Add(Line.CreateBound(barStart, barEnd));
                    }

                    AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                    {
                        Curves = curves,
                        Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                        BarTypeName = layer.VerticalBarTypeName,
                        BarDiameter = barDia,
                        FixedCount = count,
                        DistributionWidth = distWidthL,
                        ArrayDirection = firstHost.WAxis,
                        Normal = firstHost.WAxis,
                        HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                        HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                        HookStartName = (Math.Abs(seg.Start - 0) < 0.001) ? layer.HookStartName : null,
                        HookEndName = (Math.Abs(seg.End - barLen) < 0.001) ? layer.HookEndName : null,
                        OverrideHookLength = layer.OverrideHookLength,
                        HookLengthOverride = layer.HookLengthOverride,
                        Label = layer.IsContinuous ? (segments.Count > 1 ? "Btm Continuous (lapped)" : "Btm Continuous") : "Btm Continuous",
                        Comment = (botLayerIdx == 0) ? "Btm Bar" : (botLayerIdx == 1 ? "Btm B2" : (botLayerIdx == 2 ? "Btm B3" : "Btm Bar"))
                    });
                }

                // Effective clear gap: at least the user setting, but not less than the bar diameter (code requirement for db > 25mm)
                double effectiveGap = Math.Max(minLayerGap, barDia);
                botZ += (barDia + effectiveGap);
                botLayerIdx++;
            }

            // --- 2b-Override. BEAM ADVANCE BOTTOM BARS (B2, B3) ---
            if (request.HostType == ElementHostType.BeamAdvance && request.SpanOverrides != null)
            {
                // Identify global B2/B3 placeholders from standard Beam settings
                var botLayers = request.Layers.Where(l => (l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0) && !l.IsContinuous).ToList();
                var globalB2 = botLayers.ElementAtOrDefault(0);
                var globalB3 = botLayers.ElementAtOrDefault(1);

                // Identify first continuous bottom layer (B1) for hook template
                var b1Layer = request.Layers.FirstOrDefault(l => (l.Face == RebarLayerFace.Interior || l.VerticalOffset < 0) && l.IsContinuous);

                // B2 Layer
                if ((request.SpanOverrides.Any(o => o.B2_Count > 0) || globalB2 != null))
                {
                    double maxB2Dia = globalB2 != null ? GetBarDiameter(globalB2.VerticalBarTypeName) : 0;
                    if (request.SpanOverrides.Any(o => o.B2_Count > 0))
                        maxB2Dia = Math.Max(maxB2Dia, GetBarDiameter(request.SpanOverrides.First(o => o.B2_Count > 0).B2_BarTypeName));

                    if (maxB2Dia > 0)
                    {
                        double z = botZ + maxB2Dia / 2.0;
                        for (int i = 0; i < request.SpanOverrides.Count; i++)
                        {
                            var over = request.SpanOverrides[i];
                            int count = over.B2_Count;
                            string barType = over.B2_BarTypeName;

                            // Fallback to global if 0
                            if (count <= 0 && globalB2 != null)
                            {
                                count = (int)globalB2.VerticalSpacing;
                                barType = globalB2.VerticalBarTypeName;
                            }

                            if (count <= 0 || string.IsNullOrEmpty(barType)) continue;

                            int targetSpanIdx = isStartCantilever ? i + 1 : i;
                            var segOpt = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetBottomSegmentForSpan(targetSpanIdx, shiftedSpans, isStartCantilever, isEndCantilever, true);
                            if (!segOpt.HasValue) continue;

                            var seg = segOpt.Value;
                            double outDia = GetBarDiameter(barType);

                            XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                            XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                            XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidth / 2.0);
                            XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidth / 2.0);

                            AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                            {
                                Curves = new List<Curve> { Line.CreateBound(barStart, barEnd) },
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                                BarTypeName = barType,
                                BarDiameter = outDia,
                                FixedCount = count,
                                DistributionWidth = distWidth,
                                ArrayDirection = firstHost.WAxis,
                                Normal = firstHost.WAxis,
                                HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                                HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                                // Apply Hooks at End Supports (inherit from B1)
                                HookStartName = (b1Layer != null && Math.Abs(seg.Start - 0) < 0.001) ? b1Layer.HookStartName : null,
                                HookEndName = (b1Layer != null && Math.Abs(seg.End - barLen) < 0.01) ? b1Layer.HookEndName : null,
                                Label = $"B2 @ {over.SpanName}",
                                Comment = "Btm B2"
                            });
                        }
                        botZ += (maxB2Dia + Math.Max(minLayerGap, maxB2Dia));
                    }
                }

                // B3 Layer
                if ((request.SpanOverrides.Any(o => o.B3_Count > 0) || globalB3 != null))
                {
                    double maxB3Dia = globalB3 != null ? GetBarDiameter(globalB3.VerticalBarTypeName) : 0;
                    if (request.SpanOverrides.Any(o => o.B3_Count > 0))
                        maxB3Dia = Math.Max(maxB3Dia, GetBarDiameter(request.SpanOverrides.First(o => o.B3_Count > 0).B3_BarTypeName));

                    if (maxB3Dia > 0)
                    {
                        double z = botZ + maxB3Dia / 2.0;
                        for (int i = 0; i < request.SpanOverrides.Count; i++)
                        {
                            var over = request.SpanOverrides[i];
                            int count = over.B3_Count;
                            string barType = over.B3_BarTypeName;

                            // Fallback to global if 0
                            if (count <= 0 && globalB3 != null)
                            {
                                count = (int)globalB3.VerticalSpacing;
                                barType = globalB3.VerticalBarTypeName;
                            }

                            if (count <= 0 || string.IsNullOrEmpty(barType)) continue;

                            int targetSpanIdx = isStartCantilever ? i + 1 : i;
                            var segOpt = antiGGGravity.StructuralRebar.Core.Calculators.AdditionalBarCalculator.GetBottomSegmentForSpan(targetSpanIdx, shiftedSpans, isStartCantilever, isEndCantilever, false);
                            if (!segOpt.HasValue) continue;

                            var seg = segOpt.Value;
                            double outDia = GetBarDiameter(barType);

                            double innerOffset = firstHost.CoverOther + transDia + outDia / 2.0;
                            double distWidthL = firstHost.Width - 2 * innerOffset;

                            XYZ s = barOriginPt + continuousDir * (coverStart + seg.Start);
                            XYZ e = barOriginPt + continuousDir * (coverStart + seg.End);
                            XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidthL / 2.0);
                            XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidthL / 2.0);

                            AssignToBestHost(coverStart + (seg.Start + seg.End) / 2.0, new RebarDefinition
                            {
                                Curves = new List<Curve> { Line.CreateBound(barStart, barEnd) },
                                Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                                BarTypeName = barType,
                                BarDiameter = outDia,
                                FixedCount = count,
                                DistributionWidth = distWidthL,
                                ArrayDirection = firstHost.WAxis,
                                Normal = firstHost.WAxis,
                                HookStartOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                                HookEndOrientation = Autodesk.Revit.DB.Structure.RebarHookOrientation.Right,
                                // Apply Hooks at End Supports (inherit from B1)
                                HookStartName = (b1Layer != null && Math.Abs(seg.Start - 0) < 0.001) ? b1Layer.HookStartName : null,
                                HookEndName = (b1Layer != null && Math.Abs(seg.End - barLen) < 0.01) ? b1Layer.HookEndName : null,
                                Label = $"B3 @ {over.SpanName}",
                                Comment = "Btm B3"
                            });
                        }
                        botZ += (maxB3Dia + Math.Max(minLayerGap, maxB3Dia));
                    }
                }
            }

            // --- 2c. CONTINUOUS SIDE BARS (optional) ---
            if (request.EnableSideRebar && request.SideRebarRows > 0 && !string.IsNullOrEmpty(request.SideRebarTypeName))
            {
                double sideDia = GetBarDiameter(request.SideRebarTypeName);
                int rows = request.SideRebarRows;

                // Use overall safe envelope for available side-bar height
                double sideZTop = minZMax - firstHost.CoverTop - transDia - sideDia;
                double sideZBot = maxZMin + firstHost.CoverBottom + transDia + sideDia;
                double availableHeight = sideZTop - sideZBot;
                
                if (availableHeight > 0)
                {
                    double rowSpacing = availableHeight / (rows + 1);
                    double innerOffset = firstHost.CoverOther + transDia + sideDia / 2.0;
                    double distWidthSide = firstHost.Width - 2 * innerOffset;

                    for (int row = 1; row <= rows; row++)
                    {
                        double z = sideZBot + rowSpacing * row;

                        XYZ s = barOriginPt + continuousDir * coverStart;
                        XYZ e = barOriginPt + continuousDir * (coverStart + barLen);
                        XYZ barStart = new XYZ(s.X, s.Y, z) - firstHost.WAxis * (distWidthSide / 2.0);
                        XYZ barEnd = new XYZ(e.X, e.Y, z) - firstHost.WAxis * (distWidthSide / 2.0);

                        AssignToBestHost(coverStart + barLen / 2.0, new RebarDefinition
                        {
                            Curves = new List<Curve> { Line.CreateBound(barStart, barEnd) },
                            Style = Autodesk.Revit.DB.Structure.RebarStyle.Standard,
                            BarTypeName = request.SideRebarTypeName,
                            BarDiameter = sideDia,
                            FixedCount = 2,
                            DistributionWidth = distWidthSide,
                            ArrayDirection = firstHost.WAxis,
                            Normal = firstHost.WAxis,
                            Label = "Side Rebar",
                            Comment = "Side Bar"
                        });
                    }
                }
            }

            // Place all continuous bars and stirrups onto their dynamically assigned hosts
            foreach (var kvp in rebarByHost)
            {
                if (kvp.Value.Count > 0)
                    _creationService.PlaceRebar(kvp.Key, kvp.Value);
            }

            return true;
        }

        /// <summary>
        /// Finds the extension distance from a beam endpoint to the far face of the
        /// supporting column or wall at that point. Searches along the given direction.
        /// Returns 0 if no support is found.
        /// </summary>
        private double FindSupportExtension(Document doc, XYZ beamEndPoint, XYZ searchDir, ICollection<ElementId> excludeIds)
        {
            double tolerance = UnitConversion.MmToFeet(100); // 100mm search radius

            // Search for columns near the beam endpoint
            var columns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var col in columns)
            {
                BoundingBoxXYZ bbox = col.get_BoundingBox(null);
                if (bbox == null) continue;

                // Check if beam endpoint is near the column (XY proximity)
                XYZ colCenter = (bbox.Min + bbox.Max) / 2.0;
                double dx = Math.Abs(beamEndPoint.X - colCenter.X);
                double dy = Math.Abs(beamEndPoint.Y - colCenter.Y);
                double halfW = (bbox.Max.X - bbox.Min.X) / 2.0;
                double halfD = (bbox.Max.Y - bbox.Min.Y) / 2.0;

                if (dx > halfW + tolerance || dy > halfD + tolerance) continue;

                // Found a column at this beam end — compute distance to far face
                // Project the bounding box onto the search direction
                XYZ[] corners = {
                    new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Max.Y, 0),
                    new XYZ(bbox.Min.X, bbox.Max.Y, 0)
                };

                double maxProjection = double.MinValue;
                XYZ beamEnd2D = new XYZ(beamEndPoint.X, beamEndPoint.Y, 0);
                foreach (var corner in corners)
                {
                    double proj = (corner - beamEnd2D).DotProduct(searchDir);
                    if (proj > maxProjection) maxProjection = proj;
                }

                // Extension = distance from beam face to far face of column
                // (maxProjection is the distance from beam endpoint to the farthest column corner in the search direction)
                if (maxProjection > 0)
                    return maxProjection;
            }

            // Also search for walls
            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            foreach (var wall in walls)
            {
                BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                if (bbox == null) continue;

                XYZ wallCenter = (bbox.Min + bbox.Max) / 2.0;
                double dx = Math.Abs(beamEndPoint.X - wallCenter.X);
                double dy = Math.Abs(beamEndPoint.Y - wallCenter.Y);
                double halfW = (bbox.Max.X - bbox.Min.X) / 2.0;
                double halfD = (bbox.Max.Y - bbox.Min.Y) / 2.0;

                if (dx > halfW + tolerance || dy > halfD + tolerance) continue;

                XYZ[] corners = {
                    new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Max.Y, 0),
                    new XYZ(bbox.Min.X, bbox.Max.Y, 0)
                };

                double maxProjection = double.MinValue;
                XYZ beamEnd2D = new XYZ(beamEndPoint.X, beamEndPoint.Y, 0);
                foreach (var corner in corners)
                {
                    double proj = (corner - beamEnd2D).DotProduct(searchDir);
                    if (proj > maxProjection) maxProjection = proj;
                }

                if (maxProjection > 0)
                    return maxProjection;
            }

            // Also search for Primary Beams (Structural Framing)
            var framing = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var fr in framing)
            {
                // Skip the beams we are currently reinforcing
                if (excludeIds != null && excludeIds.Contains(fr.Id)) continue;

                BoundingBoxXYZ bbox = fr.get_BoundingBox(null);
                if (bbox == null) continue;

                // Simple proximity check for beam end
                XYZ center = (bbox.Min + bbox.Max) / 2.0;
                double dx = Math.Abs(beamEndPoint.X - center.X);
                double dy = Math.Abs(beamEndPoint.Y - center.Y);
                double halfW = (bbox.Max.X - bbox.Min.X) / 2.0;
                double halfD = (bbox.Max.Y - bbox.Min.Y) / 2.0;

                if (dx > halfW + tolerance || dy > halfD + tolerance) continue;

                // Found a framing member at this beam end — compute distance to far face
                XYZ[] corners = {
                    new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Min.Y, 0),
                    new XYZ(bbox.Max.X, bbox.Max.Y, 0),
                    new XYZ(bbox.Min.X, bbox.Max.Y, 0)
                };

                double maxProjection = double.MinValue;
                XYZ beamEnd2D = new XYZ(beamEndPoint.X, beamEndPoint.Y, 0);
                foreach (var corner in corners)
                {
                    double proj = (corner - beamEnd2D).DotProduct(searchDir);
                    if (proj > maxProjection) maxProjection = proj;
                }

                if (maxProjection > 0)
                    return maxProjection;
            }

            return 0; // No support found — stop at beam face
        }

        public (int Processed, int Total) GenerateWallRebar(List<Wall> walls, RebarRequest request)
        {
            var engine = new WallEngine(_doc);
            return engine.GenerateWallRebar(walls, request);
        }

        public (int Processed, int Total) GenerateWallStackRebar(List<Wall> stack, RebarRequest request)
        {
            var engine = new WallEngine(_doc);
            return engine.GenerateWallStackRebar(stack, request);
        }

        public (int Processed, int Total) GenerateColumnRebar(
            List<FamilyInstance> columns, RebarRequest request)
        {
            return new ColumnEngine(_doc).GenerateColumnRebar(columns, request);
        }

        public (int Processed, int Total) GenerateColumnStackRebar(
            List<FamilyInstance> stack, RebarRequest request)
        {
            return new ColumnEngine(_doc).GenerateColumnStackRebar(stack, request);
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

        public (int Processed, int Total) GeneratePadShapeRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Pad Shape Rebar");
        }

        public (int Processed, int Total) GenerateBoredPileRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Bored Pile Rebar");
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
                            success = new WallEngine(_doc).Execute(wall, request);
                        else if (element is FamilyInstance col && request.HostType == ElementHostType.Column)
                            success = new ColumnEngine(_doc).Execute(col, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.StripFooting)
                            success = new FootingEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.FootingPad)
                            success = new FootingEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.PadShape)
                            success = new PadShapeEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.BoredPile)
                            success = new BoredPileEngine(_doc).Execute(element, request);

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

            double minLayerGap = (request.LayerGap > 0) ? request.LayerGap : UnitConversion.MmToFeet(25);

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
            // Z bounds — already validated against parametric height by BeamGeometryModule
            double zMin = host.SolidZMin;
            double zMax = host.SolidZMax;

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

            // Determine end column extensions (longitudinal bars extend to far face of end supports)
            var excludeIds = new List<ElementId> { beam.Id };
            double startExtension = FindSupportExtension(_doc, host.StartPoint, -host.LAxis, excludeIds);
            double endExtension = FindSupportExtension(_doc, host.EndPoint, host.LAxis, excludeIds);

            // Full longitudinal bar length and origin for this beam element
            XYZ barOriginPt = host.StartPoint - host.LAxis * startExtension;
            double barTotalLen = host.Length + startExtension + endExtension;

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
                double barLen = barTotalLen - 2 * host.CoverOther;
                var segments = request.EnableLapSplice 
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: true, layerIndex: topLayerIdx, customLapLen: GetLapSpliceLength(barDia, request, BarPosition.Top))
                    : new List<(double Start, double End)> { (0.0, barLen) };

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    double innerOffset = host.CoverOther + transDia + barDia / 2.0;
                    double distWidthSeg = host.Width - 2 * innerOffset;

                    XYZ s = barOriginPt + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = barOriginPt + host.LAxis * (host.CoverOther + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    // Build curves: cranked start for segments after the first
                    var curves = new List<Curve>();
                    if (si > 0 && segments.Count > 1)
                    {
                        // Cranked bar: straight at offset → angled 1:6 → straight at main
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, request.Grade, antiGGGravity.StructuralRebar.Constants.SteelGrade.Grade500E, antiGGGravity.StructuralRebar.Constants.BarPosition.Top);
                        // Add 2x barDia visual allowance so the crank bend radius physically matches the straight bar tip
                        double straightLap = lapLen + crankRun + barDia * 2.0;

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
                        Comment = (topLayerIdx == 0) ? "Top Bar" : (topLayerIdx == 1 ? "Top T2" : (topLayerIdx == 2 ? "Top T3" : "Top Bar"))
                    };
                    definitions.Add(segDef);
                }

                // Effective clear gap: at least the user setting, but not less than the bar diameter (code requirement for db > 25mm)
                double effectiveGap = Math.Max(minLayerGap, barDia);
                topZ -= (barDia + effectiveGap);
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
                double barLen = barTotalLen - 2 * host.CoverOther;
                var segments = request.EnableLapSplice 
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: false, layerIndex: botLayerIdx, customLapLen: GetLapSpliceLength(barDia, request, BarPosition.Bottom))
                    : new List<(double Start, double End)> { (0.0, barLen) };

                for (int si = 0; si < segments.Count; si++)
                {
                    var seg = segments[si];
                    double innerOffset = host.CoverOther + transDia + barDia / 2.0;
                    double distWidthSeg = host.Width - 2 * innerOffset;

                    XYZ s = barOriginPt + host.LAxis * (host.CoverOther + seg.Start);
                    XYZ e = barOriginPt + host.LAxis * (host.CoverOther + seg.End);
                    XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidthSeg / 2.0);
                    XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidthSeg / 2.0);

                    // Build curves: cranked start for segments after the first
                    var curves = new List<Curve>();
                    if (si > 0 && segments.Count > 1)
                    {
                        // Cranked bar: straight at offset → angled 1:6 → straight at main
                        double crankOff = LapSpliceCalculator.GetCrankOffset(barDia);
                        double crankRun = LapSpliceCalculator.GetCrankRun(barDia);
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, request.Grade, antiGGGravity.StructuralRebar.Constants.SteelGrade.Grade500E, antiGGGravity.StructuralRebar.Constants.BarPosition.Bottom);
                        // Add 2x barDia visual allowance so the crank bend radius physically matches the straight bar tip
                        double straightLap = lapLen + crankRun + barDia * 2.0;

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
                        Comment = (botLayerIdx == 0) ? "Btm Bar" : (botLayerIdx == 1 ? "Btm B2" : (botLayerIdx == 2 ? "Btm B3" : "Btm Bar"))
                    };
                    definitions.Add(segDef);
                }

                // Effective clear gap: at least the user setting, but not less than the bar diameter (code requirement for db > 25mm)
                double effectiveGap = Math.Max(minLayerGap, barDia);
                botZ += (barDia + effectiveGap);
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

                double barLen = barTotalLen - 2 * host.CoverOther;

                // Side bars use generic stock-length splitting only (no code-based lap zones)
                var segments = (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                    ? LapSpliceCalculator.SplitBarForLap(barLen, sideDia, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(sideDia), BarPosition.Other, GetLapSpliceLength(sideDia, request))
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
                        XYZ s = barOriginPt + host.LAxis * (host.CoverOther + seg.Start);
                        XYZ e = barOriginPt + host.LAxis * (host.CoverOther + seg.End);

                        // Position bar at near side — FixedCount=2 distributes to both faces
                        XYZ barStart = new XYZ(s.X, s.Y, z) - host.WAxis * (distWidth / 2.0);
                        XYZ barEnd = new XYZ(e.X, e.Y, z) - host.WAxis * (distWidth / 2.0);

                        var curves = new List<Curve>();

                        if (si > 0 && segments.Count > 1)
                        {
                            double crankOff = LapSpliceCalculator.GetCrankOffset(sideDia);
                            double crankRun = LapSpliceCalculator.GetCrankRun(sideDia);
                            double lapLen = LapSpliceCalculator.CalculateTensionLapLength(sideDia, request.DesignCode, antiGGGravity.StructuralRebar.Constants.ConcreteGrade.C30, antiGGGravity.StructuralRebar.Constants.SteelGrade.Grade500E, antiGGGravity.StructuralRebar.Constants.BarPosition.Other);
                            // Add 2x barDia visual allowance
                            double straightLap = lapLen + crankRun + sideDia * 2.0;

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
            int topLayerIdx = 0;
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
                    layer.OverrideHookLength, layer.HookLengthOverride, 
                    (topLayerIdx == 0) ? "Top Bar" : (topLayerIdx == 1 ? "Top T2" : (topLayerIdx == 2 ? "Top T3" : "Top Bar")));

                if (def != null) definitions.Add(def);
                double effectiveGap = Math.Max(minLayerGap, barDia);
                topOffset -= (barDia + effectiveGap);
                topLayerIdx++;
            }

            // Bottom layers
            double botOffset = -(host.Height / 2.0 - host.CoverBottom - transDia);
            int botLayerIdx = 0;
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
                    layer.OverrideHookLength, layer.HookLengthOverride, 
                    (botLayerIdx == 0) ? "Btm Bar" : (botLayerIdx == 1 ? "Btm B2" : (botLayerIdx == 2 ? "Btm B3" : "Btm Bar")));

                if (def != null) definitions.Add(def);
                double effectiveGap = Math.Max(minLayerGap, barDia);
                botOffset += (barDia + effectiveGap);
                botLayerIdx++;
            }
        }


        public (int Processed, int Total) GenerateWallCornerRebar(List<Wall> walls, RebarRequest request)
        {
            var engine = new WallEngine(_doc);
            return engine.GenerateWallCornerRebar(walls, request);
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
                return request.LapSpliceLength;
            return LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, request.Grade, SteelGrade.Grade500E, position);
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
