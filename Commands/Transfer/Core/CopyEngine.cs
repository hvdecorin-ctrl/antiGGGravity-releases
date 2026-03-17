using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class CopyEngine
    {
        private readonly Document _sourceDoc;
        private readonly Document _targetDoc;
        private readonly ConflictResolver _conflictResolver;

        public CopyEngine(Document sourceDoc, Document targetDoc, ConflictResolver conflictResolver)
        {
            _sourceDoc = sourceDoc;
            _targetDoc = targetDoc;
            _conflictResolver = conflictResolver;
        }

        public ElementId CopySingleView(ElementId sourceViewId)
        {
            if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId) return ElementId.InvalidElementId;

            View sourceView = _sourceDoc.GetElement(sourceViewId) as View;
            if (sourceView == null) return ElementId.InvalidElementId;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            ElementId initialViewId = ElementId.InvalidElementId;
            ElementId finalViewId = ElementId.InvalidElementId;

            // Step 1: Copy the view definition
            using (Transaction t = new Transaction(_targetDoc, "Transfer View Definition"))
            {
                t.Start();
                ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(_sourceDoc, new List<ElementId> { sourceViewId }, _targetDoc, null, options);
                
                if (copiedIds != null && copiedIds.Count > 0)
                {
                    foreach (ElementId id in copiedIds)
                    {
                        if (id == null || id == ElementId.InvalidElementId) continue;
                        if (_targetDoc.GetElement(id) is View)
                        {
                            initialViewId = id;
                            break;
                        }
                    }
                }
                t.Commit();
            }

            if (initialViewId == null || initialViewId == ElementId.InvalidElementId) return ElementId.InvalidElementId;
            finalViewId = initialViewId;

            // Step 2: Copy 2D contents (Always do this for all view types including Drafting)
            // We use a set comparison to find if Revit creates a superior 'detailed' view automatically.
            ICollection<ElementId> viewsBefore = new FilteredElementCollector(_targetDoc).OfClass(typeof(View)).ToElementIds();
            
            ElementId secondaryViewId = CopyViewSpecificElements(sourceViewId, initialViewId);
            
            ICollection<ElementId> viewsAfter = new FilteredElementCollector(_targetDoc).OfClass(typeof(View)).ToElementIds();
            
            // Identify if any NEW views appeared during the content copy (dependency views)
            ElementId newlyCreatedViewId = viewsAfter.FirstOrDefault(id => !viewsBefore.Contains(id));

            if (newlyCreatedViewId != null && newlyCreatedViewId != ElementId.InvalidElementId)
            {
                finalViewId = newlyCreatedViewId;
                
                // Cleanup: Delete the initial empty view definition if we have a better one
                if (finalViewId != initialViewId)
                {
                    using (Transaction tCleanup = new Transaction(_targetDoc, "Cleanup Redundant View"))
                    {
                        tCleanup.Start();
                        try { _targetDoc.Delete(initialViewId); } catch { }
                        tCleanup.Commit();
                    }
                }
            }
            else if (secondaryViewId != null && secondaryViewId != ElementId.InvalidElementId)
            {
                finalViewId = secondaryViewId;
            }

            // Step 3: Set correct name on the final view
            if (finalViewId != null && finalViewId != ElementId.InvalidElementId)
            {
                using (Transaction tName = new Transaction(_targetDoc, "Finalize View Name"))
                {
                    tName.Start();
                    View finalView = _targetDoc.GetElement(finalViewId) as View;
                    if (finalView != null)
                    {
                        string targetName = _conflictResolver.GetUniqueViewName(sourceView.Name);
                        try { finalView.Name = targetName; } catch { }
                    }
                    tName.Commit();
                }
            }

            return finalViewId;
        }

        public ElementId CopyViewSpecificElements(ElementId sourceViewId, ElementId targetViewId)
        {
            View sourceView = _sourceDoc.GetElement(sourceViewId) as View;
            View targetView = _targetDoc.GetElement(targetViewId) as View;

            if (sourceView == null || targetView == null) return ElementId.InvalidElementId;

            var elementsToCopy = new FilteredElementCollector(_sourceDoc)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementOwnerViewFilter(sourceViewId))
                .ToElements()
                .Where(e => !(e is View) && !(e is Viewport) && (e.Category == null || e.Category.Id.GetIdValue() != (long)BuiltInCategory.OST_Viewers))
                .Select(e => e.Id)
                .ToList();

            var viewersToCopy = new FilteredElementCollector(_sourceDoc, sourceViewId)
                .OfCategory(BuiltInCategory.OST_Viewers)
                .WhereElementIsNotElementType()
                .Select(e => e.Id)
                .ToList();

            if (elementsToCopy.Count == 0 && viewersToCopy.Count == 0) return targetViewId;

            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new CustomCopyHandler());

            ElementId discoveredViewId = ElementId.InvalidElementId;

            using (Transaction t = new Transaction(_targetDoc, "Transfer Contents"))
            {
                t.Start();
                try
                {
                    if (elementsToCopy.Count > 0)
                    {
                        ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceView, elementsToCopy, targetView, Transform.Identity, options);
                        
                        if (copiedIds != null && copiedIds.Count > 0)
                        {
                            foreach (ElementId id in copiedIds)
                            {
                                if (id == null || id == ElementId.InvalidElementId) continue;
                                if (_targetDoc.GetElement(id) is View)
                                {
                                    discoveredViewId = id;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception) { }
                t.Commit();
            }

            if (viewersToCopy.Count > 0)
            {
                var nonViewSpecificViewers = new List<ElementId>();
                var viewSpecificViewers = new List<ElementId>();
                
                foreach(var id in viewersToCopy)
                {
                    var viewer = _sourceDoc.GetElement(id);
                    if (viewer != null && viewer.OwnerViewId == ElementId.InvalidElementId)
                        nonViewSpecificViewers.Add(id);
                    else
                        viewSpecificViewers.Add(id);
                }

                if (nonViewSpecificViewers.Count > 0)
                {
                    using (Transaction t2 = new Transaction(_targetDoc, "Transfer Callouts and Sections"))
                    {
                        t2.Start();
                        try
                        {
                            var copiedViewerIds = ElementTransformUtils.CopyElements(_sourceDoc, nonViewSpecificViewers, _targetDoc, null, options);
                            if (copiedViewerIds != null && copiedViewerIds.Count > 0 && (discoveredViewId == null || discoveredViewId == ElementId.InvalidElementId))
                            {
                                foreach (ElementId id in copiedViewerIds)
                                {
                                    if (id == null || id == ElementId.InvalidElementId) continue;
                                    if (_targetDoc.GetElement(id) is View)
                                    {
                                        discoveredViewId = id;
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception) { }
                        t2.Commit();
                    }
                }
                
                if (viewSpecificViewers.Count > 0)
                {
                    using (Transaction tCallouts = new Transaction(_targetDoc, "Recreate Detail Callouts"))
                    {
                        tCallouts.Start();
                        
                        foreach (var viewerId in viewSpecificViewers)
                        {
                            try
                            {
                                // In Revit, the Viewer element name often matches the View it represents
                                var viewerElement = _sourceDoc.GetElement(viewerId);
                                if (viewerElement == null) continue;
                                
                                // Find the actual View element associated with this viewer
                                // The most reliable way is to find a view with the same name, or check parameter View Name
                                string viewName = viewerElement.Name;
                                View associatedSourceView = new FilteredElementCollector(_sourceDoc)
                                    .OfClass(typeof(View))
                                    .Cast<View>()
                                    .FirstOrDefault(v => v.Name == viewName);
                                    
                                if (associatedSourceView == null) continue;
                                
                                // Get crop box to define callout corners
                                BoundingBoxXYZ cropBox = associatedSourceView.CropBox;
                                if (cropBox == null) continue;
                                
                                // Transform crop box to model space if needed (CropBox is usually in view space)
                                XYZ ptMin = cropBox.Transform.OfPoint(cropBox.Min);
                                XYZ ptMax = cropBox.Transform.OfPoint(cropBox.Max);
                                
                                XYZ p1 = new XYZ(ptMin.X, ptMin.Y, 0);
                                XYZ p2 = new XYZ(ptMax.X, ptMax.Y, 0);

                                // Find matching ViewFamilyType in target doc
                                ElementId sourceTypeId = associatedSourceView.GetTypeId();
                                ElementType sourceType = _sourceDoc.GetElement(sourceTypeId) as ElementType;
                                
                                ElementId targetTypeId = ElementId.InvalidElementId;
                                if (sourceType != null)
                                {
                                    var matchingTargetType = new FilteredElementCollector(_targetDoc)
                                        .OfClass(typeof(ViewFamilyType))
                                        .Cast<ViewFamilyType>()
                                        .FirstOrDefault(vft => vft.Name == sourceType.Name);
                                        
                                    if (matchingTargetType != null)
                                        targetTypeId = matchingTargetType.Id;
                                }
                                
                                // Fallback if type not found
                                if (targetTypeId == ElementId.InvalidElementId)
                                {
                                    ViewFamily fallbackFamily = ViewFamily.Detail;
                                    if (associatedSourceView.ViewType == ViewType.FloorPlan)
                                        fallbackFamily = ViewFamily.FloorPlan;
                                    else if (associatedSourceView.ViewType == ViewType.CeilingPlan)
                                        fallbackFamily = ViewFamily.CeilingPlan;

                                    var defaultDetailType = new FilteredElementCollector(_targetDoc)
                                        .OfClass(typeof(ViewFamilyType))
                                        .Cast<ViewFamilyType>()
                                        .FirstOrDefault(vft => vft.ViewFamily == fallbackFamily);
                                        
                                    if (defaultDetailType != null)
                                        targetTypeId = defaultDetailType.Id;
                                }

                                if (targetTypeId != ElementId.InvalidElementId)
                                {
                                    // Manually create the callout in the target view
                                    var calloutView = ViewSection.CreateCallout(_targetDoc, targetViewId, targetTypeId, p1, p2);
                                    
                                    // Try to name it
                                    string safeName = _conflictResolver.GetUniqueViewName(viewName);
                                    try { calloutView.Name = safeName; } catch { }
                                }
                            }
                            catch (Exception) { /* Skip if reconstruction fails for this specific callout */ }
                        }
                        
                        tCallouts.Commit();
                    }
                }
            }

            if (discoveredViewId != null && discoveredViewId != ElementId.InvalidElementId)
                return discoveredViewId;
                
            return targetViewId;
        }

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
    }

    public class CustomCopyHandler : IDuplicateTypeNamesHandler
    {
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            return DuplicateTypeAction.UseDestinationTypes; // Usually standard for Revit transfers
        }
    }
}
