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

        public List<FamilyTransferItem> GetFamilies(Document doc)
        {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            var items = new List<FamilyTransferItem>();
            foreach (var f in families)
            {
                var familyItem = new FamilyTransferItem
                {
                    SourceFamilyId = f.Id,
                    FamilyName = f.Name,
                    CategoryName = f.FamilyCategory?.Name ?? "General",
                    IsSelected = false
                };

                var symbolIds = f.GetFamilySymbolIds();
                foreach (var symbolId in symbolIds)
                {
                    var symbol = doc.GetElement(symbolId) as FamilySymbol;
                    if (symbol == null) continue;

                    familyItem.Types.Add(new FamilyTypeItem
                    {
                        SourceSymbolId = symbolId,
                        TypeName = symbol.Name,
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
