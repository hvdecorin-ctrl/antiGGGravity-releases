namespace antiGGGravity.StructuralRebar.Constants
{
    /// <summary>
    /// Centralized unit conversion. All internal calculations use feet (Revit internal).
    /// Convert at input boundary (UI → feet) and output boundary (feet → display).
    /// </summary>
    public static class UnitConversion
    {
        public const double MmPerFoot = 304.8;
        public const double FeetPerMm = 1.0 / 304.8;
        public const double FeetPerMeter = 1.0 / 0.3048;

        /// <summary>Millimeters → Feet (Revit internal)</summary>
        public static double MmToFeet(double mm) => mm * FeetPerMm;

        /// <summary>Feet → Millimeters (display)</summary>
        public static double FeetToMm(double feet) => feet * MmPerFoot;

        /// <summary>
        /// Rounds up a value in feet to the nearest 25mm increment.
        /// Example: 420mm -> 425mm, 960mm -> 975mm.
        /// </summary>
        public static double RoundUpToNearest25mm(double feet)
        {
            double mm = FeetToMm(feet);
            double roundedMm = System.Math.Ceiling(mm / 25.0) * 25.0;
            return MmToFeet(roundedMm);
        }

        /// <summary>Meters → Feet</summary>
        public static double MToFeet(double m) => m * FeetPerMeter;

        /// <summary>Degrees → Radians</summary>
        public static double DegToRad(double deg) => deg * System.Math.PI / 180.0;
    }
}
