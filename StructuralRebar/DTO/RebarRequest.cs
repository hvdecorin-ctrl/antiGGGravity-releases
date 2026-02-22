using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Encapsulates the full user intent for a rebar generation operation.
    /// Built from ViewModel state, consumed by RebarEngine.
    /// All dimensions already converted to FEET by the ViewModel.
    /// </summary>
    public class RebarRequest
    {
        // === TARGET ===
        /// <summary>What type of host element to reinforce.</summary>
        public ElementHostType HostType { get; set; }

        /// <summary>Element IDs of selected host elements.</summary>
        public List<ElementId> SelectedElementIds { get; set; } = new();

        /// <summary>Whether to delete existing rebar on host before generating.</summary>
        public bool RemoveExisting { get; set; }

        /// <summary>Selected design code for structural calculations.</summary>
        public DesignCodeStandard DesignCode { get; set; } = DesignCodeStandard.ACI318;

        // === TRANSVERSE (Stirrups / Ties) ===
        /// <summary>Name of bar type for stirrups/ties.</summary>
        public string TransverseBarTypeName { get; set; }

        /// <summary>Stirrup spacing (feet).</summary>
        public double TransverseSpacing { get; set; }

        public string VerticalBarTypeName { get; set; }
        public double VerticalSpacing { get; set; }

        /// <summary>Start offset along element from start point (feet).</summary>
        public double TransverseStartOffset { get; set; }

        /// <summary>Hook type name for stirrup start.</summary>
        public string TransverseHookStartName { get; set; }

        /// <summary>Hook type name for stirrup end.</summary>
        public string TransverseHookEndName { get; set; }

        /// <summary>Enable zone spacing (end zone densification).</summary>
        public bool EnableZoneSpacing { get; set; }

        /// <summary>End zone spacing override (feet). 0 = use d/4 rule.</summary>
        public double EndZoneSpacing { get; set; }

        // === WALL-SPECIFIC ===
        /// <summary>End offset along element from end point (feet).</summary>
        public double TransverseEndOffset { get; set; }

        /// <summary>Extension at top of vertical bars (feet).</summary>
        public double VerticalTopExtension { get; set; }

        /// <summary>Extension at bottom of vertical bars (feet).</summary>
        public double VerticalBottomExtension { get; set; }

        /// <summary>Whether vertical hook start bends outward.</summary>
        public bool TransverseHookStartOut { get; set; }

        /// <summary>Whether vertical hook end bends outward.</summary>
        public bool TransverseHookEndOut { get; set; }

        // === LONGITUDINAL LAYERS ===
        /// <summary>Per-layer configurations (one per face/position).</summary>
        public List<RebarLayerConfig> Layers { get; set; } = new();

        // === CONCRETE GRADE (for lap splice / design code calcs) ===
        public ConcreteGrade Grade { get; set; } = ConcreteGrade.C30;

        /// <summary>Enable automatic lap splicing for long elements.</summary>
        public bool EnableLapSplice { get; set; }

        /// <summary>Stock bar length (feet). 0 = use 12m default.</summary>
        public double StockLength { get; set; }
        public double StockLength_Backing { get; set; }

        // === COLUMN-SPECIFIC ===
        /// <summary>Number of bars along X direction (columns).</summary>
        public int ColumnCountX { get; set; }
        /// <summary>Number of bars along Y direction (columns).</summary>
        public int ColumnCountY { get; set; }

        public string VerticalBarTypeNameX { get; set; }
        public string VerticalBarTypeNameY { get; set; }

        // === FOOTING-SPECIFIC ===
        /// <summary>Number of bottom bars (footings).</summary>
        public int BottomBarCount { get; set; }
        /// <summary>Number of top bars (footings).</summary>
        public int TopBarCount { get; set; }

        /// <summary>Layer placement configuration (Centre, Both faces, etc).</summary>
        public string WallLayerConfig { get; set; }

        // === WALL CORNER SPECIFIC ===
        public double LegLength1 { get; set; }
        public double LegLength2 { get; set; }
        public bool AddTrimmers { get; set; }
        public string TrimmerBarTypeName { get; set; }
    }
}
