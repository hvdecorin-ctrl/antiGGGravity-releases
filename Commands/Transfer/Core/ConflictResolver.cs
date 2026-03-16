using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class ConflictResolver
    {
        private readonly Document _targetDoc;
        private readonly string _prefix;

        public ConflictResolver(Document targetDoc, string prefix)
        {
            _targetDoc = targetDoc;
            _prefix = prefix;
        }

        public string GetUniqueViewName(string originalName)
        {
            string baseName = string.IsNullOrEmpty(_prefix) ? originalName : $"{_prefix}{originalName}";
            string newName = baseName;
            int counter = 1;

            var existingNames = new FilteredElementCollector(_targetDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.Name)
                .ToHashSet();

            while (existingNames.Contains(newName))
            {
                newName = $"{baseName} ({counter})";
                counter++;
            }

            return newName;
        }

        public string GetUniqueSheetNumber(string originalNumber)
        {
            string newNumber = originalNumber;
            int counter = 1;

            var existingNumbers = new FilteredElementCollector(_targetDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .ToHashSet();

            // Simple conflict resolution for sheets
            while (existingNumbers.Contains(newNumber))
            {
                newNumber = $"{originalNumber}-{counter}";
                counter++;
            }

            return newNumber;
        }
    }
}
