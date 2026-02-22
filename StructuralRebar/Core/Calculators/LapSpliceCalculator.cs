using System;
using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    public static class LapSpliceCalculator
    {
        /// <summary>
        /// Calculates the required lap splice length using grade-dependent rules.
        /// </summary>
        public static double CalculateTensionLapLength(double db, DesignCodeStandard code, ConcreteGrade grade = ConcreteGrade.C30, SteelGrade steel = SteelGrade.Grade500E)
        {
            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    return DesignCodes.GetAciLapMultiplier(grade, steel) * db;

                case DesignCodeStandard.AS3600:
                    return DesignCodes.GetAsLapMultiplier(grade, steel) * db;

                case DesignCodeStandard.EC2:
                    return DesignCodes.GetEc2LapMultiplier(grade, steel) * db;

                case DesignCodeStandard.NZS3101:
                    return DesignCodes.GetNzsLapMultiplier(grade) * db;

                default:
                    // Custom fallback (conservative)
                    return (50.0 * db);
            }
        }

        public static double CalculateCompressionLapLength(double db, DesignCodeStandard code, ConcreteGrade grade = ConcreteGrade.C30, SteelGrade steel = SteelGrade.Grade500E)
        {
            double fy = DesignCodes.GetYieldStrength(steel);

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    // ACI 318-19: 0.071 * fy * db (mm), min 300mm
                    return Math.Max(0.071 * fy / 500.0 * 30.0 * db, UnitConversion.MmToFeet(300));

                case DesignCodeStandard.AS3600:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(300));

                case DesignCodeStandard.EC2:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(200));

                case DesignCodeStandard.NZS3101:
                    return Math.Max(25.0 * db, UnitConversion.MmToFeet(300));

                default:
                    return Math.Max(30.0 * db, UnitConversion.MmToFeet(300));
            }
        }

        /// <summary>
        /// Maximum stock bar length in feet (12m default).
        /// </summary>
        public static double MaxStockLengthFt => UnitConversion.MmToFeet(12000.0);

        /// <summary>
        /// Splits a main bar into segments when total length exceeds maxStockLength.
        /// Each segment overlaps the previous by the required lap splice length.
        /// Returns a list of (segmentStart, segmentEnd) offsets along the bar axis.
        /// If the bar fits within one stock length, returns a single segment covering the full length.
        /// </summary>
        public static List<(double Start, double End)> SplitBarForLap(
            double totalLength, double barDia, DesignCodeStandard code,
            double maxStockLength = 0)
        {
            if (maxStockLength <= 0) maxStockLength = MaxStockLengthFt;

            var segments = new List<(double Start, double End)>();

            if (totalLength <= maxStockLength)
            {
                // No splitting needed
                segments.Add((0.0, totalLength));
                return segments;
            }

            double lapLen = CalculateTensionLapLength(barDia, code);

            // Effective advance per segment = stock length minus one lap overlap
            double advance = maxStockLength - lapLen;
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

                // Next segment starts at (end - lap) to create the overlap
                cursor = segEnd - lapLen;
            }

            return segments;
        }
    }
}
