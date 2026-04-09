using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace antiGGGravity.Utilities
{
    public static class RevisionLogic
    {
        /// <summary>
        /// Retrieves all revisions in the document.
        /// </summary>
        public static List<Revision> GetRevisions(Document doc, bool includeIssued = true)
        {
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Revision))
                .Cast<Revision>();

            if (!includeIssued)
            {
                collector = collector.Where(r => !r.Issued);
            }

            return collector.OrderBy(r => r.SequenceNumber).ToList();
        }

        /// <summary>
        /// Updates the additional revisions on the specified sheets.
        /// Matches pyRevit's update_sheet_revisions logic.
        /// </summary>
        public static List<ViewSheet> SetSheetRevisions(IEnumerable<ViewSheet> sheets, IEnumerable<Revision> revisions)
        {
            List<ViewSheet> updated = new List<ViewSheet>();
            var revIds = revisions.Select(r => r.Id).ToList();

            foreach (var sheet in sheets)
            {
                var currentRevs = sheet.GetAdditionalRevisionIds();
                bool added = false;

                foreach (var revId in revIds)
                {
                    if (!currentRevs.Contains(revId))
                    {
                        currentRevs.Add(revId);
                        added = true;
                    }
                }

                if (added)
                {
                    sheet.SetAdditionalRevisionIds(currentRevs);
                    updated.Add(sheet);
                }
            }

            return updated;
        }

        /// <summary>
        /// Finds sheets that match the given revisions.
        /// </summary>
        public static List<ViewSheet> GetRevisedSheets(Document doc, IEnumerable<Revision> revisions, bool matchAny)
        {
            var allSheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>();

            var revIds = new HashSet<ElementId>(revisions.Select(r => r.Id));
            List<ViewSheet> matched = new List<ViewSheet>();

            foreach (var sheet in allSheets)
            {
                // This combines "Additional Revisions" and those appearing from clouds/schedules
                var sheetRevs = sheet.GetAllRevisionIds();
                
                if (matchAny)
                {
                    if (revIds.Any(id => sheetRevs.Contains(id)))
                        matched.Add(sheet);
                }
                else
                {
                    if (revIds.All(id => sheetRevs.Contains(id)))
                        matched.Add(sheet);
                }
            }

            return matched;
        }

        /// <summary>
        /// Creates a ViewSheetSet from the specified sheets.
        /// </summary>
        public static void CreateRevisionSheetSet(Document doc, string name, IEnumerable<ViewSheet> sheets)
        {
            ViewSet viewSet = new ViewSet();
            foreach (var sheet in sheets)
            {
                viewSet.Insert(sheet);
            }

            ViewSheetSetting setting = doc.PrintManager.ViewSheetSetting;
            setting.InSession.Views = viewSet;
            setting.SaveAs(name);
        }
    }
}
