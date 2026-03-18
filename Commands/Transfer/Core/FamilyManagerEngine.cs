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
                bool is2D = false;

                // Try to extract metadata without opening the document
                ExtractCategoryAndDimensionality(file, out categoryName, out is2D);

                results.Add(new FamilyManagerItem
                {
                    FilePath = file,
                    FamilyName = familyName,
                    CategoryName = categoryName,
                    Is2D = is2D,
                    Status = isLoaded ? "Loaded" : "Missing",
                    IsSelected = false
                });
            }

            return results.OrderBy(r => r.CategoryName).ThenBy(r => r.FamilyName).ToList();
        }

        private void ExtractCategoryAndDimensionality(string rfaPath, out string categoryName, out bool is2D)
        {
            categoryName = "Generic Models";
            is2D = false;
            string tempXml = null;

            try
            {
                // Prevent "Upgrading..." UI flash for older families
                BasicFileInfo info = BasicFileInfo.Extract(rfaPath);
                if (!info.Format.Contains(_app.VersionNumber))
                {
                    throw new Exception("Older version - skip PartAtom extraction to avoid UI flash.");
                }

                tempXml = Path.GetTempFileName();
                _app.ExtractPartAtomFromFamilyFile(rfaPath, tempXml);

                XDocument xdoc = XDocument.Load(tempXml);
                XNamespace atom = "http://www.w3.org/2005/Atom";

                var categoryNode = xdoc.Descendants(atom + "category").FirstOrDefault();
                if (categoryNode != null)
                {
                    string term = categoryNode.Attribute("term")?.Value ?? "";
                    categoryName = term.Replace("OST_", "").Replace("Tags", " Tags").Replace("Components", " Components");

                    // Basic Check for 2D vs 3D categories
                    is2D = term.Contains("Annotation") || 
                           term.Contains("Detail") || 
                           term.Contains("Profile") || 
                           term.Contains("Tags") || 
                           term.Contains("TitleBlocks") ||
                           term.Contains("Symb");
                }
            }
            catch
            {
                // Fallback: Guess from parent folder name
                try
                {
                    string parentFolder = new DirectoryInfo(Path.GetDirectoryName(rfaPath)).Name;
                    categoryName = parentFolder;
                    is2D = parentFolder.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           parentFolder.IndexOf("Annotation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           parentFolder.IndexOf("2D", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { }
            }
            finally
            {
                if (tempXml != null && File.Exists(tempXml))
                {
                    try { File.Delete(tempXml); } catch { }
                }
            }
        }

        public void ProcessFamilies(Document targetDoc, List<FamilyManagerItem> selectedItems, out int loadedCount, out int updatedCount, out List<string> errors)
        {
            loadedCount = 0;
            updatedCount = 0;
            errors = new List<string>();

            IFamilyLoadOptions loadOptions = new FamilyLoadOptionsOverwrite();

            foreach (var item in selectedItems)
            {
                try
                {
                    bool wasLoadedAlready = item.Status == "Loaded";
                    
                    // If Types were fetched, and not all are selected, we load selectively
                    var selectedTypes = item.Types.Where(t => t.IsSelected).ToList();
                    bool loadAll = item.Types.Count == 0 || selectedTypes.Count == item.Types.Count;

                    if (loadAll)
                    {
                        Family loadedFamily;
                        bool loaded = targetDoc.LoadFamily(item.FilePath, loadOptions, out loadedFamily);
                        if (wasLoadedAlready) updatedCount++; else loadedCount++;
                    }
                    else
                    {
                        // Selective Sub-Type loading
                        foreach (var st in selectedTypes)
                        {
                            FamilySymbol symbol;
                            bool loaded = targetDoc.LoadFamilySymbol(item.FilePath, st.TypeName, out symbol);
                        }
                        if (wasLoadedAlready) updatedCount++; else loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to process {item.FamilyName}: {ex.Message}");
                }
            }
        }
    }
}
