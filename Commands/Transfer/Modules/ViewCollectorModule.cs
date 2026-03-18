using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Modules
{
    public class ViewCollectorModule
    {
        private readonly ViewAnalyzer _analyzer;

        public ViewCollectorModule(Document sourceDoc)
        {
            _analyzer = new ViewAnalyzer(sourceDoc);
        }

        public List<ViewTransferItem> GetTransferableViews()
        {
            var views = _analyzer.GetAllTransferableViewTypes();
            return views.Select(v => new ViewTransferItem
            {
                SourceViewId = v.Id,
                ViewName = v.Name,
                ViewType = v.ViewType,
                IsSelected = false
            }).ToList();
        }

        public List<SheetTransferItem> GetSheets()
        {
            var sheets = _analyzer.GetAllSheets();
            return sheets.Select(s => new SheetTransferItem
            {
                SourceSheetId = s.Id,
                SheetNumber = s.SheetNumber,
                SheetName = s.Name,
                IsSelected = false
            }).ToList();
        }

        public List<FamilyTransferItem> GetFamilies(Document sourceDoc, Document targetDoc = null)
        {
            var families = new FilteredElementCollector(sourceDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            // Pre-collect existing symbols in target to speed up lookup
            var existingSymbols = new HashSet<string>();
            if (targetDoc != null)
            {
                var targetSymbols = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>();
                
                foreach (var ts in targetSymbols)
                {
                    // Unique key: Category + FamilyName + SymbolName
                    string key = $"{ts.Category?.Name}_{ts.FamilyName}_{ts.Name}";
                    existingSymbols.Add(key);
                }
            }

            var items = new List<FamilyTransferItem>();
            foreach (var f in families)
            {
                var familyItem = new FamilyTransferItem
                {
                    SourceFamilyId = f.Id,
                    FamilyName = f.Name,
                    CategoryName = f.FamilyCategory?.Name ?? "General",
                    Is2D = f.FamilyCategory?.CategoryType == CategoryType.Annotation || 
                           (f.FamilyCategory != null && f.FamilyCategory.Id.Value == (long)BuiltInCategory.OST_DetailComponents),
                    IsSelected = false
                };

                var symbolIds = f.GetFamilySymbolIds();
                foreach (var symbolId in symbolIds)
                {
                    var symbol = sourceDoc.GetElement(symbolId) as FamilySymbol;
                    if (symbol == null) continue;

                    string key = $"{symbol.Category?.Name}_{symbol.FamilyName}_{symbol.Name}";
                    bool exists = existingSymbols.Contains(key);

                    familyItem.Types.Add(new FamilyTypeItem
                    {
                        SourceSymbolId = symbolId,
                        TypeName = symbol.Name,
                        IsAlreadyInTarget = exists,
                        IsSelected = false
                    });
                }

                if (familyItem.Types.Count > 0)
                {
                    items.Add(familyItem);
                }
            }
            return items.OrderBy(i => i.CategoryName).ThenBy(i => i.FamilyName).ToList();
        }
    }
}
