using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

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
                           (f.FamilyCategory != null && f.FamilyCategory.Id.GetIdValue() == (long)BuiltInCategory.OST_DetailComponents),
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

        /// <summary>
        /// Collects system family types (Wall, Floor, Roof, Ceiling, Structural, Rebar Shape, etc.)
        /// from the source document. These are ElementType objects not belonging to a loadable Family.
        /// </summary>
        public List<SystemFamilyTypeItem> GetSystemFamilyTypes(Document sourceDoc, Document targetDoc = null)
        {
            // Categories to collect system types from
            var systemCategories = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructConnections,
                BuiltInCategory.OST_Rebar,
                BuiltInCategory.OST_RebarShape,
                BuiltInCategory.OST_StructuralFoundation,
            };

            // Pre-collect existing types in target for comparison
            var existingTargetTypes = new HashSet<string>();
            if (targetDoc != null)
            {
                foreach (var cat in systemCategories)
                {
                    try
                    {
                        var targetTypes = new FilteredElementCollector(targetDoc)
                            .OfCategory(cat)
                            .WhereElementIsElementType()
                            .ToElements();

                        foreach (var et in targetTypes)
                        {
                            string key = $"{et.Category?.Name}_{et.Name}";
                            existingTargetTypes.Add(key);
                        }
                    }
                    catch { }
                }
            }

            var items = new List<SystemFamilyTypeItem>();

            foreach (var cat in systemCategories)
            {
                try
                {
                    var sourceTypes = new FilteredElementCollector(sourceDoc)
                        .OfCategory(cat)
                        .WhereElementIsElementType()
                        .ToElements();

                    foreach (var et in sourceTypes)
                    {
                        if (et == null) continue;

                        string catName = et.Category?.Name ?? "Unknown";
                        string familyName = "";
                        string typeName = et.Name ?? "";

                        // Try to extract family name from the ElementType
                        try
                        {
                            var familyNameParam = et.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
                            if (familyNameParam != null)
                                familyName = familyNameParam.AsString() ?? "";
                            if (string.IsNullOrEmpty(familyName) && et is ElementType elemType)
                                familyName = elemType.FamilyName ?? "";
                        }
                        catch
                        {
                            if (et is ElementType elemType)
                                familyName = elemType.FamilyName ?? "";
                        }

                        string key = $"{catName}_{typeName}";
                        bool exists = existingTargetTypes.Contains(key);

                        items.Add(new SystemFamilyTypeItem
                        {
                            SourceTypeId = et.Id,
                            CategoryName = catName,
                            FamilyName = familyName,
                            TypeName = typeName,
                            IsAlreadyInTarget = exists,
                            IsSelected = false
                        });
                    }
                }
                catch { }
            }

            return items.OrderBy(i => i.CategoryName).ThenBy(i => i.FamilyName).ThenBy(i => i.TypeName).ToList();
        }
    }
}

