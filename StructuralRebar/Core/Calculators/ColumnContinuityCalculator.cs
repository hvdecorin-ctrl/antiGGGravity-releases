using System;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.DTO;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    /// <summary>
    /// Calculates splice positions, starter bar lengths, and crank parameters
    /// for multi-level column continuity.
    /// </summary>
    public static class ColumnContinuityCalculator
    {
        /// <summary>
        /// Gets the starter bar embedment length into the foundation.
        /// Uses tension lap length to comply with design codes for bending moment transfer.
        /// </summary>
        /// <param name="barDia">Bar diameter in feet.</param>
        /// <param name="code">Design code standard.</param>
        /// <returns>Development length in feet.</returns>
        public static double GetStarterBarLength(double barDia, DesignCodeStandard code)
        {
            return LapSpliceCalculator.CalculateTensionLapLength(barDia, code);
        }

        /// <summary>
        /// Gets the splice offset above the slab/floor level where the splice starts.
        /// Standard practice: splice starts 50mm above floor level.
        /// </summary>
        /// <returns>Offset in feet from the column base.</returns>
        public static double GetSpliceStartOffset()
        {
            // Standard practice: 50mm above slab level
            return UnitConversion.MmToFeet(50.0);
        }

        /// <summary>
        /// Gets the total splice extension length that lower column bars must
        /// project above into the upper column.
        /// </summary>
        /// <param name="barDia">Bar diameter in feet.</param>
        /// <param name="code">Design code standard.</param>
        /// <returns>Extension length in feet (above the floor slab into upper column).</returns>
        public static double GetSpliceExtension(double barDia, DesignCodeStandard code)
        {
            // Splice extension = offset above slab + tension lap length (standard for column verticals)
            return GetSpliceStartOffset() + LapSpliceCalculator.CalculateTensionLapLength(barDia, code);
        }

        /// <summary>
        /// Calculates crank parameters when a column cross-section changes between levels.
        /// The crank offsets bars from the wider lower column position to the narrower 
        /// upper column position using a 1:6 slope.
        /// </summary>
        /// <param name="lowerDim">Width or depth of the lower column (feet).</param>
        /// <param name="upperDim">Width or depth of the upper column (feet).</param>
        /// <param name="barDia">Bar diameter (feet).</param>
        /// <param name="lowerCover">Cover distance on lower column (feet).</param>
        /// <param name="upperCover">Cover distance on upper column (feet).</param>
        /// <returns>Crank offset perpendicular to bar, crank run along bar, and whether cranking is needed.</returns>
        public static (double Offset, double Run, bool Needed) GetCrankParams(
            double lowerDim, double upperDim, double barDia,
            double lowerCover, double upperCover)
        {
            // Bar position from center in each column
            double lowerBarPos = lowerDim / 2.0 - lowerCover;
            double upperBarPos = upperDim / 2.0 - upperCover;

            // Offset = difference in bar positions
            double offset = lowerBarPos - upperBarPos;

            // Only crank if offset > bar diameter (meaningful shift)
            bool needed = offset > barDia;
            double run = needed ? LapSpliceCalculator.GetCrankRun(barDia) : 0;

            return (offset, run, needed);
        }

        /// <summary>
        /// Determines the splice position along the column height based on strategy.
        /// </summary>
        /// <param name="columnHeight">Height of the column (feet).</param>
        /// <param name="splicePosition">"Above Slab" or "Mid Height".</param>
        /// <returns>Offset from column base where the splice starts (feet).</returns>
        public static double GetSplicePosition(double columnHeight, string splicePosition)
        {
            if (string.Equals(splicePosition, "Mid Height", StringComparison.OrdinalIgnoreCase))
            {
                // Mid-height splice
                return columnHeight / 2.0;
            }

            // Default: Above Slab (50mm above base)
            return GetSpliceStartOffset();
        }
    }
}
