using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.UI
{
    /// <summary>
    /// External event handler that loads a base family type from .rfa and
    /// duplicates it with a new name, setting Type Mark, Type Comments, and Description.
    /// </summary>
    public class DuplicatorRequestHandler : IExternalEventHandler
    {
        public List<DuplicatorRow> RowsToProcess { get; set; } = new List<DuplicatorRow>();
        public Action<string> StatusCallback { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            int success = 0;
            int failed = 0;
            int skipped = 0;

            using (Transaction t = new Transaction(doc, "Smart Duplicate Families"))
            {
                t.Start();

                foreach (var row in RowsToProcess)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(row.PreviewName) || string.IsNullOrEmpty(row.BaseFamilyPath))
                        {
                            failed++;
                            continue;
                        }

                        // Step 1: Ensure the base family type is loaded in the project
                        ElementType baseSymbol = null;
                        if (row.BaseFamilyPath == "Current Project")
                        {
                            baseSymbol = new FilteredElementCollector(doc)
                                .WhereElementIsElementType()
                                .Cast<ElementType>()
                                .FirstOrDefault(fs => fs.FamilyName == row.BaseFamily && fs.Name == row.BaseTypeName);
                        }
                        else if (!string.IsNullOrEmpty(row.BaseTypeName))
                        {
                            FamilySymbol loadedSymbol;
                            doc.LoadFamilySymbol(row.BaseFamilyPath, row.BaseTypeName, new FamilyLoadOptionsOverwrite(), out loadedSymbol);
                            baseSymbol = loadedSymbol;
                        }
                        else
                        {
                            // Load entire family if no specific type matched
                            Family loadedFamily;
                            doc.LoadFamily(row.BaseFamilyPath, new FamilyLoadOptionsOverwrite(), out loadedFamily);
                            if (loadedFamily != null)
                            {
                                var firstSymbolId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                                if (firstSymbolId != null && firstSymbolId != ElementId.InvalidElementId)
                                    baseSymbol = doc.GetElement(firstSymbolId) as FamilySymbol;
                            }
                        }

                        if (baseSymbol == null)
                        {
                            failed++;
                            continue;
                        }

                        // Ensure the symbol is active in the document before duplicating (only for FamilySymbols)
                        if (baseSymbol is FamilySymbol fs && !fs.IsActive)
                        {
                            fs.Activate();
                            doc.Regenerate(); // Sometimes required after activation
                        }

                        // Step 2: Check if type with this name already exists
                        string newName = row.PreviewName;
                        var existing = new FilteredElementCollector(doc)
                            .WhereElementIsElementType()
                            .Cast<ElementType>()
                            .FirstOrDefault(f => f.Name == newName && f.FamilyName == baseSymbol.FamilyName);

                        if (existing != null)
                        {
                            skipped++;
                            continue;
                        }

                        // Step 3: Duplicate
                        ElementType newType = baseSymbol.Duplicate(newName);
                        if (newType == null) { failed++; continue; }

                        // Step 4: Set parameters
                        var tmParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        if (tmParam != null && !tmParam.IsReadOnly) tmParam.Set(row.TypeMark ?? "");

                        var tcParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                        if (tcParam != null && !tcParam.IsReadOnly) tcParam.Set(row.TypeComment ?? "");

                        string finalDescription = row.Description ?? "";

                        var descParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                        if (descParam != null && !descParam.IsReadOnly)
                            descParam.Set(finalDescription);
                        else
                        {
                            descParam = newType.LookupParameter("Description");
                            if (descParam != null && !descParam.IsReadOnly)
                                descParam.Set(finalDescription);
                        }

                        success++;
                    }
                    catch { failed++; }
                }

                t.Commit();
            }

            string msg = $"✅ Created {success} types.";
            if (skipped > 0) msg += $" Skipped {skipped} (already exist).";
            if (failed > 0) msg += $" Failed: {failed}.";
            StatusCallback?.Invoke(msg);
        }

        public string GetName() => "Smart Duplicator Handler";
    }
}
