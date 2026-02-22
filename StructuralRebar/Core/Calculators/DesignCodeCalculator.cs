using System;
using System.Collections.Generic;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    /// <summary>
    /// Calculates rebar parameters for display in the Design Code Reference tool.
    /// All output values are in millimeters.
    /// </summary>
    public static class DesignCodeCalculator
    {
        public class CodeLookupResult
        {
            public string CodeName { get; set; }
            public double TensionDevLengthMm { get; set; }
            public double CompressionDevLengthMm { get; set; }
            public double TensionLapMm { get; set; }
            public double CompressionLapMm { get; set; }
            public string BeamEndZoneLength { get; set; }
            public double BeamEndZoneSpacingMm { get; set; }
            public string ColumnConfineLength { get; set; }
            public double ColumnConfineSpacingMm { get; set; }
            public double ColumnMidSpacingMm { get; set; }
            public double Hook90ExtMm { get; set; }
            public double Hook135ExtMm { get; set; }
            public double BendRadiusMm { get; set; }
            public double LapMultiplier { get; set; }
            public double DevMultiplier { get; set; }
        }

        public static CodeLookupResult Calculate(
            DesignCodeStandard code,
            ConcreteGrade grade,
            SteelGrade steel,
            double barDiaMm,
            double beamDepthMm = 600,
            double columnDimMm = 400)
        {
            double dbFt = UnitConversion.MmToFeet(barDiaMm);
            double beamDepthFt = UnitConversion.MmToFeet(beamDepthMm);
            double columnDimFt = UnitConversion.MmToFeet(columnDimMm);

            // Tension & Compression Lap (use the calculator, then convert to mm)
            double tensionLapFt = LapSpliceCalculator.CalculateTensionLapLength(dbFt, code, grade, steel);
            double compressionLapFt = LapSpliceCalculator.CalculateCompressionLapLength(dbFt, code, grade, steel);

            // Lap multiplier (for display)
            double lapK = GetTensionLapMultiplier(code, grade, steel);

            // Development length = Lap / splice factor
            // ACI: Class B splice = 1.3 × Ld, so Ld = lap/1.3
            // AS: splice = 1.25 × Lsy.t, so Ld = lap/1.25
            // EC2: lap = alpha_6 × lb,rqd with alpha_6=1.5, so Ld = lap/1.5
            // NZS: simplified dev length ≈ lap (conservative)
            double spliceFactor = code switch
            {
                DesignCodeStandard.ACI318 => 1.3,
                DesignCodeStandard.AS3600 => 1.25,
                DesignCodeStandard.EC2 => 1.5,
                DesignCodeStandard.NZS3101 => 1.0, // NZS simplified: Ld ≈ lap
                _ => 1.0
            };
            double tensionDevFt = tensionLapFt / spliceFactor;
            double compressionDevFt = compressionLapFt / spliceFactor;
            double devK = lapK / spliceFactor;

            // Beam end zone spacing
            double endZoneSpacingMm;
            string endZoneLenDesc;
            double endZoneFactor;

            // Column confinement spacing
            double colConfineSpacingMm;
            double colMidSpacingMm;
            string colConfineLenDesc;

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    endZoneFactor = 2.0;
                    endZoneSpacingMm = Math.Min(beamDepthMm / 4.0, Math.Min(150.0, beamDepthMm)); // simplified
                    endZoneLenDesc = "2h";
                    colConfineSpacingMm = Math.Min(columnDimMm / 4.0, Math.Min(6 * barDiaMm, 150.0));
                    colMidSpacingMm = colConfineSpacingMm * 2;
                    colConfineLenDesc = "max(H/6, D, 450mm)";
                    break;

                case DesignCodeStandard.AS3600:
                    endZoneFactor = 2.0;
                    endZoneSpacingMm = Math.Min(beamDepthMm / 4.0, 150.0);
                    endZoneLenDesc = "2D";
                    colConfineSpacingMm = Math.Min(columnDimMm / 2.0, Math.Min(15 * barDiaMm, 300.0));
                    colMidSpacingMm = colConfineSpacingMm * 2;
                    colConfineLenDesc = "max(H/6, D, 450mm)";
                    break;

                case DesignCodeStandard.EC2:
                    endZoneFactor = 1.5;
                    endZoneSpacingMm = Math.Min(beamDepthMm / 4.0, 200.0);
                    endZoneLenDesc = "1.5h";
                    colConfineSpacingMm = Math.Min(columnDimMm / 2.0, Math.Min(8 * barDiaMm, 175.0));
                    colMidSpacingMm = Math.Min(15 * barDiaMm, Math.Min(columnDimMm, 300.0));
                    colConfineLenDesc = "max(H/6, D, 450mm)";
                    break;

                case DesignCodeStandard.NZS3101:
                    endZoneFactor = 2.0;
                    endZoneSpacingMm = Math.Min(beamDepthMm / 4.0, 100.0);
                    endZoneLenDesc = "2h";
                    colConfineSpacingMm = Math.Min(columnDimMm / 4.0, 100.0);
                    colMidSpacingMm = Math.Min(columnDimMm / 2.0, 200.0);
                    colConfineLenDesc = "max(H/6, D, 450mm)";
                    break;

                default: // Custom
                    endZoneFactor = 2.0;
                    endZoneSpacingMm = beamDepthMm / 4.0;
                    endZoneLenDesc = "2h";
                    colConfineSpacingMm = 100.0;
                    colMidSpacingMm = 200.0;
                    colConfineLenDesc = "max(H/6, D, 450mm)";
                    break;
            }

            return new CodeLookupResult
            {
                CodeName = GetCodeDisplayName(code),
                TensionDevLengthMm = Math.Round(UnitConversion.FeetToMm(tensionDevFt), 0),
                CompressionDevLengthMm = Math.Round(UnitConversion.FeetToMm(compressionDevFt), 0),
                TensionLapMm = Math.Round(UnitConversion.FeetToMm(tensionLapFt), 0),
                CompressionLapMm = Math.Round(UnitConversion.FeetToMm(compressionLapFt), 0),
                BeamEndZoneLength = endZoneLenDesc,
                BeamEndZoneSpacingMm = Math.Round(endZoneSpacingMm, 0),
                ColumnConfineLength = colConfineLenDesc,
                ColumnConfineSpacingMm = Math.Round(colConfineSpacingMm, 0),
                ColumnMidSpacingMm = Math.Round(colMidSpacingMm, 0),
                Hook90ExtMm = Math.Round(12 * barDiaMm, 0),
                Hook135ExtMm = Math.Round(6 * barDiaMm, 0),
                BendRadiusMm = Math.Round(5 * barDiaMm, 0),
                LapMultiplier = Math.Round(lapK, 1),
                DevMultiplier = Math.Round(devK, 1),
            };
        }

        /// <summary>
        /// Returns results for ALL codes for the comparison table.
        /// </summary>
        public static List<CodeLookupResult> CalculateAll(
            ConcreteGrade grade, SteelGrade steel, double barDiaMm,
            double beamDepthMm = 600, double columnDimMm = 400)
        {
            var results = new List<CodeLookupResult>();
            foreach (DesignCodeStandard code in new[] {
                DesignCodeStandard.ACI318,
                DesignCodeStandard.AS3600,
                DesignCodeStandard.EC2,
                DesignCodeStandard.NZS3101 })
            {
                results.Add(Calculate(code, grade, steel, barDiaMm, beamDepthMm, columnDimMm));
            }
            return results;
        }

        public static double GetTensionLapMultiplier(DesignCodeStandard code, ConcreteGrade grade, SteelGrade steel)
        {
            return code switch
            {
                DesignCodeStandard.ACI318 => DesignCodes.GetAciLapMultiplier(grade, steel),
                DesignCodeStandard.AS3600 => DesignCodes.GetAsLapMultiplier(grade, steel),
                DesignCodeStandard.EC2 => DesignCodes.GetEc2LapMultiplier(grade, steel),
                DesignCodeStandard.NZS3101 => DesignCodes.GetNzsLapMultiplier(grade),
                _ => 50.0
            };
        }

        public static string GetCodeDisplayName(DesignCodeStandard code)
        {
            return code switch
            {
                DesignCodeStandard.ACI318 => "ACI 318",
                DesignCodeStandard.AS3600 => "AS 3600",
                DesignCodeStandard.EC2 => "Eurocode 2",
                DesignCodeStandard.NZS3101 => "NZS 3101",
                DesignCodeStandard.Custom => "Custom",
                _ => code.ToString()
            };
        }
    }
}
