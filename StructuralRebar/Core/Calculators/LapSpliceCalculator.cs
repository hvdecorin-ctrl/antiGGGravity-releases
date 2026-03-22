using System;
using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    public static class LapSpliceCalculator
    {
        /// <summary>
        /// Calculates the required lap splice length using grade-dependent rules.
        /// </summary>
        public static double CalculateTensionLapLength(double db, DesignCodeStandard code, ConcreteGrade grade = ConcreteGrade.C30, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    return DesignCodes.GetAciLapMultiplier(grade, steel, position) * db;

                case DesignCodeStandard.AS3600:
                    return DesignCodes.GetAsLapMultiplier(grade, steel, position) * db;

                case DesignCodeStandard.EC2:
                    return DesignCodes.GetEc2LapMultiplier(grade, steel, position) * db;

                case DesignCodeStandard.NZS3101:
                    return DesignCodes.GetNzsLapMultiplier(grade, steel, position) * db;

                default:
                    // Custom user-defined rules
                    double customTenMult = SettingsManager.GetDouble("RebarSuite_CustomDesign", "LapTension", 50.0);
                    return (customTenMult * db);
            }
        }

        public static double CalculateCompressionLapLength(double db, DesignCodeStandard code, ConcreteGrade grade = ConcreteGrade.C30, SteelGrade steel = SteelGrade.Grade500E)
        {
            double fy = DesignCodes.GetYieldStrength(steel);

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    // ACI 318-19: 0.071 * fy * db (mm), min 300mm
                    return Math.Max(0.071 * fy * db, UnitConversion.MmToFeet(300));

                case DesignCodeStandard.AS3600:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(300));

                case DesignCodeStandard.EC2:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(200));

                case DesignCodeStandard.NZS3101:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(300));

                default:
                    double customCompMult = SettingsManager.GetDouble("RebarSuite_CustomDesign", "LapCompression", 30.0);
                    return Math.Max(customCompMult * db, UnitConversion.MmToFeet(300));
            }
        }

        /// <summary>
        /// Maximum stock bar length in feet (11.7m — accounts for bend/hook extensions).
        /// </summary>
        public static double MaxStockLengthFt => UnitConversion.MmToFeet(11700.0);

        /// <summary>
        /// Splits a main bar into segments when total length exceeds maxStockLength.
        /// Each segment overlaps the previous by the required lap splice length.
        /// When crankRun > 0, the total overlap = lapLen + crankRun so that
        /// the full lap length is measured from the end of the angled crank.
        /// Returns a list of (segmentStart, segmentEnd) offsets along the bar axis.
        /// If the bar fits within one stock length, returns a single segment covering the full length.
        /// </summary>
        public static List<(double Start, double End)> SplitBarForLap(
            double totalLength, double barDia, DesignCodeStandard code,
            double maxStockLength = 0, double crankRun = 0, BarPosition position = BarPosition.Other,
            double customLapLen = 0)
        {
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;

            var segments = new List<(double Start, double End)>();

            if (totalLength <= maxStockLength)
            {
                // No splitting needed
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = customLapLen > 0 
                ? customLapLen 
                : CalculateTensionLapLength(barDia, code, ConcreteGrade.C30, SteelGrade.Grade500E, position);
            double totalOverlap = lapLen + crankRun; // Full overlap: lap + crank transition

            // Effective advance per segment = stock length minus total overlap
            double advance = maxStockLength - totalOverlap;
            if (advance <= 0)
            {
                // Bar diameter is so large the lap fills the stock length; just use one bar
                segments.Add((0.0, totalLength));
                return segments;
            }

            double cursor = 0;
            while (cursor < totalLength)
            {
                double segEnd = Math.Min(cursor + maxStockLength, totalLength);
                segments.Add((cursor, segEnd));

                if (segEnd >= totalLength) break;

                // Next segment starts at (end - totalOverlap) to create the overlap
                cursor = segEnd - totalOverlap;
            }

            return segments;
        }

        /// <summary>
        /// Splits a beam bar using code-based preferred cut positions.
        /// Zone compliance is COMPULSORY — splices MUST fall INSIDE the correct zone.
        /// Top bars: splice INSIDE the middle L/N zone (N = topZoneDivisor, default 3).
        /// Bottom bars: splice INSIDE the L/N zone from each support (N = bottomZoneDivisor, default 5).
        /// 
        /// Stagger rule: odd-numbered layers offset the splice further into the zone
        /// to ensure no more than 50% of bars are spliced at the same cross-section.
        /// - Top bar layer 2: splice shifts closer to mid-span
        /// - Bottom bar layer 2: splice shifts closer to support
        /// </summary>
        public static List<(double Start, double End)> SplitBeamBarForLap(
            double totalLength, double barDia, DesignCodeStandard code,
            bool isTopBar, int layerIndex = 0, double maxStockLength = 0, double crankRun = 0,
            double customLapLen = 0, int topZoneDivisor = 3, int bottomZoneDivisor = 5)
        {
            BarPosition position = isTopBar ? BarPosition.Top : BarPosition.Bottom;
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;
            if (crankRun <= 0) crankRun = GetCrankRun(barDia);
            if (topZoneDivisor < 2) topZoneDivisor = 3;
            if (bottomZoneDivisor < 2) bottomZoneDivisor = 5;

            var segments = new List<(double Start, double End)>();

            // No splitting needed if bar fits in one stock length
            if (totalLength <= maxStockLength)
            {
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = customLapLen > 0 
                ? customLapLen 
                : CalculateTensionLapLength(barDia, code, ConcreteGrade.C30, SteelGrade.Grade500E, position);
            double totalOverlap = lapLen + crankRun;

            // Stagger offset for alternating layers (odd layers shift deeper into zone)
            // Offset = 1.3 × lap length (standard stagger distance)
            double staggerOffset = (layerIndex % 2 == 1) ? 1.3 * lapLen + UnitConversion.MmToFeet(200) : 0.0;

            // Calculate cut positions so the LAP ZONE falls INSIDE the correct zone.
            // The splice/lap region = (cutPoint - totalOverlap) to cutPoint.
            double cut1, cut2;
            double topFrac = 1.0 / topZoneDivisor;
            double botFrac = 1.0 / bottomZoneDivisor;

            if (isTopBar)
            {
                // TOP BARS: splice must be INSIDE the middle L/N zone [L/N .. (N-1)L/N]
                // Layer 0: splice starts at L/N boundary
                // Layer 1: splice shifts further toward mid-span (stagger)
                cut1 = totalLength * topFrac + totalOverlap + staggerOffset;
                cut2 = totalLength * (1.0 - topFrac) - staggerOffset;
            }
            else
            {
                // BOTTOM BARS: splice must be INSIDE the L/N zone from each support [0..L/N] and [(N-1)L/N..L]
                // Layer 0: splice ends at L/N boundary
                // Layer 1: splice shifts closer to support (stagger)
                cut1 = totalLength * botFrac - staggerOffset;
                cut2 = totalLength * (1.0 - botFrac) + totalOverlap + staggerOffset;
            }

            // Safety: ensure cut positions stay valid
            cut1 = Math.Max(totalOverlap, Math.Min(cut1, totalLength - totalOverlap));
            if (cut2 > 0) cut2 = Math.Max(cut1 + totalOverlap, Math.Min(cut2, totalLength));

            // ── TRY 2-PIECE SPLIT (single cut at left zone) ──
            double piece1 = cut1;
            double piece2 = totalLength - cut1 + totalOverlap;
            double longestPiece = Math.Max(piece1, piece2);

            if (longestPiece <= maxStockLength)
            {
                // 2 pieces — single cut, splice inside correct zone
                segments.Add((0.0, cut1));
                segments.Add((cut1 - totalOverlap, totalLength));
                return segments;
            }

            // ── 3-PIECE SPLIT (cuts at BOTH zone positions) ──
            // Piece 1: support → cut1
            // Piece 2: middle section (with overlaps on both sides)
            // Piece 3: cut2 → support
            segments.Add((0.0, cut1));
            segments.Add((cut1 - totalOverlap, cut2));
            segments.Add((cut2 - totalOverlap, totalLength));
            return segments;
        }

        /// <summary>
        /// Crank offset perpendicular to bar axis (1× bar diameter).
        /// The bar shifts by this amount to sit beside the continuing bar.
        /// </summary>
        public static double GetCrankOffset(double barDia) => barDia;

        /// <summary>
        /// Horizontal run of the crank along the bar axis (6× bar diameter).
        /// This gives the standard 1-in-6 slope.
        /// </summary>
        public static double GetCrankRun(double barDia) => 6.0 * barDia;

        /// <summary>
        /// Splits a continuous bar (multi-span or single long span) according to actual defined clear spans.
        /// Top bars: splice INSIDE the middle L/N zone of any span (N = topZoneDivisor).
        /// Bottom bars: splice INSIDE the support zones [L/N of previous span ... L/N of next span] (N = bottomZoneDivisor).
        /// </summary>
        public static List<(double Start, double End)> SplitContinuousBarForLap(
            double totalLength, double barDia, DesignCodeStandard code,
            bool isTopBar, int layerIndex, List<(double Start, double End)> clearSpans,
            double maxStockLength = 0, double crankRun = 0,
            int topZoneDivisor = 3, int bottomZoneDivisor = 5)
        {
            BarPosition position = isTopBar ? BarPosition.Top : BarPosition.Bottom;
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;
            if (crankRun <= 0) crankRun = GetCrankRun(barDia);
            if (topZoneDivisor < 2) topZoneDivisor = 3;
            if (bottomZoneDivisor < 2) bottomZoneDivisor = 5;

            var segments = new List<(double Start, double End)>();

            if (totalLength <= maxStockLength)
            {
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = CalculateTensionLapLength(barDia, code, ConcreteGrade.C30, SteelGrade.Grade500E, position);
            double totalOverlap = lapLen + crankRun;
            double staggerOffset = (layerIndex % 2 == 1) ? 1.3 * lapLen + UnitConversion.MmToFeet(200) : 0.0;

            double topFrac = 1.0 / topZoneDivisor;
            double botFrac = 1.0 / bottomZoneDivisor;

            // Define all valid zones along the absolute bar length (0 to totalLength)
            var validZones = new List<(double ZStart, double ZEnd)>();

            if (isTopBar)
            {
                foreach (var span in clearSpans)
                {
                    double L = span.End - span.Start;
                    if (L <= 0) continue;
                    validZones.Add((span.Start + L * topFrac, span.End - L * topFrac));
                }
            }
            else
            {
                if (clearSpans.Count > 0)
                {
                    double firstL = clearSpans[0].End - clearSpans[0].Start;
                    // First span: check if it's a cantilever. Never lap in a cantilever.
                    bool isStartCantileverSpan = clearSpans[0].Start < 0.1;
                    if (!isStartCantileverSpan)
                    {
                        // Standard end span: lap inside the L/N near the first column
                        validZones.Add((0.0, clearSpans[0].Start + firstL * botFrac));
                    }

                    for (int i = 1; i < clearSpans.Count; i++)
                    {
                        var prevSpan = clearSpans[i - 1];
                        var span = clearSpans[i];
                        double Lprev = prevSpan.End - prevSpan.Start;
                        double L = span.End - span.Start;

                        validZones.Add((prevSpan.End - Lprev * botFrac, prevSpan.End));
                        validZones.Add((span.Start, span.Start + L * botFrac));
                    }

                    var lastSpan = clearSpans[clearSpans.Count - 1];
                    double lastL = lastSpan.End - lastSpan.Start;
                    bool isEndCantileverSpan = (totalLength - lastSpan.End) < 0.1;
                    if (!isEndCantileverSpan)
                    {
                        // Standard end span: lap inside the L/N near the far end column
                        validZones.Add((lastSpan.End - lastL * botFrac, totalLength));
                    }
                }
                else
                {
                    validZones.Add((0.0, totalLength));
                }
            }

            double cursor = 0;
            while (cursor < totalLength)
            {
                double targetMaxCut = cursor + maxStockLength;
                if (targetMaxCut >= totalLength)
                {
                    segments.Add((cursor, totalLength));
                    break;
                }

                double bestCut = -1;

                // Iterate backwards through valid zones to find the furthest one we can safely reach
                for (int i = validZones.Count - 1; i >= 0; i--)
                {
                    var z = validZones[i];
                    
                    // If this zone is completely out of reach, skip to an earlier one
                    if (z.ZStart >= targetMaxCut) continue;

                    // The ideal, safest cut is exactly in the center of the valid zone
                    // We offset by (totalOverlap / 2) so the *entire splice* is centered in the zone.
                    double actualCut = (z.ZStart + z.ZEnd) / 2.0 + (totalOverlap / 2.0);

                    // If we can't safely stretch to the center, fall back to max stock length
                    if (actualCut > targetMaxCut) actualCut = targetMaxCut;

                    // Apply odd-layer stagger
                    double cutWithStagger = actualCut - staggerOffset;
                    
                    // If stagger doesn't push the lap out of the zone, use it.
                    // Otherwise, ignore stagger and keep it inside the zone to guarantee code compliance.
                    if (cutWithStagger - totalOverlap >= z.ZStart)
                    {
                        actualCut = cutWithStagger;
                    }

                    // Enforce upper bounds (cannot cut beyond the zone or stock length)
                    if (actualCut > z.ZEnd) actualCut = z.ZEnd;
                    if (actualCut > targetMaxCut) actualCut = targetMaxCut;

                    // Enforce lower bound for the entire lap length
                    // If shifting the cut to the upper bound still leaves the lap hanging out the left side,
                    // we shift the cut to the exact left edge needed to fit the lap.
                    if (actualCut - totalOverlap < z.ZStart)
                    {
                        actualCut = z.ZStart + totalOverlap;
                    }

                    // Final code-compliance verification: does this cut fit perfectly inside the valid zone?
                    // We MUST also verify it doesn't exceed targetMaxCut.
                    if (actualCut <= targetMaxCut + 0.005 && actualCut <= z.ZEnd + 0.005 && (actualCut - totalOverlap) >= z.ZStart - 0.005) 
                    {
                        bestCut = actualCut;
                        break;
                    }
                }

                // A valid cut was found and meaningfully advances the cursor
                if (bestCut > cursor + totalOverlap + UnitConversion.MmToFeet(100))
                {
                    // Strict Length Enforcement: We can NEVER exceed the absolute max stock length from the current cursor.
                    if (bestCut > targetMaxCut) bestCut = targetMaxCut;

                    segments.Add((cursor, bestCut));
                    cursor = bestCut - totalOverlap;
                }
                else
                {
                    // Fallback if no zone is reachable within stock length (e.g. extremely long isolated spans)
                    bestCut = targetMaxCut;
                    segments.Add((cursor, bestCut));
                    cursor = bestCut - totalOverlap;
                }
            }

            return segments;
        }
    }
}
