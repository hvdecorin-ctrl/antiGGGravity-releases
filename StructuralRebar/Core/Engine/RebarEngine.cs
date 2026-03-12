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
                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(barDia, request.DesignCode, antiGGGravity.StructuralRebar.Constants.ConcreteGrade.C30, antiGGGravity.StructuralRebar.Constants.SteelGrade.Grade500E, antiGGGravity.StructuralRebar.Constants.BarPosition.Top);
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
                        Comment = layer.IsContinuous ? "Top Bar" : "Top Additional Bar"
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
                                Comment = "Top Additional Bar"
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
                                Comment = "Top Additional Bar"
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
                        Label = layer.IsContinuous ? (segments.Count > 1 ? "Btm Continuous (lapped)" : "Btm Continuous") : "Btm Additional (Sagging)",
                        Comment = layer.IsContinuous ? "Btm Bar" : "Btm Additional Bar"
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
                                Comment = "Btm Additional Bar"
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
                                Comment = "Btm Additional Bar"
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
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: true, layerIndex: topLayerIdx)
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
                        Comment = "Top Bar"
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
                    ? LapSpliceCalculator.SplitBeamBarForLap(barLen, barDia, request.DesignCode, isTopBar: false, layerIndex: botLayerIdx)
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
                        Comment = "Btm Bar"
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
                double effectiveGap = Math.Max(minLayerGap, barDia);
                topOffset -= (barDia + effectiveGap);
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
                double effectiveGap = Math.Max(minLayerGap, barDia);
                botOffset += (barDia + effectiveGap);
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

                    // If hooks are specified at an end, don't trim horizontal bars there —
                    // the user wants bars (U-bars) extending to the wall face with hooks.
                    double hStartCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookStartName) ? startTrim : 0);
                    double hEndCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookEndName) ? endTrim : 0);

                    var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                        host, layer.HorizontalBarTypeName, hDia,
                        layer.HorizontalSpacing, layer.TopOffset, layer.BottomOffset,
                        hStartCover,
                        hEndCover,
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
                            starterDef.Comment = "Starter Bar";
                            definitions.Add(starterDef);
                        }
                    }

                    // 3. Horizontal Bars
                    if (request.Layers.Count > 0 && hDia > 0)
                    {
                        var layer = request.Layers[0];

                        // If hooks are specified at an end, don't trim horizontal bars there —
                        // the user wants bars (U-bars) extending to the wall face with hooks.
                        double hStartCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookStartName) ? startTrim : 0);
                        double hEndCover = host.CoverOther + (string.IsNullOrEmpty(layer.HookEndName) ? endTrim : 0);

                        var horizDef = WallLayoutGenerator.CreateHorizontalBars(
                            host, layer.HorizontalBarTypeName, hDia,
                            layer.HorizontalSpacing, layer.TopOffset, layer.BottomOffset,
                            hStartCover,
                            hEndCover,
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
