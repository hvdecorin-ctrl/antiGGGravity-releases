using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Transfer.Core
{
    /// <summary>
    /// Suppresses non-critical Revit warnings during transfer (e.g., "Detail Number is empty").
    /// </summary>
    public class TransferFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (FailureMessageAccessor failure in failures)
            {
                // Delete all warnings — they should not block the transfer
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
                else
                {
                    // For errors, try to resolve or delete them too
                    try
                    {
                        failuresAccessor.DeleteWarning(failure);
                    }
                    catch
                    {
                        try
                        {
                            failuresAccessor.ResolveFailure(failure);
                        }
                        catch { }
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }

    public class SheetBuilder
    {
        private readonly Document _sourceDoc;
        private readonly Document _targetDoc;
        private readonly ConflictResolver _conflictResolver;

        public SheetBuilder(Document sourceDoc, Document targetDoc, ConflictResolver conflictResolver)
        {
            _sourceDoc = sourceDoc;
            _targetDoc = targetDoc;
            _conflictResolver = conflictResolver;
        }

        public ViewSheet RebuildSheet(ViewSheet sourceSheet, ElementId targetTitleblockId, Dictionary<ElementId, ElementId> viewMap)
        {
            using (Transaction t = new Transaction(_targetDoc, "Rebuild Sheet"))
            {
                // Attach failure handler to suppress "Detail Number is empty" warnings
                var failureOptions = t.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new TransferFailuresPreprocessor());
                t.SetFailureHandlingOptions(failureOptions);

                t.Start();

                ViewSheet newSheet = ViewSheet.Create(_targetDoc, targetTitleblockId);

                // Set Name and Number
                string safeNumber = _conflictResolver.GetUniqueSheetNumber(sourceSheet.SheetNumber);
                newSheet.SheetNumber = safeNumber;
                
                try { newSheet.Name = sourceSheet.Name; }
                catch (Exception) { }

                // Place corresponding viewports
                var vpIds = sourceSheet.GetAllViewports();
                foreach (ElementId vpId in vpIds)
                {
                    if (vpId == null || vpId == ElementId.InvalidElementId) continue;
                    
                    Viewport vp = _sourceDoc.GetElement(vpId) as Viewport;
                    if (vp == null) continue;

                    if (viewMap.TryGetValue(vp.ViewId, out ElementId newViewId))
                    {
                        if (newViewId == null || newViewId == ElementId.InvalidElementId) continue;

                        try
                        {
                            if (Viewport.CanAddViewToSheet(_targetDoc, newSheet.Id, newViewId))
                            {
                                Viewport newVp = Viewport.Create(_targetDoc, newSheet.Id, newViewId, vp.GetBoxCenter());
                                
                                // Copy Detail Number (with null check to avoid "Detail Number is empty")
                                try
                                {
                                    var sourceDetailNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                                    if (sourceDetailNum != null)
                                    {
                                        string detailNumStr = sourceDetailNum.AsString();
                                        if (!string.IsNullOrEmpty(detailNumStr))
                                        {
                                            var targetDetailNum = newVp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                                            if (targetDetailNum != null && !targetDetailNum.IsReadOnly)
                                            {
                                                targetDetailNum.Set(detailNumStr);
                                            }
                                        }
                                    }
                                }
                                catch { }
                                
                                // Attempt to map viewport type
                                try
                                {
                                    ElementId typeId = vp.GetTypeId();
                                    if (typeId != null && typeId != ElementId.InvalidElementId)
                                    {
                                        string vpTypeName = _sourceDoc.GetElement(typeId)?.Name;
                                        if (!string.IsNullOrEmpty(vpTypeName))
                                        {
                                            var targetVpType = new FilteredElementCollector(_targetDoc)
                                                .OfClass(typeof(ElementType))
                                                .Cast<ElementType>()
                                                .FirstOrDefault(e => e.Category != null && e.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Viewports && e.Name == vpTypeName);
                                            
                                            if (targetVpType != null)
                                            {
                                                newVp.ChangeTypeId(targetVpType.Id);
                                            }
                                        }
                                    }
                                }
                                catch (Exception) { }
                            }
                        }
                        catch (Exception) { /* Skip individual viewport failures */ }
                    }
                }

                t.Commit();
                return newSheet;
            }
        }
    }
}
