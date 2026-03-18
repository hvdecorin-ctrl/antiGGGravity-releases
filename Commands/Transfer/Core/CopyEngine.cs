using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Transfer.Core
{
    /// <summary>
    /// Pre-queries and caches target document data to avoid repeated FilteredElementCollector calls.
    /// Built once, reused for every view in the batch.
    /// </summary>
    public class DependencyCache
    {
        public Dictionary<string, Level> LevelsByName { get; }
        public List<Level> AllLevels { get; }
        public List<ViewFamilyType> AllViewFamilyTypes { get; }
        public Dictionary<string, View> TemplatesByName { get; }
        public List<View> AllTargetViews { get; private set; }

        public DependencyCache(Document targetDoc)
        {
            AllLevels = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            LevelsByName = new Dictionary<string, Level>();
            foreach (var l in AllLevels)
            {
                if (!LevelsByName.ContainsKey(l.Name))
                    LevelsByName[l.Name] = l;
            }

            AllViewFamilyTypes = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .ToList();

            TemplatesByName = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .GroupBy(v => v.Name)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>Refreshes the target view list (call after creating views).</summary>
        public void RefreshTargetViews(Document targetDoc)
        {
            AllTargetViews = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .ToList();
        }

        public Level FindLevel(string name, double elevation)
        {
            if (LevelsByName.TryGetValue(name, out Level match))
                return match;

            return AllLevels.FirstOrDefault(l => Math.Abs(l.Elevation - elevation) < 0.004);
        }

        public ViewFamilyType FindViewFamilyType(string typeName, ViewFamily family)
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                var byName = AllViewFamilyTypes.FirstOrDefault(t => t.Name == typeName && t.ViewFamily == family);
                if (byName != null) return byName;
            }

            return AllViewFamilyTypes.FirstOrDefault(t => t.ViewFamily == family);
        }
    }

    public class CopyEngine
    {
        private readonly Document _sourceDoc;
        private readonly Document _targetDoc;
        private readonly ConflictResolver _conflictResolver;
        private DependencyCache _cache;

        public CopyEngine(Document sourceDoc, Document targetDoc, ConflictResolver conflictResolver)
        {
            _sourceDoc = sourceDoc;
            _targetDoc = targetDoc;
            _conflictResolver = conflictResolver;
        }

        public void BuildCache()
        {
            _cache = new DependencyCache(_targetDoc);
        }

        public DependencyCache GetCache() => _cache;

        // ─── Pipeline Step: Batch Copy Non-Plan View Definitions ───────────
        /// <summary>
        /// Copies all non-plan view definitions WITH their 2D content in a single CopyElements call.
        /// Includes view-specific elements (detail lines, text, fill regions, dimensions) alongside
        /// view IDs so Revit maintains ownership. Uses before/after snapshot + name matching for mapping.
        /// </summary>
        public Dictionary<ElementId, ElementId> BatchCopyViewDefinitions(List<ElementId> sourceViewIds)
        {
            var viewMap = new Dictionary<ElementId, ElementId>();
            if (sourceViewIds == null || sourceViewIds.Count == 0) return viewMap;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            // Collect view IDs AND all their view-specific content in one list
            var allIdsToCopy = new List<ElementId>();
            foreach (var viewId in sourceViewIds)
            {
                allIdsToCopy.Add(viewId);

                // Include all view-specific 2D elements owned by this view
                var viewElements = new FilteredElementCollector(_sourceDoc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementOwnerViewFilter(viewId))
                    .ToElements()
                    .Where(e => !(e is View) && !(e is Viewport))
                    .Select(e => e.Id)
                    .ToList();

                allIdsToCopy.AddRange(viewElements);
            }

            // Snapshot target views BEFORE copy
            var viewIdsBefore = new HashSet<ElementId>(
                new FilteredElementCollector(_targetDoc).OfClass(typeof(View)).ToElementIds());

            using (Transaction t = new Transaction(_targetDoc, "Batch Copy Views With Content"))
            {
                t.Start();
                try
                {
                    ElementTransformUtils.CopyElements(_sourceDoc, allIdsToCopy, _targetDoc, null, options);
                }
                catch (Exception) { }
                t.Commit();
            }

            // Snapshot AFTER — find newly created views
            var newTargetViews = new FilteredElementCollector(_targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !viewIdsBefore.Contains(v.Id) && !v.IsTemplate)
                .ToList();

            // Match source views to new target views by name
            var unmatchedTargets = new List<View>(newTargetViews);
            foreach (ElementId sourceId in sourceViewIds)
            {
                View sourceView = _sourceDoc.GetElement(sourceId) as View;
                if (sourceView == null) continue;

                var match = unmatchedTargets.FirstOrDefault(v => v.Name == sourceView.Name)
                         ?? unmatchedTargets.FirstOrDefault(v => v.Name.StartsWith(sourceView.Name));

                if (match != null)
                {
                    viewMap[sourceId] = match.Id;
                    unmatchedTargets.Remove(match);
                }
            }

            return viewMap;
        }

        // ─── Pipeline Step: Batch Create Plan Views ────────────────────────
        /// <summary>
        /// Creates all plan views via ViewPlan.Create() in a single transaction.
        /// </summary>
        public Dictionary<ElementId, ElementId> BatchCreatePlanViews(List<View> sourcePlanViews)
        {
            var viewMap = new Dictionary<ElementId, ElementId>();
            if (sourcePlanViews == null || sourcePlanViews.Count == 0) return viewMap;

            using (Transaction t = new Transaction(_targetDoc, "Batch Create Plan Views"))
            {
                t.Start();

                foreach (View sourceView in sourcePlanViews)
                {
                    try
                    {
                        ViewPlan sourcePlan = sourceView as ViewPlan;
                        if (sourcePlan == null) continue;

                        ViewFamily viewFamily;
                        if (sourceView.ViewType == ViewType.CeilingPlan)
                            viewFamily = ViewFamily.CeilingPlan;
                        else if (sourceView.ViewType == ViewType.EngineeringPlan)
                            viewFamily = ViewFamily.StructuralPlan;
                        else
                            viewFamily = ViewFamily.FloorPlan;

                        Level sourceLevel = sourcePlan.GenLevel;
                        if (sourceLevel == null) continue;

                        Level targetLevel = _cache.FindLevel(sourceLevel.Name, sourceLevel.Elevation);
                        ElementId targetLevelId;

                        if (targetLevel != null)
                        {
                            targetLevelId = targetLevel.Id;
                        }
                        else
                        {
                            Level newLevel = Level.Create(_targetDoc, sourceLevel.Elevation);
                            try { newLevel.Name = sourceLevel.Name; } catch { }
                            targetLevelId = newLevel.Id;
                            _cache.AllLevels.Add(newLevel);
                            _cache.LevelsByName[newLevel.Name] = newLevel;
                        }

                        string sourceTypeName = _sourceDoc.GetElement(sourceView.GetTypeId())?.Name;
                        ViewFamilyType vft = _cache.FindViewFamilyType(sourceTypeName, viewFamily);
                        if (vft == null) continue;

                        ViewPlan newPlan = ViewPlan.Create(_targetDoc, vft.Id, targetLevelId);
                        if (newPlan == null) continue;

                        try { newPlan.Scale = sourcePlan.Scale; } catch { }
                        try { newPlan.DetailLevel = sourcePlan.DetailLevel; } catch { }

                        try
                        {
                            newPlan.CropBoxActive = sourcePlan.CropBoxActive;
                            if (sourcePlan.CropBoxActive)
                            {
                                newPlan.CropBox = sourcePlan.CropBox;
                                newPlan.CropBoxVisible = sourcePlan.CropBoxVisible;
                            }
                        }
                        catch { }

                        try
                        {
                            ElementId srcTemplateId = sourcePlan.ViewTemplateId;
                            if (srcTemplateId != null && srcTemplateId != ElementId.InvalidElementId)
                            {
                                View srcTemplate = _sourceDoc.GetElement(srcTemplateId) as View;
                                if (srcTemplate != null && _cache.TemplatesByName.TryGetValue(srcTemplate.Name, out View targetTemplate))
                                {
                                    newPlan.ViewTemplateId = targetTemplate.Id;
                                }
                            }
                        }
                        catch { }

                        viewMap[sourceView.Id] = newPlan.Id;
                    }
                    catch (Exception) { }
                }

                t.Commit();
            }

            return viewMap;
        }

        // ─── Pipeline Step: Batch Copy View Contents ───────────────────────
        /// <summary>
        /// Copies all view-specific 2D elements and viewers in a single transaction.
        /// </summary>
        public void BatchCopyViewContents(Dictionary<ElementId, ElementId> viewMap)
        {
            if (viewMap == null || viewMap.Count == 0) return;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            using (Transaction t = new Transaction(_targetDoc, "Batch Copy View Contents"))
            {
                t.Start();

                foreach (var kvp in viewMap)
                {
                    try
                    {
                        View sourceView = _sourceDoc.GetElement(kvp.Key) as View;
                        View targetView = _targetDoc.GetElement(kvp.Value) as View;
                        if (sourceView == null || targetView == null) continue;

                        // 2D elements (excluding viewers, views, viewports)
                        var elementsToCopy = new FilteredElementCollector(_sourceDoc)
                            .WhereElementIsNotElementType()
                            .WherePasses(new ElementOwnerViewFilter(kvp.Key))
                            .ToElements()
                            .Where(e => !(e is View) && !(e is Viewport) &&
                                   (e.Category == null || e.Category.Id.GetIdValue() != (long)BuiltInCategory.OST_Viewers))
                            .Select(e => e.Id)
                            .ToList();

                        if (elementsToCopy.Count > 0)
                        {
                            try
                            {
                                ElementTransformUtils.CopyElements(sourceView, elementsToCopy, targetView, Transform.Identity, options);
                            }
                            catch (Exception) { }
                        }

                        // Viewers (callout/section marks)
                        var viewersToCopy = new FilteredElementCollector(_sourceDoc, kvp.Key)
                            .OfCategory(BuiltInCategory.OST_Viewers)
                            .WhereElementIsNotElementType()
                            .ToList();

                        if (viewersToCopy.Count > 0)
                        {
                            var nonViewSpecific = viewersToCopy
                                .Where(e => e.OwnerViewId == ElementId.InvalidElementId)
                                .Select(e => e.Id).ToList();

                            if (nonViewSpecific.Count > 0)
                            {
                                try
                                {
                                    ElementTransformUtils.CopyElements(_sourceDoc, nonViewSpecific, _targetDoc, null, options);
                                }
                                catch (Exception) { }
                            }

                            var viewSpecific = viewersToCopy
                                .Where(e => e.OwnerViewId != ElementId.InvalidElementId)
                                .Select(e => e.Id).ToList();

                            if (viewSpecific.Count > 0)
                            {
                                try
                                {
                                    ElementTransformUtils.CopyElements(sourceView, viewSpecific, targetView, Transform.Identity, options);
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                    catch (Exception) { }
                }

                t.Commit();
            }
        }

        // ─── Pipeline Step: Batch Rename Views ─────────────────────────────
        public void BatchRenameViews(Dictionary<ElementId, ElementId> viewMap)
        {
            if (viewMap == null || viewMap.Count == 0) return;

            using (Transaction t = new Transaction(_targetDoc, "Batch Rename Views"))
            {
                t.Start();

                foreach (var kvp in viewMap)
                {
                    try
                    {
                        View sourceView = _sourceDoc.GetElement(kvp.Key) as View;
                        View targetView = _targetDoc.GetElement(kvp.Value) as View;
                        if (sourceView == null || targetView == null) continue;

                        string targetName = _conflictResolver.GetUniqueViewName(sourceView.Name);
                        try { targetView.Name = targetName; } catch { }
                    }
                    catch (Exception) { }
                }

                t.Commit();
            }
        }

        // ─── Fallback: Single View Copy ────────────────────────────────────
        public ElementId CopySingleView(ElementId sourceViewId)
        {
            if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId) return ElementId.InvalidElementId;

            View sourceView = _sourceDoc.GetElement(sourceViewId) as View;
            if (sourceView == null) return ElementId.InvalidElementId;

            if (sourceView.ViewType == ViewType.FloorPlan || sourceView.ViewType == ViewType.CeilingPlan || sourceView.ViewType == ViewType.EngineeringPlan)
            {
                if (_cache == null) BuildCache();
                var planMap = BatchCreatePlanViews(new List<View> { sourceView });
                if (planMap.TryGetValue(sourceViewId, out ElementId newPlanId))
                {
                    var singleMap = new Dictionary<ElementId, ElementId> { { sourceViewId, newPlanId } };
                    BatchCopyViewContents(singleMap);

                    using (Transaction tName = new Transaction(_targetDoc, "Rename View"))
                    {
                        tName.Start();
                        View newView = _targetDoc.GetElement(newPlanId) as View;
                        if (newView != null)
                        {
                            string name = _conflictResolver.GetUniqueViewName(sourceView.Name);
                            try { newView.Name = name; } catch { }
                        }
                        tName.Commit();
                    }
                    return newPlanId;
                }
                return ElementId.InvalidElementId;
            }

            var batchMap = BatchCopyViewDefinitions(new List<ElementId> { sourceViewId });
            if (batchMap.TryGetValue(sourceViewId, out ElementId newViewId))
            {
                var singleMap = new Dictionary<ElementId, ElementId> { { sourceViewId, newViewId } };
                BatchCopyViewContents(singleMap);

                using (Transaction tName = new Transaction(_targetDoc, "Rename View"))
                {
                    tName.Start();
                    View newView = _targetDoc.GetElement(newViewId) as View;
                    if (newView != null)
                    {
                        string name = _conflictResolver.GetUniqueViewName(sourceView.Name);
                        try { newView.Name = name; } catch { }
                    }
                    tName.Commit();
                }
                return newViewId;
            }

            return ElementId.InvalidElementId;
        }

        // ─── Copy Families ─────────────────────────────────────────────────
        public void CopyFamilies(List<ElementId> familyIds)
        {
            if (familyIds == null || familyIds.Count == 0) return;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            using (Transaction t = new Transaction(_targetDoc, "Transfer Families"))
            {
                t.Start();
                try
                {
                    ElementTransformUtils.CopyElements(_sourceDoc, familyIds, _targetDoc, null, options);
                }
                catch (Exception) { }
                t.Commit();
            }
        }

        // ─── Copy System Family Types ─────────────────────────────────────
        /// <summary>
        /// Copies system family types (Wall types, Floor types, Rebar Shapes, etc.)
        /// by their ElementType IDs using ElementTransformUtils.CopyElements.
        /// </summary>
        public void CopySystemTypes(List<ElementId> typeIds)
        {
            if (typeIds == null || typeIds.Count == 0) return;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            using (Transaction t = new Transaction(_targetDoc, "Transfer System Types"))
            {
                t.Start();
                try
                {
                    ElementTransformUtils.CopyElements(_sourceDoc, typeIds, _targetDoc, null, options);
                }
                catch (Exception) { }
                t.Commit();
            }
        }
    }

    public class CustomCopyHandler : IDuplicateTypeNamesHandler
    {
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            return DuplicateTypeAction.UseDestinationTypes;
        }
    }
}
