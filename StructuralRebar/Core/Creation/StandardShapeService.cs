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
        
        /// <summary>
        /// Multi-standard alias table — Primary lookup priority from User Standard (Pic 1).
        /// These are the 5 geometry-specific shapes that MUST be matched.
        /// </summary>
        private static readonly Dictionary<string, string[]> ShapeAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // ── 1. Closed rectangular stirrup / tie ────────────────────────────────
            // US: M_T1  |  AS: HT  |  BS: 52
            ["Shape HT"]      = new[] { "Shape HT", "M_T1", "HT", "52" },

            // ── 2. L-bar (2 segments at 90°) ───────────────────────────────────────
            // US: M_17A  |  AS: L   |  BS: 11
            ["Shape L"]       = new[] { "Shape L", "M_17A", "L", "11" },

            // ── 3. U-bar / hairpin (3 segments, U-shape) ───────────────────────────
            // US: M_17   |  AS: LL  |  BS: 21
            ["Shape LL"]      = new[] { "Shape LL", "M_17", "LL", "21" },

            // ── 4. Circular tie (closed loop) ──────────────────────────────────────
            // US: M_T3  |  AS: CT  |  BS: 75
            ["Shape CT"]      = new[] { "Shape CT", "M_T3", "CT", "75" },

            // ── 5. Spiral ──────────────────────────────────────────────────────────
            // US: M_SP  |  AS: SP  |  BS: 77
            ["Shape SP"]      = new[] { "Shape SP", "M_SP", "77" },
        };

        public StandardShapeService(Document doc)
        {
            _doc = doc;
            _shapeCache = new Dictionary<string, RebarShape>(StringComparer.OrdinalIgnoreCase);
            var shapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>();
            
            foreach (var s in shapes)
            {
                string name = s.Name ?? "";
                _shapeCache[name] = s;
                // Also index by sanitized name (no "Shape", no spaces)
                string clean = name.Replace("Shape", "").Replace(" ", "").Trim();
                if (!string.IsNullOrEmpty(clean) && !_shapeCache.ContainsKey(clean))
                    _shapeCache[clean] = s;
            }
        }

        /// <summary>
        /// Attempts to apply the best matching standard project shape to a rebar element.
        /// Handles position correction to ensure geometry doesn't shift after reassignment.
        /// </summary>
        public bool MatchAndApply(DBRebar rebar, RebarDefinition def, ISet<ElementId> trashShapes = null)
        {
            if (rebar == null || def == null || def.SkipShapeReassignment) return false;

            // Use the DEFINITION's hook names (user intent) — not the rebar's actual hooks
            // (Revit may auto-assign hooks to both ends when using certain shapes)
            bool hasHookStart = !string.IsNullOrEmpty(def.HookStartName);
            bool hasHookEnd = !string.IsNullOrEmpty(def.HookEndName);

            string expectedName = GetExpectedName(def.Curves, def.Style, hasHookStart, hasHookEnd, def.ShapeNameHint, def.HookStartName, def.HookEndName);
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
                {
                    // Only trash auto-generated shapes, never user-created standard shapes
                    var curShapeForTrash = _doc.GetElement(currentId) as RebarShape;
                    bool isStandard = curShapeForTrash != null && (curShapeForTrash.Name ?? "").StartsWith("Shape", StringComparison.OrdinalIgnoreCase);
                    if (!isStandard) trashShapes?.Add(currentId);
                }

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

        public string GetExpectedName(IList<Curve> curves, RebarStyle style, bool hasHookStart, bool hasHookEnd, string hint = null, string hookStartName = null, string hookEndName = null)
        {
            if (!string.IsNullOrEmpty(hint)) return hint;
            if (curves == null || curves.Count == 0) return null;

            if (style == RebarStyle.StirrupTie) return "Shape HT";

            string sCode = hasHookStart ? GetHookAngleCode(hookStartName) : "0";
            string eCode = hasHookEnd ? GetHookAngleCode(hookEndName) : "0";

            switch (curves.Count)
            {
                case 1 when curves[0] is Line:
                    if (sCode == "0" && eCode == "0") return "Shape 00";
                    return $"Shape {sCode}x{eCode}";

                case 2 when curves.All(c => c is Line):
                    return "Shape L";

                case 3 when curves.All(c => c is Line):
                    XYZ d1 = (curves[0] as Line).Direction;
                    XYZ d2 = (curves[1] as Line).Direction;
                    XYZ d3 = (curves[2] as Line).Direction;
                    bool isU = Math.Abs(d1.DotProduct(d3)) > 0.99 && Math.Abs(d1.DotProduct(d2)) < 0.01;
                    if (isU) return "Shape LL";
                    
                    if (hasHookStart && hasHookEnd) return $"Shape {sCode}x{eCode}_Crk";
                    if (hasHookStart) return $"Shape {sCode}x0_Crk";
                    return "Shape 00_Crk";

                default:
                    return hint;
            }
        }

        /// <summary>
        /// Extracts the hook angle code from a hook type name.
        /// e.g. "Standard - 180 deg" → "180", "Seismic - 135 deg" → "135", anything else → "90".
        /// </summary>
        private static string GetHookAngleCode(string hookName)
        {
            if (string.IsNullOrEmpty(hookName)) return "90";
            if (hookName.Contains("180")) return "180";
            if (hookName.Contains("135")) return "135";
            return "90";
        }

        public RebarShape FindShapeRobustly(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // 1. Exact match (Priority 1)
            if (_shapeCache.TryGetValue(name, out var s)) return s;

            // 2. Multi-standard alias lookup (Priority 2 - Pic 1 Standard)
            // If we have a canonical name (e.g. "Shape L"), immediately check regional 
            // equivalents before trying broader fuzzy matching.
            if (ShapeAliases.TryGetValue(name, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (_shapeCache.TryGetValue(alias, out s)) return s;
                }
            }

            // 3. Sanitized match (Priority 3) — Remove "Shape" prefix and spaces
            string clean = name.Replace("Shape", "").Replace(" ", "").Trim();
            if (_shapeCache.TryGetValue(clean, out s)) return s;

            // 4. Exact Alias Lookup again using the cleaned name 
            // (e.g. "L" should also find M_17A, H, 11)
            if (ShapeAliases.TryGetValue("Shape " + clean, out var aliasesAlt))
            {
                foreach (var alias in aliasesAlt)
                {
                    if (_shapeCache.TryGetValue(alias, out s)) return s;
                    string cleanAlias = alias.Replace("Shape", "").Replace(" ", "").Trim();
                    if (_shapeCache.TryGetValue(cleanAlias, out s)) return s;
                }
            }

            // 5. Full scan with IsNameMatch (Priority 4) — Handles "M_" prefixes, etc.
            s = _shapeCache.Values.FirstOrDefault(v => IsNameMatch(v.Name, name));
            if (s != null) return s;

            return null;
        }

        /// <summary>
        /// Used by the Setup tool to report which project shape is being used as a fallback.
        /// </summary>
        public string FindAliasInProject(string canonicalName)
        {
            var shape = FindShapeRobustly(canonicalName);
            if (shape == null) return null;
            if (shape.Name.Equals(canonicalName, StringComparison.OrdinalIgnoreCase)) return null;
            return shape.Name;
        }

        private bool IsNameMatch(string actual, string expected)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expected)) return false;
            
            // Normalize both
            string a = actual.Replace("Shape", "").Replace(" ", "").Trim();
            string e = expected.Replace("Shape", "").Replace(" ", "").Trim();
            
            if (a.Equals(e, StringComparison.OrdinalIgnoreCase)) return true;

            // Handle Revit naming variants (00 vs M_00)
            a = a.Replace("M_", "");
            e = e.Replace("M_", "");
            if (a.Equals(e, StringComparison.OrdinalIgnoreCase)) return true;

            // Check if one is a "Specific" subset of the other (e.g. "0x180" matches "180" only if the context allows)
            // But we must be careful not to match "180x180" to "180x0"
            // So we skip 'EndsWith' as it proved too broad in this context.
            return false;
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
