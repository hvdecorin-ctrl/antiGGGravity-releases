using System;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Constants and unit-conversion helpers for the Auto Dimension tool.
    /// All default values match the original Python script.
    /// </summary>
    public class AutoDimSettings
    {
        // ---- Category toggles ----
        public bool DimGrids { get; set; } = true;
        public bool DimWalls { get; set; } = true;
        public bool DimColumns { get; set; } = true;
        public bool DimFoundations { get; set; } = true;

        // ---- Offsets (mm, scaled by view-scale/100 at runtime) ----
        public double Offset1Mm { get; set; } = 1000;   // first dim row
        public double Offset2Mm { get; set; } = 800;    // second dim row
        public double OffsetChain1Mm { get; set; } = 500;  // grid chain row 1
        public double OffsetChainGapMm { get; set; } = 800; // gap between grid chain rows

        // ---- Tolerances (mm) ----
        public double ZeroTolMm { get; set; } = 5;       // edge "on grid"
        public double IntersectTolMm { get; set; } = 50;  // grid "intersects" element
        public double MaxSnapDistMm { get; set; } = 10000; // max snap to grid

        // ---- Clustering / collision (mm) ----
        public double DedupTolMm { get; set; } = 100;
        public double ClusterGapMm { get; set; } = 1500;
        public double CollisionShiftMm { get; set; } = 500;
        public int CollisionMaxPasses { get; set; } = 8;

        /// <summary>
        /// Applies view-scale multiplier to all offset/collision values.
        /// Call once before dimensioning with viewScale = view.Scale.
        /// </summary>
        public void ApplyViewScale(int viewScale)
        {
            double m = viewScale / 100.0;
            Offset1Mm *= m;
            Offset2Mm *= m;
            OffsetChain1Mm *= m;
            OffsetChainGapMm *= m;
            CollisionShiftMm *= m;
        }
    }

    public static class AutoDimUnits
    {
        public static double MmToFt(double mm) => mm / 304.8;
        public static double FtToMm(double ft) => ft * 304.8;

        /// <summary>
        /// Extracts a long value from ElementId, compatible with Revit 2025+ API.
        /// </summary>
        public static long GetIdValue(Autodesk.Revit.DB.ElementId id)
            => id.Value;

        public static long GetIdValue(Autodesk.Revit.DB.Element elem)
            => elem.Id.Value;
    }
}
