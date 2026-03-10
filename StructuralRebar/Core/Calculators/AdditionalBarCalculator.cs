using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Calculators
{
    /// <summary>
    /// Calculates the start and end positions for non-continuous (additional) reinforcement bars.
    /// Implements standard engineering detailing for Hogging (top, over supports) and Sagging (bottom, mid-span) bars.
    /// </summary>
    public static class AdditionalBarCalculator
    {
        /// <summary>
        /// Calculates segments for Top Additional Bars (Hogging / Negative moment).
        /// These bars are placed over supports and extend into adjacent spans by L/3.
        /// If a span is a cantilever (at the end of the chain), the bar extends to the full end.
        /// Overlapping segments over short spans are automatically merged.
        /// </summary>
        /// <param name="totalLength">The full length of the straight beam geometry (face to face of end columns).</param>
        /// <param name="clearSpans">List of all clear spans (Start, End) inside the beam.</param>
        /// <returns>A list of (Start, End) segment bounds relative to the beam's start point.</returns>
        public static List<(double Start, double End)> CalculateTopAdditionalSegments(
            double totalLength, 
            List<(double Start, double End)> clearSpans,
            bool isStartCantilever,
            bool isEndCantilever)
        {
            var segments = new List<(double Start, double End)>();
            if (clearSpans == null || clearSpans.Count == 0) return segments;

            int supportCount = clearSpans.Count + 1;
            if (isStartCantilever) supportCount--;
            if (isEndCantilever) supportCount--;

            if (supportCount <= 0) return segments;

            for (int i = 0; i < supportCount; i++)
            {
                var seg = GetTopSegmentForSupport(i, totalLength, clearSpans, isStartCantilever, isEndCantilever);
                if (seg.HasValue) segments.Add(seg.Value);
            }

            // Merge overlapping segments
            return MergeSegments(segments);
        }

        /// <summary>
        /// Calculates segments for Bottom Additional Bars (Sagging / Positive moment).
        /// These bars are placed in the mid-span and stop 0.1L away from both supports.
        /// </summary>
        /// <param name="clearSpans">List of all clear spans (Start, End) inside the beam.</param>
        /// <returns>A list of (Start, End) segment bounds relative to the beam's start point.</returns>
        public static List<(double Start, double End)> CalculateBottomAdditionalSegments(List<(double Start, double End)> clearSpans)
        {
            var segments = new List<(double Start, double End)>();
            if (clearSpans == null || clearSpans.Count == 0) return segments;

            foreach (var span in clearSpans)
            {
                double spanL = span.End - span.Start;
                double offset = spanL * 0.1;

                segments.Add((span.Start + offset, span.End - offset));
            }

            return segments;
        }

        /// <summary>
        /// Merges any overlapping or touching segments into continuous segments.
        /// Useful when spans are shorter than the required L/3 laps.
        /// </summary>
        private static List<(double Start, double End)> MergeSegments(List<(double Start, double End)> segments)
        {
            if (segments.Count <= 1) return segments;

            // Sort by start position
            var sorted = segments.OrderBy(s => s.Start).ToList();
            var merged = new List<(double Start, double End)>();
            
            var current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                if (current.End >= next.Start) // Overlap or touching
                {
                    // Extend current segment to the max end position
                    current = (current.Start, Math.Max(current.End, next.End));
                }
                else
                {
                    // No overlap, commit the current segment and start a new one
                    merged.Add(current);
                    current = next;
                }
            }
            
            merged.Add(current);
            return merged;
        }

        /// <summary>
        /// Gets the exact segment bounds for a specific support index, considering cantilevers.
        /// </summary>
        public static (double Start, double End)? GetTopSegmentForSupport(
            int supportIndex, 
            double totalLength, 
            List<(double Start, double End)> clearSpans,
            bool isStartCantilever,
            bool isEndCantilever)
        {
            if (clearSpans == null || clearSpans.Count == 0) return null;

            // Determine which spans are to the left and right of this physical support
            int leftSpanIdx = isStartCantilever ? supportIndex : supportIndex - 1;
            int rightSpanIdx = isStartCantilever ? supportIndex + 1 : supportIndex;

            double? leftL = (leftSpanIdx >= 0 && leftSpanIdx < clearSpans.Count) ? (clearSpans[leftSpanIdx].End - clearSpans[leftSpanIdx].Start) : (double?)null;
            double? rightL = (rightSpanIdx >= 0 && rightSpanIdx < clearSpans.Count) ? (clearSpans[rightSpanIdx].End - clearSpans[rightSpanIdx].Start) : (double?)null;

            // Near face is the end of the left span. Far face is the start of the right span.
            double nearFace;
            if (leftL.HasValue) nearFace = clearSpans[leftSpanIdx].End;
            else if (rightL.HasValue) nearFace = clearSpans[rightSpanIdx].Start; // Failsafe
            else return null;

            double farFace;
            if (rightL.HasValue) farFace = clearSpans[rightSpanIdx].Start;
            else if (leftL.HasValue) farFace = clearSpans[leftSpanIdx].End; // Failsafe
            else farFace = nearFace;

            double startPos = nearFace;
            double endPos = farFace;

            // Left Extension
            if (leftL.HasValue)
            {
                bool isCant = (supportIndex == 0 && isStartCantilever);
                double ext = isCant ? leftL.Value : leftL.Value / 3.0;
                startPos = nearFace - ext;
            }
            else
            {
                startPos = 0.0;
            }

            // Right Extension
            if (rightL.HasValue)
            {
                int totalPhysicalSupports = clearSpans.Count + 1 - (isStartCantilever ? 1 : 0) - (isEndCantilever ? 1 : 0);
                bool isCant = (supportIndex == totalPhysicalSupports - 1 && isEndCantilever);
                double ext = isCant ? rightL.Value : rightL.Value / 3.0;
                endPos = farFace + ext;
            }
            else
            {
                endPos = totalLength;
            }

            // Failsafe bounds
            startPos = Math.Max(0, startPos);
            endPos = Math.Min(totalLength, endPos);

            return (startPos, endPos);
        }

        /// <summary>
        /// Gets the exact 0.1L offset segment bounds for a specific clear span index.
        /// </summary>
        public static (double Start, double End)? GetBottomSegmentForSpan(int spanIndex, List<(double Start, double End)> clearSpans)
        {
            if (clearSpans == null || spanIndex < 0 || spanIndex >= clearSpans.Count) return null;

            var span = clearSpans[spanIndex];
            double spanL = span.End - span.Start;
            double offset = spanL * 0.1;

            return (span.Start + offset, span.End - offset);
        }
    }
}
