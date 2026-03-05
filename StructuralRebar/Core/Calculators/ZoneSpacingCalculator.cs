using System;
using System.Collections.Generic;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    public static class ZoneSpacingCalculator
    {
        /// <summary>
        /// Calculates the required end zone (confinement) spacing and length.
        /// Useful for columns and beams where seismic or standard design codes require closer spacing near joints.
        /// </summary>
        public static List<SpacingZone> CalculateColumnZones(double clearHeight, double maxCrossSectionDim, double barDia, double tieDia, DesignCodeStandard code)
        {
            var zones = new List<SpacingZone>();
            
            // Calculate critical zone length (l_o)
            double l_o = Math.Max(clearHeight / 6.0, Math.Max(maxCrossSectionDim, UnitConversion.MmToFeet(450.0)));
            
            // Calculate confinement spacing (s_o)
            double s_o;
            double s_mid;

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    // ACI 318-19 18.7.5.3: 
                    // s_o is min of: maxCrossSectionDim / 4, 6 * longitudinal bar dia, or s_x
                    s_o = Math.Min(maxCrossSectionDim / 4.0, Math.Min(6.0 * barDia, UnitConversion.MmToFeet(150.0))); // Simplification
                    s_mid = Math.Min(6.0 * barDia, UnitConversion.MmToFeet(150.0)) * 2; // Rough mid-zone estimate
                    break;
                
                case DesignCodeStandard.AS3600:
                    // AS 3600:2018 10.7.4:
                    // Confinement spacing generally min of Dc, 15db, or 300mm. Sometimes 0.5Dc for high ductility.
                    s_o = Math.Min(maxCrossSectionDim / 2.0, Math.Min(15.0 * barDia, UnitConversion.MmToFeet(300.0))); // Simplification
                    s_mid = s_o * 2.0;
                    break;

                case DesignCodeStandard.EC2:
                    // EN 1992-1-1 cl. 9.5.3:
                    // Critical zone spacing: min(min_dim/2, 8*db_long, 175mm)
                    // Mid zone: min(15*db_long, min_dim, 300mm)
                    s_o = Math.Min(maxCrossSectionDim / 2.0, Math.Min(8.0 * barDia, UnitConversion.MmToFeet(175.0)));
                    s_mid = Math.Min(15.0 * barDia, Math.Min(maxCrossSectionDim, UnitConversion.MmToFeet(300.0)));
                    break;

                case DesignCodeStandard.NZS3101:
                    // NZS 3101:2006 cl. 10.4.7:
                    // Confinement zone = max(D, H/6, 450mm)
                    // End zone spacing: d/4 ≈ maxDim/4, abs max 100mm for ductile
                    // Mid zone spacing: d/2, max 200mm
                    s_o = Math.Min(maxCrossSectionDim / 4.0, UnitConversion.MmToFeet(100.0));
                    s_mid = Math.Min(maxCrossSectionDim / 2.0, UnitConversion.MmToFeet(200.0));
                    break;

                default: // Custom
                    double customLenFactor = SettingsManager.GetDouble("RebarSuite_CustomDesign", "ZoneLenFactor", 1.0);
                    double customSpaMult = SettingsManager.GetDouble("RebarSuite_CustomDesign", "ZoneSpacing", 6.0);

                    l_o = Math.Max(maxCrossSectionDim * customLenFactor, clearHeight / 6.0);
                    s_o = barDia * customSpaMult;
                    s_mid = s_o * 2.0;
                    break;
            }

            // Bottom Zone
            zones.Add(new SpacingZone(0.0, l_o, s_o, "Bottom Confinement"));

            // Mid Zone
            double midLength = clearHeight - (2 * l_o);
            if (midLength > 0)
            {
                zones.Add(new SpacingZone(l_o, l_o + midLength, s_mid, "Mid Span"));
            }

            // Top Zone
            zones.Add(new SpacingZone(clearHeight - l_o, clearHeight, s_o, "Top Confinement"));

            return zones;
        }

        /// <summary>
        /// Calculates stirrup zone spacing for beams.
        /// End zones use tighter spacing (code-based); mid-zone uses user-specified spacing.
        /// </summary>
        public static List<SpacingZone> CalculateBeamZones(
            double beamLength, double beamDepth, double userSpacing, double startOffset,
            DesignCodeStandard code)
        {
            var zones = new List<SpacingZone>();

            // End zone length per codes (typically 2h from face of support)
            double endZoneLen;
            double endZoneSpacing;

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    // ACI 318-19: Confinement zone = 2h from face of support
                    endZoneLen = Math.Min(2.0 * beamDepth, beamLength / 4.0);
                    endZoneSpacing = Math.Min(beamDepth / 4.0, Math.Min(userSpacing / 2.0, UnitConversion.MmToFeet(150.0)));
                    break;

                case DesignCodeStandard.AS3600:
                    // AS 3600: Typically 2D from support face
                    endZoneLen = Math.Min(2.0 * beamDepth, beamLength / 4.0);
                    endZoneSpacing = Math.Min(beamDepth / 4.0, Math.Min(userSpacing / 2.0, UnitConversion.MmToFeet(150.0)));
                    break;

                case DesignCodeStandard.EC2:
                    // EN 1992-1-1 cl. 9.2.1:
                    // Critical zone = 1.5h from support face
                    // End zone spacing: min(h/4, s/2, 200mm)
                    endZoneLen = Math.Min(1.5 * beamDepth, beamLength / 4.0);
                    endZoneSpacing = Math.Min(beamDepth / 4.0, Math.Min(userSpacing / 2.0, UnitConversion.MmToFeet(200.0)));
                    break;

                case DesignCodeStandard.NZS3101:
                    // NZS 3101:2006: End zone = 2h from support face
                    // End zone spacing: d/4, max 100mm for ductile
                    // Mid zone: d/2, max 200mm
                    endZoneLen = Math.Min(2.0 * beamDepth, beamLength / 4.0);
                    endZoneSpacing = Math.Min(beamDepth / 4.0, Math.Min(userSpacing / 2.0, UnitConversion.MmToFeet(100.0)));
                    break;

                default: // Custom
                    double customLenFactorB = SettingsManager.GetDouble("RebarSuite_CustomDesign", "ZoneLenFactor", 1.0);
                    endZoneLen = Math.Min(customLenFactorB * beamDepth, beamLength / 4.0);
                    endZoneSpacing = userSpacing / 2.0;
                    break;
            }

            double effectiveLen = beamLength - 2.0 * startOffset;
            if (effectiveLen <= 0) return zones;

            // Left end zone
            double leftEnd = Math.Min(endZoneLen, effectiveLen / 2.0);
            zones.Add(new SpacingZone(startOffset, startOffset + leftEnd, endZoneSpacing, "Left End Zone"));

            // Mid zone
            double midStart = startOffset + leftEnd;
            double midEnd = beamLength - startOffset - leftEnd;
            if (midEnd > midStart)
            {
                zones.Add(new SpacingZone(midStart, midEnd, userSpacing, "Mid Zone"));
            }

            // Right end zone
            double rightStart = beamLength - startOffset - leftEnd;
            if (rightStart < beamLength - startOffset)
            {
                zones.Add(new SpacingZone(rightStart, beamLength - startOffset, endZoneSpacing, "Right End Zone"));
            }

            return zones;
        }
    }
}
