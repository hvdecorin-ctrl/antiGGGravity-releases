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
                    if (hasHookStart) return "Shape 90x0";
                    if (hasHookEnd) return "Shape 0x90";
                    return "Shape 00";

                case 2 when curves.All(c => c is Line):
                    // L-shape (2 line segments) — always 90x0 (as shape topology)
                    return "Shape 90x0";

                case 3 when curves.All(c => c is Line):
                    // Cranked bar (3 line segments)
                    if (hasHookStart) return "Shape 90x0_Crk";
                    if (hasHookEnd) return "Shape 0x90_Crk";
                    return "Shape 0x0_Crk";

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
            if (rebar == null || curves == null || curves.Count == 0) return false;

            // Detect actual hooks from rebar element
            bool actualHookStart = rebar.GetHookTypeId(0) != ElementId.InvalidElementId;
            bool actualHookEnd = rebar.GetHookTypeId(1) != ElementId.InvalidElementId;

            string expectedCode = GetExpectedShapeName(curves, style, actualHookStart, actualHookEnd);
            if (expectedCode == null) return false;

            // Check current shape — skip if it already matches our expected code
            var shapeParam = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE);
            if (shapeParam == null) return false;

            var currentShapeId = shapeParam.AsElementId();
            if (currentShapeId != ElementId.InvalidElementId)
            {
                var currentShape = doc.GetElement(currentShapeId) as RebarShape;
                if (currentShape != null && IsShapeMatch(currentShape.Name, expectedCode))
                    return false; // Already correct
            }

            // Look up the target standard shape using robust matching
            RebarShape targetShape = FindShapeRobustly(expectedCode, shapeCache);
            if (targetShape == null)
            {
                System.Diagnostics.Debug.WriteLine($"RebarShapeDetector: Could not find project shape matching '{expectedCode}'");
                return false;
            }

            try
            {
                if (!shapeParam.IsReadOnly)
                {
                    shapeParam.Set(targetShape.Id);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"RebarShapeDetector: Failed to apply '{targetShape.Name}' (code {expectedCode}) to rebar {rebar.Id}: {ex.Message}");
            }

            return false;
        }

        private static bool IsShapeMatch(string actualName, string expectedCode)
        {
            if (string.IsNullOrEmpty(actualName)) return false;
            
            // Exact match (e.g. "Shape 00" == "Shape 00")
            if (actualName.Equals(expectedCode, StringComparison.OrdinalIgnoreCase)) return true;

            // Simple code match (e.g. "00" matches "Shape 00")
            string cleanExpected = expectedCode.Replace("Shape ", "").Trim();
            if (actualName.Equals(cleanExpected, StringComparison.OrdinalIgnoreCase)) return true;

            // Suffix match (e.g. "M_00" or "Rebar 00" matches code "00")
            if (actualName.EndsWith(cleanExpected, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure it's not a partial word match (e.g. "100" should not match "00")
                int prefixLen = actualName.Length - cleanExpected.Length;
                if (prefixLen > 0 && char.IsLetterOrDigit(actualName[prefixLen - 1]))
                    return false;
                    
                return true;
            }

            return false;
        }

        private static RebarShape FindShapeRobustly(string expectedCode, Dictionary<string, RebarShape> shapeCache)
        {
            // 1. Try direct cache lookup
            if (shapeCache.TryGetValue(expectedCode, out var shape)) return shape;

            // 2. Try lookup by clean code (no "Shape " prefix)
            string cleanCode = expectedCode.Replace("Shape ", "").Trim();
            if (shapeCache.TryGetValue(cleanCode, out shape)) return shape;

            // 3. Fuzzy scan of the entire project shapes
            return shapeCache.Values.FirstOrDefault(s => IsShapeMatch(s.Name, expectedCode));
        }
    }
}
