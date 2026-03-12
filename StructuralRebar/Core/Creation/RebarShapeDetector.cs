using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    /// <summary>
    /// Detects the correct standard RebarShape for generated rebar based on
    /// curve topology, style, and hook configuration.
    ///
    /// Standard project shapes:
    ///   Shape 00       — Straight bar (no hooks)
    ///   Shape 90x0     — Straight bar with 90° hook at one end
    ///   Shape 90x90    — Straight bar with 90° hooks at both ends
    ///   Shape HT       — Closed stirrup/tie
    ///   Shape 0x0_Crk  — Cranked bar (no hooks)
    ///   Shape 90x0_Crk — Cranked bar with 90° hook
    /// </summary>
    public static class RebarShapeDetector
    {
        /// <summary>
        /// Determines the expected standard shape name based on curve topology,
        /// rebar style, and hook configuration.
        /// </summary>
        public static string GetExpectedShapeName(IList<Curve> curves, RebarStyle style,
            bool hasHookStart, bool hasHookEnd)
        {
            if (curves == null || curves.Count == 0) return null;

            // Stirrup/Tie → Shape HT
            if (style == RebarStyle.StirrupTie)
                return "Shape HT";

            bool hasBothHooks = hasHookStart && hasHookEnd;
            bool hasAnyHook = hasHookStart || hasHookEnd;

            switch (curves.Count)
            {
                case 1 when curves[0] is Line:
                    // Single straight line
                    if (hasBothHooks) return "Shape 90x90";
                    return hasAnyHook ? "Shape 90x0" : "Shape 00";

                case 2 when curves.All(c => c is Line):
                    // L-shape (2 line segments) — always 90x0
                    return "Shape 90x0";

                case 3 when curves.All(c => c is Line):
                    // Cranked bar (3 line segments)
                    return hasAnyHook ? "Shape 90x0_Crk" : "Shape 0x0_Crk";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Attempts to reassign a rebar's shape to the correct standard shape.
        /// Only acts if the current shape doesn't match the expected standard shape.
        /// Returns true if reassignment was performed successfully.
        /// </summary>
        public static bool TryApplyStandardShape(Document doc, Rebar rebar,
            IList<Curve> curves, RebarStyle style, Dictionary<string, RebarShape> shapeCache)
        {
            if (rebar == null) return false;

            // Detect actual hooks from rebar element (crucial if some were stripped)
            bool actualHookStart = rebar.GetHookTypeId(0) != ElementId.InvalidElementId;
            bool actualHookEnd = rebar.GetHookTypeId(1) != ElementId.InvalidElementId;

            string expectedName = GetExpectedShapeName(curves, style, actualHookStart, actualHookEnd);
            if (expectedName == null) return false;

            // Check current shape — skip if already correct
            var shapeParam = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE);
            if (shapeParam == null) return false;

            var currentShapeId = shapeParam.AsElementId();
            if (currentShapeId != ElementId.InvalidElementId)
            {
                var currentShape = doc.GetElement(currentShapeId) as RebarShape;
                if (currentShape != null &&
                    currentShape.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                    return false; // Already the correct standard shape
            }

            // Look up the target standard shape
            if (!shapeCache.TryGetValue(expectedName, out var standardShape) || standardShape == null)
                return false;

            try
            {
                if (!shapeParam.IsReadOnly)
                {
                    shapeParam.Set(standardShape.Id);
                    return true;
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(
                    $"RebarShapeDetector: Failed to apply '{expectedName}' to rebar {rebar.Id}");
            }

            return false;
        }
    }
}
