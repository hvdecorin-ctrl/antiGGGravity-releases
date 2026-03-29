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

            // 1. Get all loaded families in the project (GroupBy handles duplicate names)
            var loadedFamilies = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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

            // Get authoritative list of loaded families to check category conflicts (GroupBy handles duplicate names)
            var loadedFamilies = new FilteredElementCollector(targetDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            IFamilyLoadOptions loadOptions = new FamilyLoadOptionsOverwrite();

            foreach (var item in selectedItems)
            {
                try
                {
                    // Note: Category conflict pre-check removed. item.CategoryName is derived from 
                    // folder names (e.g. "38 Timber") and doesn't match Revit categories (e.g. "Structural Framing").
                    // Revit's own LoadFamilySymbol/LoadFamily will reject genuine cross-category conflicts natively.
                    bool wasLoadedAlready = loadedFamilies.ContainsKey(item.FamilyName);
                    var selectedTypes = item.Types.Where(t => t.IsSelected).ToList();
                    
                    // Priority 1: Load specific symbols if any are selected OR if we have types at all.
                    // This forces Revit to bypass the "Select Types" catalog dialog.
                    if (item.Types.Count > 0)
                    {
                        bool anySuccess = false;
                        
                        // If user checked the parent family but NO specific types, load ALL types we found
                        var typesToLoad = selectedTypes.Count > 0 ? selectedTypes : item.Types.ToList();

                        foreach (var st in typesToLoad)
                        {
                            FamilySymbol symbol;
                            if (targetDoc.LoadFamilySymbol(item.FilePath, st.TypeName, loadOptions, out symbol))
                            {
                                st.IsAlreadyInTarget = true;
                                st.IsSelected = false;
                                anySuccess = true;
                            }
                            else
                            {
                                errors.Add($"FAILED type '{st.TypeName}' from '{item.FamilyName}' @ '{item.FilePath}'");
                            }
                        }

                        if (anySuccess)
                        {
                            if (wasLoadedAlready) updatedCount++; else loadedCount++;
                            item.Status = "Loaded";
                            
                            // If we weren't in "selection" mode (no specific types checked), 
                            // mark everything as loaded since we just loaded all indexed types
                            if (selectedTypes.Count == 0)
                            {
                               foreach (var t in item.Types) t.IsAlreadyInTarget = true;
                            }
                        }
                    }
                    else
                    {
                        // Priority 2: Fallback to whole family load ONLY if index has no types (e.g. no .txt and .rfa not scanned)
                        Family loadedFamily;
                        if (targetDoc.LoadFamily(item.FilePath, loadOptions, out loadedFamily))
                        {
                            if (wasLoadedAlready) updatedCount++; else loadedCount++;
                            item.Status = "Loaded";
                        }
                        else
                        {
                            errors.Add($"FAILED family load '{item.FamilyName}' @ '{item.FilePath}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"EXCEPTION {item.FamilyName}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }
}
