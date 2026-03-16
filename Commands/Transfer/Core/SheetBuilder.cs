using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
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
                t.Start();
                ViewSheet newSheet = ViewSheet.Create(_targetDoc, targetTitleblockId);

                // Set Name and Number
                string safeNumber = _conflictResolver.GetUniqueSheetNumber(sourceSheet.SheetNumber);
                newSheet.SheetNumber = safeNumber;
                
                try { newSheet.Name = sourceSheet.Name; }
                catch (Exception) { /* Sometimes Names are reserved but less common for sheets */ }

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

                        if (Viewport.CanAddViewToSheet(_targetDoc, newSheet.Id, newViewId))
                        {
                            Viewport newVp = Viewport.Create(_targetDoc, newSheet.Id, newViewId, vp.GetBoxCenter());
                            
                            // Attempt to map type (titleline behavior etc)
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
                                            .FirstOrDefault(e => e.Category != null && e.Category.Id.Value == (long)BuiltInCategory.OST_Viewports && e.Name == vpTypeName);
                                        
                                        if (targetVpType != null)
                                        {
                                            newVp.ChangeTypeId(targetVpType.Id);
                                        }
                                    }
                                }
                            }
                            catch (Exception) { /* Ignored if type fails to swap */ }
                        }
                    }
                }

                t.Commit();
                return newSheet;
            }
        }
    }
}
