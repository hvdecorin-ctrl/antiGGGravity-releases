using System;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Defines a spacing zone for stirrup/tie layout.
    /// Elements are divided into zones with different stirrup densities
    /// (e.g. end zone d/4, mid zone d/2 per NZS 3101).
    /// All dimensions in FEET.
    /// </summary>
    public readonly struct SpacingZone
    {
        /// <summary>Start offset from element start point (feet).</summary>
        public readonly double StartOffset;

        /// <summary>End offset from element start point (feet).</summary>
        public readonly double EndOffset;

        /// <summary>Stirrup spacing within this zone (feet).</summary>
        public readonly double Spacing;

        /// <summary>Zone label for debugging (e.g. "End Zone Start", "Mid Zone").</summary>
        public readonly string Label;

        public SpacingZone(double startOffset, double endOffset, double spacing, string label = "")
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            Spacing = spacing;
            Label = label;
        }

        /// <summary>Length of this zone (feet).</summary>
        public double Length => EndOffset - StartOffset;

        /// <summary>Estimated bar count within this zone.</summary>
        public int EstimatedBarCount => Spacing > 0
            ? Math.Max(1, (int)Math.Ceiling(Length / Spacing) + 1)
            : 1;
    }
}
