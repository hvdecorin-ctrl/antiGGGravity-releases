using System;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    public static class HookCalculator
    {
        public static double GetHookExtension(HookAngle angle, double barDia, DesignCodeStandard code)
        {
            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    // ACI 318-19 Table 25.3.1
                    // 90 deg: 12db
                    // 135 deg: 6db or 3 in (75mm)
                    // 180 deg: 4db or 2.5 in (65mm)
                    if (angle == HookAngle.Deg90) return 12.0 * barDia;
                    if (angle == HookAngle.Deg135) Math.Max(6.0 * barDia, UnitConversion.MmToFeet(75.0));
                    if (angle == HookAngle.Deg180) Math.Max(4.0 * barDia, UnitConversion.MmToFeet(65.0));
                    break;

                case DesignCodeStandard.AS3600:
                    // AS 3600 (simplified typical lengths)
                    if (angle == HookAngle.Deg90) return 12.0 * barDia;
                    if (angle == HookAngle.Deg135) return Math.Max(6.0 * barDia, UnitConversion.MmToFeet(75.0));
                    if (angle == HookAngle.Deg180) return Math.Max(4.0 * barDia, UnitConversion.MmToFeet(65.0));
                    break;
            }

            // Custom / default fallback
            if (angle == HookAngle.Deg90) return 12.0 * barDia;
            if (angle == HookAngle.Deg135) return 6.0 * barDia;
            if (angle == HookAngle.Deg180) return 4.0 * barDia;
            
            return 0;
        }

        public static double GetMinimumBendDiameter(double barDia, DesignCodeStandard code)
        {
             // Generally 4db for small bars, 6db for larger bars
             // Simplified generic return for now.
             return 4.0 * barDia;
        }
    }
}
