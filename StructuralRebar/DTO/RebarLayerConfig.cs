using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Configuration for one rebar layer within a host element.
    /// Describes bar type, spacing, offset from centerline, and hooks for that layer.
    /// All dimensions in FEET.
    /// </summary>
    public class RebarLayerConfig
    {
        /// <summary>Which face of the element this layer is on.</summary>
        public RebarLayerFace Face { get; set; }

        public RebarSide Side { get; set; }

        // === BAR TYPE ===
        /// <summary>Name of the RebarBarType for vertical/longitudinal bars.</summary>
        public string VerticalBarTypeName { get; set; }

        /// <summary>Name of the RebarBarType for horizontal/transverse bars.</summary>
        public string HorizontalBarTypeName { get; set; }

        // === SPACING (feet) ===
        public double VerticalSpacing { get; set; }
        public double HorizontalSpacing { get; set; }
        public int VerticalCount { get; set; }
        public int HorizontalCount { get; set; }

        // === OFFSETS (feet) ===
        /// <summary>Offset along host normal from centerline (positive = exterior).</summary>
        public double VerticalOffset { get; set; }
        public double HorizontalOffset { get; set; }

        // === EDGE OFFSETS (feet) ===
        /// <summary>Start offset along element length from start point.</summary>
        public double StartOffset { get; set; }
        /// <summary>End offset along element length from end point.</summary>
        public double EndOffset { get; set; }
        /// <summary>Top offset from top of element.</summary>
        public double TopOffset { get; set; }
        /// <summary>Bottom offset from bottom of element.</summary>
        public double BottomOffset { get; set; }

        // === EXTENSIONS (feet) ===
        /// <summary>Extend bar beyond top of element (e.g. into slab).</summary>
        public double TopExtension { get; set; }
        /// <summary>Extend bar beyond bottom of element (e.g. into footing).</summary>
        public double BottomExtension { get; set; }

        // === HOOKS ===
        public string HookStartName { get; set; }
        public string HookEndName { get; set; }
        public bool HookStartOutward { get; set; }
        public bool HookEndOutward { get; set; }

        /// <summary>If true, override the default hook length with HookLengthOverride.</summary>
        public bool OverrideHookLength { get; set; }
        /// <summary>Custom hook length (feet). Used when OverrideHookLength is true.</summary>
        public double HookLengthOverride { get; set; }

        /// <summary>Resolved diameter (feet) — calculated by engine.</summary>
        public double BarDiameter_Backing { get; set; }
    }
}
