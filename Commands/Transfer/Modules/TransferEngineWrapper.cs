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
        private readonly ConflictResolver _conflictResolver;
        private readonly CopyEngine _copyEngine;
        private readonly SheetBuilder _sheetBuilder;

        public TransferEngineWrapper(Document sourceDoc, Document targetDoc, TransferOptions options)
        {
            _sourceDoc = sourceDoc;
            _targetDoc = targetDoc;
            
            _conflictResolver = new ConflictResolver(targetDoc, options.DuplicateHandlingPrefix ? options.PrefixString : "");
            _copyEngine = new CopyEngine(sourceDoc, targetDoc, _conflictResolver);
            _sheetBuilder = new SheetBuilder(sourceDoc, targetDoc, _conflictResolver);
        }

        public void ExecuteTransfer(List<ViewTransferItem> viewsToCopy, List<SheetTransferItem> sheetsToCopy, List<FamilyTransferItem> familiesToCopy)
        {
            if ((viewsToCopy == null || viewsToCopy.Count == 0) && 
                (sheetsToCopy == null || sheetsToCopy.Count == 0) &&
                (familiesToCopy == null || familiesToCopy.Count == 0))
                return;

            // Step 0: Copy Families first (as views might depend on them)
            if (familiesToCopy != null && familiesToCopy.Count > 0)
            {
                var idsToCopy = familiesToCopy
                    .Select(f => f.SourceSymbolId ?? f.SourceFamilyId)
                    .Where(id => id != null && id != ElementId.InvalidElementId)
                    .Distinct()
                    .ToList();
                _copyEngine.CopyFamilies(idsToCopy);
            }

            // Step 1: Gather all unique view IDs that need copying
            HashSet<ElementId> allViewIdsToCopy = new HashSet<ElementId>();
            
            if (viewsToCopy != null)
            {
                foreach (var v in viewsToCopy) allViewIdsToCopy.Add(v.SourceViewId);
            }

            // Step 2 & 2.5: Copy views... (rest of the logic)
            Dictionary<ElementId, ElementId> viewMap = new Dictionary<ElementId, ElementId>();
            foreach (ElementId sourceViewId in allViewIdsToCopy)
            {
                if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId) continue;
                
                try
                {
                    ElementId newViewId = _copyEngine.CopySingleView(sourceViewId);
                    if (newViewId != null && newViewId != ElementId.InvalidElementId)
                    {
                        viewMap[sourceViewId] = newViewId;
                    }
                }
                catch (Exception) { /* Skip failures */ }
            }

            // Step 3: Rebuild Sheets...
            if (sheetsToCopy != null && sheetsToCopy.Count > 0)
            {
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
        }
    }
}
