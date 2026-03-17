using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Utilities;
using DBRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    public class RebarCreationService
    {
        private readonly Document _doc;
        private readonly Dictionary<string, RebarBarType> _barTypes;
        private readonly Dictionary<string, RebarHookType> _hookTypes;
        private readonly StandardShapeService _shapeService;

        public RebarCreationService(Document doc)
        {
            _doc = doc;
            _barTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

            _hookTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

            _shapeService = new StandardShapeService(doc);
        }

        public List<ElementId> PlaceRebar(Element host, List<RebarDefinition> definitions)
        {
            var results = new List<ElementId>();
            var generatedShapesToDelete = new HashSet<ElementId>();

            foreach (var def in definitions)
            {
                if (def == null || def.Curves == null || def.Curves.Count == 0) continue;

                try
                {
                    RebarBarType barType = ResolveBarType(def.BarTypeName);
                    if (barType == null) continue;

                    RebarHookType hookStart = ResolveHookType(def.HookStartName);
                    RebarHookType hookEnd = ResolveHookType(def.HookEndName);

                    RebarShape standardShape = null;
                    if (!def.SkipShapeReassignment)
                    {
                        standardShape = _shapeService.FindShapeRobustly(
                            _shapeService.GetExpectedName(def.Curves, def.Style, hookStart != null, hookEnd != null, def.ShapeNameHint));
                    }

                    DBRebar rebar = null;
                    try 
                    {
                        if (standardShape != null)
                        {
                            rebar = RevitCompatibility.CreateRebarWithShape(_doc, standardShape, barType, hookStart, hookEnd, host, def.Normal, def.Curves, def.HookStartOrientation, def.HookEndOrientation);
                        }
                    }
                    catch (Exception ex1)
                    {
                        System.Diagnostics.Debug.WriteLine($"RebarCreationService: CreateFromCurvesAndShape failed for '{def.Label}': {ex1.Message}");
                    }

                    if (rebar == null)
                    {
                        try 
                        {
                            rebar = RevitCompatibility.CreateRebar(_doc, def.Style, barType, hookStart, hookEnd, host, def.Normal, def.Curves, def.HookStartOrientation, def.HookEndOrientation, true, true);
                        }
                        catch (Exception ex2)
                        {
                            System.Diagnostics.Debug.WriteLine($"RebarCreationService: CreateFromCurves ALSO failed for '{def.Label}': {ex2.Message}");
                        }
                    }

                    if (rebar != null)
                    {
                        var paramStart = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_START_TYPE);
                        var paramEnd = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_HOOK_END_TYPE);
                        bool forcedStart = (hookStart == null) && paramStart != null && paramStart.AsElementId() != ElementId.InvalidElementId;
                        bool forcedEnd = (hookEnd == null) && paramEnd != null && paramEnd.AsElementId() != ElementId.InvalidElementId;

                        if (forcedStart || forcedEnd)
                        {
                            var shapeIdToTrash = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE)?.AsElementId();
                            if (shapeIdToTrash != null && shapeIdToTrash != ElementId.InvalidElementId) generatedShapesToDelete.Add(shapeIdToTrash);

                            _doc.Delete(rebar.Id);
                            rebar = RevitCompatibility.CreateRebar(_doc, def.Style, barType, hookStart, hookEnd, host, def.Normal, def.Curves, def.HookStartOrientation, def.HookEndOrientation, false, true);
                        }
                    }

                    if (rebar != null)
                    {
                        if (hookStart == null) try { rebar.SetHookTypeId(0, ElementId.InvalidElementId); } catch { }
                        if (hookEnd == null) try { rebar.SetHookTypeId(1, ElementId.InvalidElementId); } catch { }

                        if (def.IsSpiral)
                        {
                            // Setting parameters by BuiltInName failed (possibly different Revit version)
                            // We will rely on the RebarShape parameters for now.
                            // var pitchParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_SPIRAL_PITCH);
                        }

                        var accessor = rebar.GetShapeDrivenAccessor();
                        if (def.FixedCount > 1 && def.DistributionWidth > 0)
                        {
                            accessor.SetLayoutAsFixedNumber(def.FixedCount, def.DistributionWidth, true, true, true);
                        }
                        else if (def.FixedCount == 1 && def.DistributionWidth > 0)
                        {
                            ElementTransformUtils.MoveElement(_doc, rebar.Id, def.ArrayDirection * (def.DistributionWidth / 2.0));
                        }
                        else if (def.Spacing > 0 && def.ArrayLength > 0)
                        {
                            accessor.SetLayoutAsMaximumSpacing(def.Spacing, def.ArrayLength, true, true, true);
                        }

                        if (def.OverrideHookLength && def.HookLengthOverride > 0)
                        {
                            try {
                                rebar.EnableHookLengthOverride(true);
                                _doc.Regenerate();
                                rebar.GetOverridableHookParameters(out ISet<ElementId> sL, out ISet<ElementId> sO, out ISet<ElementId> eL, out ISet<ElementId> eO);
                                foreach (Parameter p in rebar.Parameters) {
                                    if (p.IsReadOnly || p.StorageType != StorageType.Double) continue;
                                    if ((sL != null && sL.Contains(p.Id)) || (eL != null && eL.Contains(p.Id))) p.Set(def.HookLengthOverride);
                                }
                            } catch { }
                        }

                        if (!string.IsNullOrEmpty(def.Comment))
                        {
                            Parameter commentsParam = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (commentsParam != null && !commentsParam.IsReadOnly) commentsParam.Set(def.Comment);
                        }

                        try {
                            _shapeService.MatchAndApply(rebar, def, generatedShapesToDelete);

                            if (hookStart != null)
                            {
                                if (rebar.GetHookTypeId(0) == ElementId.InvalidElementId) rebar.SetHookTypeId(0, hookStart.Id);
                                RevitCompatibility.SetHookOrientationCompatible(rebar, 0, def.HookStartOrientation);
                            }
                            if (hookEnd != null)
                            {
                                if (rebar.GetHookTypeId(1) == ElementId.InvalidElementId) rebar.SetHookTypeId(1, hookEnd.Id);
                                RevitCompatibility.SetHookOrientationCompatible(rebar, 1, def.HookEndOrientation);
                            }
                        } catch { }

                        results.Add(rebar.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RebarCreationService: Failed to place '{def.Label}': {ex.Message}");
                }
            }

            foreach (var shapeId in generatedShapesToDelete)
            {
                if (shapeId == null || shapeId == ElementId.InvalidElementId) continue;
                try { _doc.Delete(shapeId); } catch { }
            }

            return results;
        }

        public void DeleteExistingRebar(Element host)
        {
            var rebarHostData = RebarHostData.GetRebarHostData(host);
            if (rebarHostData == null) return;
            foreach (var rebar in rebarHostData.GetRebarsInHost())
            {
                try { _doc.Delete(new List<ElementId> { rebar.Id }); } catch { }
            }
        }

        private XYZ GetGeometricCenter(IList<Curve> curves)
        {
            if (curves == null || curves.Count == 0) return null;
            XYZ sum = XYZ.Zero;
            foreach (var c in curves) sum += c.GetEndPoint(0);
            return sum / curves.Count;
        }

        private XYZ GetRebarCenter(DBRebar rebar)
        {
            try
            {
                var curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
                var arcs = curves.OfType<Arc>().ToList();
                if (arcs.Count > 0)
                {
                    return arcs.OrderByDescending(a => a.Radius).First().Center;
                }
                foreach (var c in curves)
                {
                    if (c.GetType().Name.Contains("CylindricalHelix"))
                    {
                        dynamic helix = c;
                        return helix.BasePoint;
                    }
                }
            }
            catch { }
            var bb = rebar.get_BoundingBox(null);
            return bb != null ? (bb.Min + bb.Max) / 2.0 : null;
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
