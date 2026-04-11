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

        /// <summary>Whether to skip horizontal rebar within intersecting floor slabs (compulsory rule).</summary>
        public bool SkipSlabIntersections { get; set; } = true;

        // === LONGITUDINAL LAYERS ===
        /// <summary>Per-layer configurations (one per face/position).</summary>
        public List<RebarLayerConfig> Layers { get; set; } = new();

        // === CONCRETE GRADE (for lap splice / design code calcs) ===
        public ConcreteGrade Grade { get; set; } = ConcreteGrade.C30;

        /// <summary>Enable automatic lap splicing for long elements.</summary>
        public bool EnableLapSplice { get; set; }

        /// <summary>Lap length mode: "Auto" or "Manual".</summary>
        public string LapSpliceMode { get; set; } = "Auto";

        /// <summary>Manual lap length override (feet). Used when mode is "Manual".</summary>
        public double LapSpliceLength { get; set; }

        /// <summary>Stock bar length (feet). 0 = use 12m default.</summary>
        public double StockLength { get; set; }
        public double StockLength_Backing { get; set; }

        // === COLUMN-SPECIFIC ===
        /// <summary>Number of bars along X direction (columns).</summary>
        public int ColumnCountX { get; set; }
        /// <summary>Number of bars along Y direction (columns).</summary>
        public int ColumnCountY { get; set; }
        
        /// <summary>True if this column is circular, bypassing rectangular logic.</summary>
        public bool IsCircularColumn { get; set; }

        public string VerticalBarTypeNameX { get; set; }
        public string VerticalBarTypeNameY { get; set; }

        // === MULTI-LEVEL ===
        /// <summary>Whether to process a stack of columns across multiple levels.</summary>
        public bool MultiLevel { get; set; }

        /// <summary>Crank position: "None", "Upper Column", or "Lower Column".</summary>
        public string CrankPosition { get; set; } = "Upper Column";

        /// <summary>Splice position strategy: "Above Slab" or "Mid Height".</summary>
        public string SplicePosition { get; set; } = "Above Slab";

        /// <summary>Length of lap splice for continuous vertical bars (feet).</summary>
        public double VerticalContinuousSpliceLength { get; set; }

        // === STARTER BARS ===
        /// <summary>Whether to extend starter bars from the bottom column into the foundation.</summary>
        public bool EnableStarterBars { get; set; }

        /// <summary>Bar type name for starter bars (defaults to vertical bar type if null).</summary>
        public string StarterBarTypeName { get; set; }

        /// <summary>Hook type name for starter bar bottom end. Null = no hook.</summary>
        public string StarterHookEndName { get; set; }

        /// <summary>Development/embedment length for starter bars (feet). 0 = auto from code.</summary>
        public double StarterDevLength { get; set; }

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

        // Intersect U-Bars (corner toggle)
        public bool AddIntersectUBars { get; set; }

        // Wall End U-Bars
        public bool AddWallEndUBars { get; set; }
        public string WallEndBarTypeName { get; set; }
        public double WallEndSpacing { get; set; }
        public double WallEndLeg1 { get; set; }
        public double WallEndLeg2 { get; set; }
        
        // === BORED PILE SPECIFIC ===
        public int PileBarCount { get; set; }
        public bool EnableSpiral { get; set; }
        public string PileMainExtensionMode { get; set; } = "Auto";
        public double PileMainExtensionVal { get; set; }
        public double PileTransverseExtensionVal { get; set; }

        // Top U-Bars
        public bool AddTopEndUBars { get; set; }
        public string TopEndBarTypeName { get; set; }
        public double TopEndSpacing { get; set; }
        public double TopEndLeg1 { get; set; }
        public double TopEndLeg2 { get; set; }
        public string TopEndLayer { get; set; } = "Vert External";

        // Bottom U-Bars
        public bool AddBotEndUBars { get; set; }
        public string BotEndBarTypeName { get; set; }
        public double BotEndSpacing { get; set; }
        public double BotEndLeg1 { get; set; }
        public double BotEndLeg2 { get; set; }
        public string BotEndLayer { get; set; } = "Vert External";

        // Distribution Offsets for Top/Bottom U-bars (position from wall edge)
        public double TopBotTopOffset { get; set; }
        public double TopBotBotOffset { get; set; }

        // === MULTI-SPAN BEAMS ===
        /// <summary>Whether to process multiple selected beams as a single continuous unit.</summary>
        public bool MultiSpan { get; set; }

        /// <summary>Individual per-span configurations for continuous beams.</summary>
        public List<SpanRebarConfig> SpanConfigs { get; set; } = new();

        // === ADVANCED OVERRIDES (Span-by-Span Detailing) ===
        public List<SupportOverride> SupportOverrides { get; set; } = new();
        public List<SpanOverride> SpanOverrides { get; set; } = new();

        // === SIDE REBAR (Skin Reinforcement for Beams) ===
        /// <summary>Whether to add side/skin reinforcement bars.</summary>
        public bool EnableSideRebar { get; set; }
        /// <summary>Bar type name for side reinforcement.</summary>
        public string SideRebarTypeName { get; set; }
        /// <summary>Number of rows per side face.</summary>
        public int SideRebarRows { get; set; }

        /// <summary>Spacing for side/perimeter bars (feet).</summary>
        public double SideRebarSpacing { get; set; }

        /// <summary>Hook type name for side/perimeter bars.</summary>
        public string SideRebarHookName { get; set; }

        /// <summary>Whether to override the leg length for side/perimeter bars (Shape LL).</summary>
        public bool EnableSideRebarOverrideLeg { get; set; }

        /// <summary>Leg length override for side/perimeter bars (feet).</summary>
        public double SideRebarLegLength { get; set; }

        /// <summary>Vertical clear gap between multiple rebar layers (T1/T2, B1/B2) (feet).</summary>
        public double LayerGap { get; set; }

        // === SPLICE ZONE FRACTIONS ===
        /// <summary>Top bar splice zone divisor: splice in middle L/N zone (default 3 = L/3).</summary>
        public int TopSpliceZoneDivisor { get; set; } = 3;

        /// <summary>Bottom bar splice zone divisor: splice in L/N zone from supports (default 5 = L/5).</summary>
        public int BottomSpliceZoneDivisor { get; set; } = 5;

        /// <summary>Whether B2 bottom additional bars extend into cantilever zones. Default true.</summary>
        public bool ExtendB2ToCantilever { get; set; } = true;
    }
}
