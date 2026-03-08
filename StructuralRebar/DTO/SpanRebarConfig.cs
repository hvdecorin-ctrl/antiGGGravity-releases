using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Configuration for one individual span within a continuous beam.
    /// Used by the Multi-Span engine to override global beam settings.
    /// </summary>
    public class SpanRebarConfig
    {
        /// <summary>Index of the span in the continuous line (0-based).</summary>
        public int SpanIndex { get; set; }

        /// <summary>The Revit ElementId of the beam for this span.</summary>
        public long ElementId { get; set; }

        /// <summary>Number of top bars specifically within this span (if different from global).</summary>
        public int TopBarCount { get; set; }

        /// <summary>Number of bottom bars specifically within this span.</summary>
        public int BottomBarCount { get; set; }

        /// <summary>Stirrup spacing for the mid-zone of this span (feet).</summary>
        public double MidSpacing { get; set; }

        /// <summary>Stirrup spacing for the end-zones of this span (feet).</summary>
        public double EndSpacing { get; set; }

        /// <summary>Left curtailment percentage of span length (e.g. 0.25 for L/4).</summary>
        public double CurtailmentLeft { get; set; } = 0.25;

        /// <summary>Right curtailment percentage of span length.</summary>
        public double CurtailmentRight { get; set; } = 0.25;
    }
}
