using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class FamilyManagerEngine
    {
        private Application _app;

        public FamilyManagerEngine(Application app)
        {
            _app = app;
        }

        public List<FamilyManagerItem> ScanFolder(string folderPath, Document targetDoc)
        {
            var results = new List<FamilyManagerItem>();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return results;

            // 1. Get all loaded families in the project
            var loadedFamilies = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            // 2. Scan all .rfa files in the folder (and subfolders)
            string[] rfaFiles = Directory.GetFiles(folderPath, "*.rfa", SearchOption.AllDirectories);

            foreach (var file in rfaFiles)
            {
                string familyName = Path.GetFileNameWithoutExtension(file);
                bool isLoaded = loadedFamilies.ContainsKey(familyName);

                string categoryName = "Generic Models";

                // Try to extract category
                if (loadedFamilies.TryGetValue(familyName, out Family family))
                {
                    categoryName = family.FamilyCategory?.Name ?? "Generic Models";
                }
                else
                {
                    // Fallback: Guess from parent folder name
                    categoryName = GuessCategoryFromFolder(file);
                }

                results.Add(new FamilyManagerItem
                {
                    FilePath = file,
                    FamilyName = familyName,
                    CategoryName = categoryName,
                    Status = isLoaded ? "Loaded" : "Missing",
                    IsSelected = false
                });
            }

            return results.OrderBy(r => r.CategoryName).ThenBy(r => r.FamilyName).ToList();
        }

        private string GuessCategoryFromFolder(string rfaPath)
        {
            try
            {
                string parentFolder = new DirectoryInfo(Path.GetDirectoryName(rfaPath)).Name;

                // Strip numeric prefixes like "34-11.2 " → "Framing"
                string folderClean = parentFolder;
                if (folderClean.Contains(" "))
                {
                    int spaceIndex = folderClean.IndexOf(' ');
                    string firstPart = folderClean.Substring(0, spaceIndex);
                    if (firstPart.Any(char.IsDigit) && firstPart.Any(c => c == '-' || c == '.'))
                        folderClean = folderClean.Substring(spaceIndex + 1);
                }

                if (folderClean.IndexOf("Framing", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Framing";
                if (folderClean.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Columns";
                if (folderClean.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Connections";
                if (folderClean.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Foundations";
                if (folderClean.IndexOf("Rebar", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Rebar";
                if (folderClean.IndexOf("Steel", StringComparison.OrdinalIgnoreCase) >= 0) return "Structural Framing";
                if (folderClean.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0) return "Detail Items";
                if (folderClean.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0) return "Profiles";
                if (folderClean.IndexOf("Annotation", StringComparison.OrdinalIgnoreCase) >= 0) return "Generic Annotations";
                
                return folderClean; // Use raw folder name as last resort
            }
            catch { return "Generic Models"; }
        }


        public void ProcessFamilies(Document targetDoc, List<FamilyManagerItem> selectedItems, out int loadedCount, out int updatedCount, out List<string> errors)
        {
            loadedCount = 0;
            updatedCount = 0;
            errors = new List<string>();

            // Get authoritative list of loaded families to check category conflicts
            var loadedFamilies = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            IFamilyLoadOptions loadOptions = new FamilyLoadOptionsOverwrite();

            foreach (var item in selectedItems)
            {
                try
                {
                    // Revit Restriction: Cannot load a family if another family with same name 
                    // but different category already exists in the document.
                    if (loadedFamilies.TryGetValue(item.FamilyName, out Family existingFamily))
                    {
                        string projectCat = existingFamily.FamilyCategory?.Name;
                        if (!string.Equals(projectCat, item.CategoryName, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"CONFLICT: Family '{item.FamilyName}' exists in project as '{projectCat}', but the file on disk is category '{item.CategoryName}'. Revit forbids same-name cross-category families. Rename the existing family first.");
                            continue;
                        }
                    }

                    bool wasLoadedAlready = item.Status == "Loaded";
                    var selectedTypes = item.Types.Where(t => t.IsSelected).ToList();
                    bool loadAll = item.Types.Count == 0 || selectedTypes.Count == item.Types.Count;

                    if (loadAll)
                    {
                        Family loadedFamily;
                        if (targetDoc.LoadFamily(item.FilePath, loadOptions, out loadedFamily))
                        {
                            if (wasLoadedAlready) updatedCount++; else loadedCount++;
                            item.Status = "Loaded";
                            foreach (var t in item.Types) { t.IsAlreadyInTarget = true; t.IsSelected = false; }
                        }
                        else
                        {
                            errors.Add($"FAILED: Revit could not load family '{item.FamilyName}'. It may be corrupted or blocked by another internal conflict.");
                        }
                    }
                    else
                    {
                        // Selective Sub-Type loading
                        bool anySuccess = false;
                        foreach (var st in selectedTypes)
                        {
                            FamilySymbol symbol;
                            if (targetDoc.LoadFamilySymbol(item.FilePath, st.TypeName, out symbol))
                            {
                                st.IsAlreadyInTarget = true;
                                st.IsSelected = false;
                                anySuccess = true;
                            }
                            else
                            {
                                errors.Add($"FAILED: Could not load type '{st.TypeName}' from '{item.FamilyName}'. Check if this type exists in the .rfa file.");
                            }
                        }
                        
                        if (anySuccess)
                        {
                            if (wasLoadedAlready) updatedCount++; else loadedCount++;
                            item.Status = "Loaded";
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"ERROR processing {item.FamilyName}: {ex.Message}");
                }
            }
        }
    }
}
