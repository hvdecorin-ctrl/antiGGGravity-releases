using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Describes exactly one rebar element to be placed.
    /// Pure data — no Revit Document or Element references.
    /// Created by layout generators, consumed by RebarCreationService.
    /// </summary>
    public class RebarDefinition
    {
        /// <summary>Curve loop defining the bar shape (polyline vertices).</summary>
        public List<Curve> Curves { get; set; } = new();

        /// <summary>Standard (longitudinal) or StirrupTie.</summary>
        public RebarStyle Style { get; set; }

        /// <summary>Name of the RebarBarType to use (resolved by CreationService).</summary>
        public string BarTypeName { get; set; }

        /// <summary>Bar diameter in feet (for clearance calculations).</summary>
        public double BarDiameter { get; set; }

        // === ARRAY LAYOUT ===
        /// <summary>Spacing between arrayed bars (feet). Zero = single bar.</summary>
        public double Spacing { get; set; }

        /// <summary>Total length over which to array (feet).</summary>
        public double ArrayLength { get; set; }

        /// <summary>Direction to array bars along (world coordinates).</summary>
        public XYZ ArrayDirection { get; set; }

        /// <summary>Fixed count layout instead of max-spacing. 0 = use spacing.</summary>
        public int FixedCount { get; set; }

        /// <summary>Distribution width for fixed-count layout (feet).</summary>
        public double DistributionWidth { get; set; }

        // === HOOKS ===
        /// <summary>Hook type name at start. Null = no hook.</summary>
        public string HookStartName { get; set; }

        /// <summary>Hook type name at end. Null = no hook.</summary>
        public string HookEndName { get; set; }

        public RebarHookOrientation HookStartOrientation { get; set; } = RebarHookOrientation.Left;
        public RebarHookOrientation HookEndOrientation { get; set; } = RebarHookOrientation.Left;

        // === NORMAL ===
        /// <summary>Normal vector for Rebar.CreateFromCurves (array propagation direction).</summary>
        public XYZ Normal { get; set; }

        // === METADATA ===
        /// <summary>Label for debugging/logging (e.g. "Top Layer", "Stirrup Zone 1").</summary>
        public string Label { get; set; }
    }
}
