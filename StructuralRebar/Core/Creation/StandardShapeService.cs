using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using DBRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    /// <summary>
    /// Centralized service for matching and applying project standard rebar shapes.
    /// Handles geometrical analysis, robust name matching (priority shapes), 
    /// and position correction after shape reassignment.
    /// </summary>
    public class StandardShapeService
    {
        private readonly Document _doc;
        private readonly Dictionary<string, RebarShape> _shapeCache;

        public StandardShapeService(Document doc)
        {
            _doc = doc;
            _shapeCache = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to apply the best matching standard project shape to a rebar element.
        /// Handles position correction to ensure geometry doesn't shift after reassignment.
        /// </summary>
        public bool MatchAndApply(DBRebar rebar, RebarDefinition def, ISet<ElementId> trashShapes = null)
        {
            if (rebar == null || def == null || def.SkipShapeReassignment) return false;

            bool hasHookStart = rebar.GetHookTypeId(0) != ElementId.InvalidElementId;
            bool hasHookEnd = rebar.GetHookTypeId(1) != ElementId.InvalidElementId;

            string expectedName = GetExpectedName(def.Curves, def.Style, hasHookStart, hasHookEnd, def.ShapeNameHint);
            if (string.IsNullOrEmpty(expectedName)) return false;

            // 1. Check if already correct
            var shapeParam = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE);
            if (shapeParam == null || shapeParam.IsReadOnly) return false;

            var currentId = shapeParam.AsElementId();
            if (currentId != ElementId.InvalidElementId)
            {
                var curShape = _doc.GetElement(currentId) as RebarShape;
                if (curShape != null && IsNameMatch(curShape.Name, expectedName))
                    return false; // Already matches
            }

            // 2. Find target shape robustly
            RebarShape target = FindShapeRobustly(expectedName);
            if (target == null) return false;

            // 3. Apply with position preservation
            XYZ centerBefore = GetRebarCenter(rebar);
            BoundingBoxXYZ bbBefore = rebar.get_BoundingBox(null);
            XYZ bbCenterBefore = bbBefore != null ? (bbBefore.Min + bbBefore.Max) / 2.0 : null;

            try
            {
                if (currentId != ElementId.InvalidElementId && currentId != target.Id)
                    trashShapes?.Add(currentId);

                shapeParam.Set(target.Id);
                _doc.Regenerate();

                // 4. Correct position ONLY for specifically requested circular shapes (SP/CT)
                // Linear shapes (00, L, Crk, LL) should TRUST the original curves provided to CreateFromCurves.
                bool isCircular = def.ShapeNameHint == "Shape SP" || def.ShapeNameHint == "Shape CT" || def.IsSpiral;
                
                if (isCircular && centerBefore != null)
                {
                    XYZ centerAfter = GetRebarCenter(rebar);
                    if (centerAfter != null)
                    {
                        XYZ offset = centerBefore - centerAfter;
                        if (offset.GetLength() > 0.001)
                        {
                            ElementTransformUtils.MoveElement(_doc, rebar.Id, offset);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StandardShapeService: Apply failed: {ex.Message}");
                return false;
            }
        }

        public string GetExpectedName(IList<Curve> curves, RebarStyle style, bool hasHookStart, bool hasHookEnd, string hint = null)
        {
            if (!string.IsNullOrEmpty(hint)) return hint;
            if (curves == null || curves.Count == 0) return null;

            if (style == RebarStyle.StirrupTie) return "Shape HT";

            bool both = hasHookStart && hasHookEnd;
            bool any = hasHookStart || hasHookEnd;

            switch (curves.Count)
            {
                case 1 when curves[0] is Line:
                    if (both) return "Shape 90x90";
                    if (hasHookStart) return "Shape 90x0";
                    if (hasHookEnd) return "Shape 0x90";
                    return "Shape 00";

                case 2 when curves.All(c => c is Line):
                    // Use "Shape L" if available, otherwise fallback to "90x0" logic
                    return "Shape L";

                case 3 when curves.All(c => c is Line):
                    XYZ d1 = (curves[0] as Line).Direction;
                    XYZ d2 = (curves[1] as Line).Direction;
                    XYZ d3 = (curves[2] as Line).Direction;
                    bool isU = Math.Abs(d1.DotProduct(d3)) > 0.99 && Math.Abs(d1.DotProduct(d2)) < 0.01;
                    if (isU) return "Shape LL";
                    
                    if (hasHookStart && hasHookEnd) return "Shape 90x90_Crk";
                    if (hasHookStart) return "Shape 90x0_Crk";
                    return "Shape 00_Crk";

                default:
                    return hint;
            }
        }

        public RebarShape FindShapeRobustly(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_shapeCache.TryGetValue(name, out var s)) return s;

            string clean = name.Replace("Shape ", "").Trim();
            if (_shapeCache.TryGetValue(clean, out s)) return s;

            return _shapeCache.Values.FirstOrDefault(v => IsNameMatch(v.Name, name));
        }

        private bool IsNameMatch(string actual, string expected)
        {
            if (string.IsNullOrEmpty(actual)) return false;
            if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;

            string cleanEx = expected.Replace("Shape ", "").Trim();
            if (actual.Equals(cleanEx, StringComparison.OrdinalIgnoreCase)) return true;

            string normAct = actual.Replace("Shape ", "").Replace("x", "0").Trim();
            string normEx = cleanEx.Replace("x", "0").Trim();
            if (normAct.Equals(normEx, StringComparison.OrdinalIgnoreCase)) return true;

            return actual.EndsWith(cleanEx, StringComparison.OrdinalIgnoreCase);
        }

        private XYZ GetRebarCenter(DBRebar rebar)
        {
            try
            {
                var curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
                if (curves.Count > 0)
                {
                    XYZ sum = XYZ.Zero;
                    foreach (var c in curves) sum += (c.GetEndPoint(0) + c.GetEndPoint(1)) / 2.0;
                    return sum / curves.Count;
                }
            } catch { }
            return null;
        }
    }
}
