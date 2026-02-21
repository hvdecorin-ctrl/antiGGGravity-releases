using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using DBRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    /// <summary>
    /// Thin Revit API wrapper. Takes RebarDefinitions and creates actual Rebar elements.
    /// Pre-caches RebarBarType and RebarHookType dictionaries for performance.
    /// </summary>
    public class RebarCreationService
    {
        private readonly Document _doc;
        private readonly Dictionary<string, RebarBarType> _barTypes;
        private readonly Dictionary<string, RebarHookType> _hookTypes;

        public RebarCreationService(Document doc)
        {
            _doc = doc;

            // Pre-cache all bar types (read ONCE)
            _barTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

            // Pre-cache all hook types (read ONCE)
            _hookTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Places a list of RebarDefinitions on a host element.
        /// Returns element IDs of created rebar.
        /// </summary>
        public List<ElementId> PlaceRebar(Element host, List<RebarDefinition> definitions)
        {
            var results = new List<ElementId>();

            foreach (var def in definitions)
            {
                if (def == null || def.Curves == null || def.Curves.Count == 0)
                    continue;

                try
                {
                    RebarBarType barType = ResolveBarType(def.BarTypeName);
                    if (barType == null) continue;

                    RebarHookType hookStart = ResolveHookType(def.HookStartName);
                    RebarHookType hookEnd = ResolveHookType(def.HookEndName);

                    DBRebar rebar = DBRebar.CreateFromCurves(
                        _doc,
                        def.Style,
                        barType,
                        hookStart,
                        hookEnd,
                        host,
                        def.Normal,
                        def.Curves,
                        def.HookStartOrientation,
                        def.HookEndOrientation,
                        true, true);

                    if (rebar != null)
                    {
                        var accessor = rebar.GetShapeDrivenAccessor();

                        if (def.FixedCount > 1 && def.DistributionWidth > 0)
                        {
                            // Longitudinal bars: fixed count layout
                            accessor.SetLayoutAsFixedNumber(def.FixedCount, def.DistributionWidth, true, true, true);
                        }
                        else if (def.FixedCount == 1 && def.DistributionWidth > 0)
                        {
                            // Single bar: center it
                            ElementTransformUtils.MoveElement(_doc, rebar.Id, def.ArrayDirection * (def.DistributionWidth / 2.0));
                        }
                        else if (def.Spacing > 0 && def.ArrayLength > 0)
                        {
                            // Stirrups/ties: max spacing layout
                            accessor.SetLayoutAsMaximumSpacing(def.Spacing, def.ArrayLength, true, true, true);
                        }

                        results.Add(rebar.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RebarCreationService: Failed to place '{def.Label}': {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Deletes all existing rebar hosted on an element.
        /// </summary>
        public void DeleteExistingRebar(Element host)
        {
            var rebarHostData = RebarHostData.GetRebarHostData(host);
            if (rebarHostData == null) return;

            foreach (var rebar in rebarHostData.GetRebarsInHost())
            {
                try { _doc.Delete(new List<ElementId> { rebar.Id }); } catch { }
            }
        }

        private RebarBarType ResolveBarType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _barTypes.TryGetValue(name, out var t) ? t : null;
        }

        private RebarHookType ResolveHookType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _hookTypes.TryGetValue(name, out var t) ? t : null;
        }
    }
}
