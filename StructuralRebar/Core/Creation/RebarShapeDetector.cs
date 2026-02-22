using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    public static class RebarShapeDetector
    {
        /// <summary>
        /// Attempts to find a matching RebarShape in the document for the given curves.
        /// This allows the rebar to correctly populate schedules instead of remaining
        /// as an unassigned shape-driven item. 
        /// Currently highly simplified: primarily detects straight bars (Shape 00)
        /// and standard closed stirrups (Shape M_T1).
        /// </summary>
        public static RebarShape FindMatchingShape(Document doc, IList<Curve> curves, RebarStyle style)
        {
            var shapeCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .ToList();

            if (curves == null || curves.Count == 0) return null;

            // Scenario 1: Straight Bar (Typically 1 curve)
            if (curves.Count == 1 && curves[0] is Line)
            {
                // Most standard libraries name straight bars "00", "M_00", "T1", or "Shape 1"
                return shapeCollector.FirstOrDefault(s => 
                    s.Name.Contains("00") || 
                    s.Name.Equals("M_00", StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Equals("1", StringComparison.OrdinalIgnoreCase));
            }

            // Scenario 2: Standard Closed Rectangular Stirrup (Usually 4 or 5 curves)
            if (style == RebarStyle.StirrupTie && curves.Count >= 4)
            {
                // T1, M_T1, or Shape 51 are common names
                return shapeCollector.FirstOrDefault(s => 
                    s.Name.Contains("T1") || 
                    s.Name.Contains("51") ||
                    s.Name.Equals("M_T1", StringComparison.OrdinalIgnoreCase));
            }

            // Scenario 3: L-bar or U-bar 
            if (curves.Count == 2)
            {
                // L-shape: Shape 11, M_11
                return shapeCollector.FirstOrDefault(s => 
                    s.Name.Contains("11") || 
                    s.Name.Equals("M_11", StringComparison.OrdinalIgnoreCase));
            }
            if (curves.Count == 3)
            {
                // U-shape: Shape 21, M_21
                return shapeCollector.FirstOrDefault(s => 
                    s.Name.Contains("21") || 
                    s.Name.Equals("M_21", StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        /// <summary>
        /// Attempts to force a rebar element to adopt a specific shape if possible.
        /// </summary>
        public static void ApplyShapeIfValid(Rebar rebar, RebarShape shape)
        {
            if (shape == null || rebar == null) return;

            try
            {
                // We use SetShapeId but it might fail if the geometry isn't perfectly mapped
                // to the shape's driving parameters. 
                // In production, we'd need a robust parameter-mapping engine. 
                // For Phase 4, we wrap in a try-catch to 'fail-open' back to free-form if it fails.
                rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE).Set(shape.Id);
            }
            catch { }
        }
    }
}
