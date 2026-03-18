using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Modules
{
    public class TransferEngineWrapper
    {
        private readonly Document _sourceDoc;
        private readonly Document _targetDoc;
        private readonly string _prefix;
        private readonly ConflictResolver _conflictResolver;
        private readonly CopyEngine _copyEngine;
        private readonly SheetBuilder _sheetBuilder;

        public TransferEngineWrapper(Document sourceDoc, Document targetDoc, TransferOptions options)
        {
            _sourceDoc = sourceDoc;
            _targetDoc = targetDoc;
            _prefix = options.DuplicateHandlingPrefix ? options.PrefixString : "";

            _conflictResolver = new ConflictResolver(targetDoc, _prefix);
            _copyEngine = new CopyEngine(sourceDoc, targetDoc, _conflictResolver);
            _sheetBuilder = new SheetBuilder(sourceDoc, targetDoc, _conflictResolver);
        }

        public void ExecuteTransfer(List<ViewTransferItem> viewsToCopy, List<SheetTransferItem> sheetsToCopy, List<FamilyTransferItem> familiesToCopy, List<SystemFamilyTypeItem> systemTypesToCopy = null)
        {
            if ((viewsToCopy == null || viewsToCopy.Count == 0) &&
                (sheetsToCopy == null || sheetsToCopy.Count == 0) &&
                (familiesToCopy == null || familiesToCopy.Count == 0) &&
                (systemTypesToCopy == null || systemTypesToCopy.Count == 0))
                return;

            // ── Snapshot: Record existing views before transfer ──────────
            var viewIdsBefore = new HashSet<ElementId>(
                new FilteredElementCollector(_targetDoc).OfClass(typeof(View)).ToElementIds());

            // ── Step 1: Build Dependency Cache ───────────────────────────
            _copyEngine.BuildCache();

            // ── Step 2: Copy System Family Types (1 transaction) ─────────
            if (systemTypesToCopy != null && systemTypesToCopy.Count > 0)
            {
                var sysIds = systemTypesToCopy
                    .Where(s => s.SourceTypeId != null && s.SourceTypeId != ElementId.InvalidElementId)
                    .Select(s => s.SourceTypeId)
                    .Distinct()
                    .ToList();
                _copyEngine.CopySystemTypes(sysIds);
            }

            // ── Step 3: Copy Families (1 transaction) ─────────────────────
            if (familiesToCopy != null && familiesToCopy.Count > 0)
            {
                var idsToCopy = familiesToCopy
                    .Select(f => f.SourceSymbolId ?? f.SourceFamilyId)
                    .Where(id => id != null && id != ElementId.InvalidElementId)
                    .Distinct()
                    .ToList();
                _copyEngine.CopyFamilies(idsToCopy);
            }

            // ── Step 3: Collect & Categorize ONLY explicitly selected views ──
            var planViews = new List<View>();
            var nonPlanViewIds = new List<ElementId>();

            if (viewsToCopy != null)
            {
                foreach (var v in viewsToCopy)
                {
                    ElementId viewId = v.SourceViewId;
                    if (viewId == null || viewId == ElementId.InvalidElementId) continue;

                    View view = _sourceDoc.GetElement(viewId) as View;
                    if (view == null) continue;

                    if (view.ViewType == ViewType.FloorPlan ||
                        view.ViewType == ViewType.CeilingPlan ||
                        view.ViewType == ViewType.EngineeringPlan)
                    {
                        planViews.Add(view);
                    }
                    else
                    {
                        nonPlanViewIds.Add(viewId);
                    }
                }
            }

            // ── Step 4: Batch Copy Non-Plan View Definitions (1 transaction) ──
            Dictionary<ElementId, ElementId> viewMap = new Dictionary<ElementId, ElementId>();

            if (nonPlanViewIds.Count > 0)
            {
                var batchResult = _copyEngine.BatchCopyViewDefinitions(nonPlanViewIds);
                foreach (var kvp in batchResult)
                    viewMap[kvp.Key] = kvp.Value;
            }

            // ── Step 5: Batch Create Plan Views (1 transaction) ──────────
            if (planViews.Count > 0)
            {
                var planResult = _copyEngine.BatchCreatePlanViews(planViews);
                foreach (var kvp in planResult)
                    viewMap[kvp.Key] = kvp.Value;
            }

            // ── Step 6: Copy View Contents for Plan Views Only (1 transaction) ──
            // Non-plan views already have content from BatchCopyViewDefinitions.
            // Plan views (created empty via ViewPlan.Create) need content copied separately.
            if (planViews.Count > 0)
            {
                var planViewMap = new Dictionary<ElementId, ElementId>();
                foreach (var pv in planViews)
                {
                    if (viewMap.ContainsKey(pv.Id))
                        planViewMap[pv.Id] = viewMap[pv.Id];
                }
                if (planViewMap.Count > 0)
                    _copyEngine.BatchCopyViewContents(planViewMap);
            }

            // ── Step 7: Batch Rename All Views (1 transaction) ───────────
            if (viewMap.Count > 0)
            {
                _copyEngine.BatchRenameViews(viewMap);
            }

            // ── Step 8: Rebuild Sheets ───────────────────────────────────
            if (sheetsToCopy != null && sheetsToCopy.Count > 0)
            {
                // For views on sheets that weren't explicitly selected: 
                // copy them on-demand via CopySingleView fallback
                foreach (var sheetInfo in sheetsToCopy)
                {
                    ViewSheet sourceSheet = _sourceDoc.GetElement(sheetInfo.SourceSheetId) as ViewSheet;
                    if (sourceSheet == null) continue;

                    foreach (ElementId vpId in sourceSheet.GetAllViewports())
                    {
                        if (vpId == null || vpId == ElementId.InvalidElementId) continue;
                        Viewport vp = _sourceDoc.GetElement(vpId) as Viewport;
                        if (vp == null || vp.ViewId == null || vp.ViewId == ElementId.InvalidElementId) continue;
                        if (viewMap.ContainsKey(vp.ViewId)) continue;

                        try
                        {
                            ElementId newViewId = _copyEngine.CopySingleView(vp.ViewId);
                            if (newViewId != null && newViewId != ElementId.InvalidElementId)
                            {
                                viewMap[vp.ViewId] = newViewId;
                                continue;
                            }
                        }
                        catch (Exception) { }

                        // Name-based search as final fallback
                        _copyEngine.GetCache()?.RefreshTargetViews(_targetDoc);
                        View sourceView = _sourceDoc.GetElement(vp.ViewId) as View;
                        if (sourceView == null) continue;

                        string sourceName = sourceView.Name;
                        string prefixedName = string.IsNullOrEmpty(_prefix) ? sourceName : _prefix + sourceName;

                        var targetViews = _copyEngine.GetCache()?.AllTargetViews;
                        if (targetViews == null) continue;

                        var match = targetViews.FirstOrDefault(v => v.Name == prefixedName)
                                 ?? targetViews.FirstOrDefault(v => v.Name == sourceName)
                                 ?? targetViews.FirstOrDefault(v => v.Name.StartsWith(prefixedName + " ("));

                        if (match != null)
                            viewMap[vp.ViewId] = match.Id;
                    }
                }

                // Build sheets
                ElementId defaultTbId = new FilteredElementCollector(_targetDoc)
                                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                                            .WhereElementIsElementType()
                                            .FirstElementId();

                foreach (var sheetInfo in sheetsToCopy)
                {
                    ViewSheet sourceSheet = _sourceDoc.GetElement(sheetInfo.SourceSheetId) as ViewSheet;
                    if (sourceSheet != null)
                    {
                        _sheetBuilder.RebuildSheet(sourceSheet, defaultTbId, viewMap);
                    }
                }
            }

            // ── Step 9: Cleanup — Delete spare views ─────────────────────
            // Views created as side effects of batch CopyElements that aren't
            // placed on any sheet and weren't explicitly requested.
            CleanupSpareViews(viewIdsBefore, viewMap);
        }

        /// <summary>
        /// Deletes views that were created as side effects during the transfer
        /// but aren't placed on any sheet and weren't in the intentional viewMap.
        /// </summary>
        private void CleanupSpareViews(HashSet<ElementId> viewIdsBefore, Dictionary<ElementId, ElementId> viewMap)
        {
            // Collect all intentionally-kept view IDs
            var keepViewIds = new HashSet<ElementId>(viewMap.Values);

            // Collect all views currently on sheets (via viewports)
            var allSheets = new FilteredElementCollector(_targetDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            foreach (var sheet in allSheets)
            {
                foreach (ElementId vpId in sheet.GetAllViewports())
                {
                    if (vpId == null || vpId == ElementId.InvalidElementId) continue;
                    Viewport vp = _targetDoc.GetElement(vpId) as Viewport;
                    if (vp != null)
                        keepViewIds.Add(vp.ViewId);
                }
            }

            // Find NEW views (created during transfer) that should be deleted
            var allViewsNow = new FilteredElementCollector(_targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && !(v is ViewSheet))
                .ToList();

            var viewsToDelete = allViewsNow
                .Where(v => !viewIdsBefore.Contains(v.Id)   // New (created during transfer)
                         && !keepViewIds.Contains(v.Id))     // Not on any sheet and not in viewMap
                .Select(v => v.Id)
                .ToList();

            if (viewsToDelete.Count > 0)
            {
                using (Transaction t = new Transaction(_targetDoc, "Cleanup Spare Views"))
                {
                    t.Start();
                    foreach (ElementId id in viewsToDelete)
                    {
                        try { _targetDoc.Delete(id); } catch { }
                    }
                    t.Commit();
                }
            }
        }
    }
}
