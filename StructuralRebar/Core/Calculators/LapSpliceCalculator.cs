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
        /// Maximum stock bar length in feet (11.5m — accounts for bend/hook extensions).
        /// </summary>
        public static double MaxStockLengthFt => UnitConversion.MmToFeet(11500.0);

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
            double maxStockLength = 0, double crankRun = 0, BarPosition position = BarPosition.Other)
        {
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;

            var segments = new List<(double Start, double End)>();

            if (totalLength <= maxStockLength)
            {
                // No splitting needed
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = CalculateTensionLapLength(barDia, code, ConcreteGrade.C30, SteelGrade.Grade500E, position);
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
        /// Top bars: splice INSIDE the middle L/3 zone (red zone).
        /// Bottom bars: splice INSIDE the L/5 zone from each support (green zone).
        /// (L/5 is stricter than the code-minimum L/4, providing extra safety margin)
        /// 
        /// Stagger rule: odd-numbered layers offset the splice further into the zone
        /// to ensure no more than 50% of bars are spliced at the same cross-section.
        /// - Top bar layer 2: splice shifts closer to mid-span
        /// - Bottom bar layer 2: splice shifts closer to support
        /// </summary>
        public static List<(double Start, double End)> SplitBeamBarForLap(
            double totalLength, double barDia, DesignCodeStandard code,
            bool isTopBar, int layerIndex = 0, double maxStockLength = 0, double crankRun = 0)
        {
            BarPosition position = isTopBar ? BarPosition.Top : BarPosition.Bottom;
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;
            if (crankRun <= 0) crankRun = GetCrankRun(barDia);

            var segments = new List<(double Start, double End)>();

            // No splitting needed if bar fits in one stock length
            if (totalLength <= maxStockLength)
            {
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = CalculateTensionLapLength(barDia, code, ConcreteGrade.C30, SteelGrade.Grade500E, position);
            double totalOverlap = lapLen + crankRun;

            // Stagger offset for alternating layers (odd layers shift deeper into zone)
            // Offset = 1.3 × lap length (standard stagger distance)
            double staggerOffset = (layerIndex % 2 == 1) ? 1.3 * lapLen + UnitConversion.MmToFeet(200) : 0.0;

            // Calculate cut positions so the LAP ZONE falls INSIDE the correct zone.
            // The splice/lap region = (cutPoint - totalOverlap) to cutPoint.
            double cut1, cut2;

            if (isTopBar)
            {
                // TOP BARS: splice must be INSIDE the middle L/3 zone [L/3 .. 2L/3]
                // Layer 0: splice starts at L/3 boundary
                // Layer 1: splice shifts further toward mid-span (stagger)
                cut1 = totalLength / 3.0 + totalOverlap + staggerOffset;
                cut2 = totalLength * 2.0 / 3.0 - staggerOffset;
            }
            else
            {
                // BOTTOM BARS: splice must be INSIDE the L/5 zone from each support [0..L/5] and [4L/5..L]
                // (L/5 is stricter than code-minimum L/4 for extra safety)
                // Layer 0: splice ends at L/5 boundary
                // Layer 1: splice shifts closer to support (stagger)
                cut1 = totalLength / 5.0 - staggerOffset;
                cut2 = totalLength * 4.0 / 5.0 + totalOverlap + staggerOffset;
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
    }
}
