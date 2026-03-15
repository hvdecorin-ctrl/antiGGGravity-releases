namespace antiGGGravity.StructuralRebar.Constants
{
    /// <summary>
    /// Design code enumerations and standard values.
    /// Reference: ACI 318-19, AS 3600:2018, EN 1992-1-1 (EC2), NZS 3101:2006.
    /// </summary>
    public enum ConcreteGrade
    {
        C25, C30, C35, C40, C50
    }

    public enum DesignCodeStandard
    {
        ACI318,
        AS3600,
        EC2,
        NZS3101,
        Custom
    }

    public enum SteelGrade
    {
        Grade300E,
        Grade500E
    }

    public enum HookAngle
    {
        None = 0,
        Deg90 = 90,
        Deg135 = 135,
        Deg180 = 180
    }

    public enum BarPosition
    {
        Top,
        Bottom,
        Other
    }

    public enum RebarSide
    {
        Top,
        Bottom,
        Exterior,
        Interior,
        Left,
        Right
    }

    public enum RebarLayerFace
    {
        Exterior,
        Interior,
        Centre,
        BothFaces
    }

    public enum ElementHostType
    {
        Beam,
        Wall,
        Column,
        FootingPad,
        PadShape,
        StripFooting,
        WallCornerL,
        WallCornerU,
        BeamAdvance,
        BoredPile
    }

    public static class DesignCodes
    {
        // === Steel Grade ===
        public static double GetYieldStrength(SteelGrade steel)
        {
            return steel switch
            {
                SteelGrade.Grade300E => 300.0,
                SteelGrade.Grade500E => 500.0,
                _ => 500.0
            };
        }

        // === NZS 3101 Hook Extensions (multiples of bar diameter) ===
        public const double Hook90Extension = 12.0;    // 12db straight extension
        public const double Hook135Extension = 6.0;    // 6db into concrete core
        public const double Hook180Extension = 4.0;    // 4db past bend

        // === NZS 3101 Bend Radius (multiple of bar diameter) ===
        public const double StandardBendRadius = 5.0;  // 5db for all sizes (NZS)

        // === NZS 3101 Lap Splice Multipliers ===
        public const double TopBarFactor = 1.3;        // Bars with >300mm concrete below

        /// <summary>NZS 3101:2006 A3 tension development length multiplier (Ld/db).</summary>
        public static double GetNzsDevMultiplier(ConcreteGrade grade, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            // NZS 3101:2006 A3 Cl 8.6.3.2: Ldb = 0.5 * fy / sqrt(f'c) * db
            double fy = GetYieldStrength(steel);
            double fc = ToMPa(grade);
            double k = 0.5 * fy / Math.Sqrt(fc);
            
            // NZS 3101:2006 Cl 8.6.3.2: 1.3 factor for top bars (>300mm concrete below)
            if (position == BarPosition.Top) k *= TopBarFactor;

            return Math.Max(k, 25.0);
        }

        /// <summary>NZS 3101:2006 A3 tension lap multiplier (1.3 * Ld/db).</summary>
        public static double GetNzsLapMultiplier(ConcreteGrade grade, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            return 1.3 * GetNzsDevMultiplier(grade, steel, position);
        }

        /// <summary>Backward compatibility for NZS lap multiplier.</summary>
        public static double GetLapMultiplier(ConcreteGrade grade) => GetNzsLapMultiplier(grade);

        /// <summary>ACI 318: tension dev length multiplier by concrete grade (Class B splice).</summary>
        public static double GetAciLapMultiplier(ConcreteGrade grade, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            // ACI 318-19 Table 25.4.2.2: ld/db = (fy * ψt * ψe) / (2.1 * λ * sqrt(f'c))
            // Class B splice = 1.3 * ld
            double fy = GetYieldStrength(steel);
            double fc = ToMPa(grade);
            
            double psiT = (position == BarPosition.Top) ? 1.3 : 1.0;
            double kLd = (fy * psiT) / (2.1 * Math.Sqrt(fc));
            
            double kLap = 1.3 * kLd; // Class B
            return Math.Max(kLap, 25.0);
        }

        /// <summary>AS 3600: tension dev length multiplier by concrete grade (simplified).</summary>
        public static double GetAsLapMultiplier(ConcreteGrade grade, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            // AS 3600:2018 Cl 13.1.2: Lsy.t = 0.5 * k1 * fy * db / sqrt(f'c)
            // k1 = 1.3 for top bars, 1.0 otherwise
            double fy = GetYieldStrength(steel);
            double fc = ToMPa(grade);
            double k1 = (position == BarPosition.Top) ? 1.3 : 1.0;
            double k = 0.5 * k1 * fy / Math.Sqrt(fc);
            return Math.Max(k, 25.0);
        }

        /// <summary>EC2: tension dev length multiplier by concrete grade (simplified).</summary>
        public static double GetEc2LapMultiplier(ConcreteGrade grade, SteelGrade steel = SteelGrade.Grade500E, BarPosition position = BarPosition.Other)
        {
            // EN 1992-1-1 Cl 8.4.2: lb,rqd = (db/4) * (σsd / fbd)
            // fbd = 2.25 * η1 * fctd (η1 = 0.7 for poor bond conditions/top bars, 1.0 for good)
            double fy = GetYieldStrength(steel);
            double fc = ToMPa(grade);
            double eta1 = (position == BarPosition.Top) ? 0.7 : 1.0;
            double fctd = 0.7 * 0.3 * Math.Pow(fc, 2.0 / 3.0) / 1.5;
            double fbd = 2.25 * eta1 * fctd;
            double kLd = fy / (4.0 * fbd);
            double kLap = 1.5 * kLd; // 1.5 = alpha_6 for lap
            return Math.Max(kLap, 25.0);
        }

        // === NZS 3101 Zone Spacing Rules ===
        /// <summary>End zone length = factor × beam height.</summary>
        public const double EndZoneFactor = 2.0;       // 2h from face of support
        /// <summary>Max stirrup spacing in end zone = d / divisor.</summary>
        public const double EndZoneSpacingDivisor = 4.0;   // d/4
        /// <summary>Max stirrup spacing in mid zone = d / divisor.</summary>
        public const double MidZoneSpacingDivisor = 2.0;   // d/2
        /// <summary>Absolute max stirrup spacing in mm.</summary>
        public const double MaxStirrupSpacingMm = 600.0;

        // === Standard Stock Lengths (meters) ===
        public const double StockLength6m = 6.0;
        public const double StockLength12m = 12.0;

        /// <summary>Concrete grade → f'c in MPa.</summary>
        public static double ToMPa(ConcreteGrade grade)
        {
            return grade switch
            {
                ConcreteGrade.C25 => 25.0,
                ConcreteGrade.C30 => 30.0,
                ConcreteGrade.C35 => 35.0,
                ConcreteGrade.C40 => 40.0,
                ConcreteGrade.C50 => 50.0,
                _ => 30.0
            };
        }
    }
}
