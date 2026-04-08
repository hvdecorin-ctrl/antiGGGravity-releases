using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace antiGGGravity.Utilities
{
    public static class PrintLogic
    {
        /// <summary>
        /// Orders a list of sheet elements according to their appearance in a ViewSchedule (Sheet Index).
        /// </summary>
        public static List<ViewSheet> OrderSheetsBySchedule(ViewSchedule schedule, IEnumerable<ViewSheet> sheets)
        {
            Document doc = schedule.Document;
            string tempPath = Path.Combine(Path.GetTempPath(), $"antiGG_PrintIndex_{Guid.NewGuid()}.txt");

            ViewScheduleExportOptions opt = new ViewScheduleExportOptions
            {
                TextQualifier = ExportTextQualifier.None,
                FieldDelimiter = "\t"
            };

            try
            {
                schedule.Export(Path.GetDirectoryName(tempPath), Path.GetFileName(tempPath), opt);
                
                if (!File.Exists(tempPath)) return sheets.ToList();

                string[] lines = File.ReadAllLines(tempPath);
                File.Delete(tempPath);

                var sheetsList = sheets.ToList();
                var orderedSheets = new Dictionary<int, ViewSheet>();

                foreach (var sheet in sheetsList)
                {
                    string sheetNumber = sheet.SheetNumber;
                    // Match the sheet number in the schedule data lines
                    for (int i = 0; i < lines.Length; i++)
                    {
                        // Check if sheet number exists as a standalone token in the tab-separated line
                        if (lines[i].Split('\t').Contains(sheetNumber))
                        {
                            orderedSheets[i] = sheet;
                            break;
                        }
                    }
                }

                return orderedSheets.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            }
            catch
            {
                return sheets.ToList();
            }
        }

        /// <summary>
        /// Solves a naming template for a given sheet.
        /// </summary>
        public static string ResolveName(string template, ViewSheet sheet, int index, string dateStr)
        {
            string name = template;
            Document doc = sheet.Document;

            // Basic Placeholders
            name = name.Replace("{number}", sheet.SheetNumber);
            name = name.Replace("{name}", sheet.Name);
            name = name.Replace("{index}", index.ToString("D4"));
            name = name.Replace("{date}", dateStr);

            // Project Info
            ProjectInfo pi = doc.ProjectInformation;
            name = name.Replace("{proj_name}", pi.Name ?? "");
            name = name.Replace("{proj_number}", pi.Number ?? "");

            // Regex for parameters: {param:ParameterName}
            var matches = Regex.Matches(name, @"\{param:(.*?)\}");
            foreach (Match match in matches)
            {
                string paramName = match.Groups[1].Value;
                Parameter p = sheet.LookupParameter(paramName);
                if (p == null || !p.HasValue)
                {
                    // Try titleblock
                    Element tblock = new FilteredElementCollector(doc, sheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .FirstOrDefault();
                    if (tblock != null) p = tblock.LookupParameter(paramName);
                }

                string value = (p != null && p.HasValue) ? p.AsValueString() ?? p.AsString() : "";
                name = name.Replace(match.Value, value ?? "");
            }

            return CleanupFileName(name);
        }

        public static string CleanupFileName(string filename)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c, '_');
            }
            return filename;
        }

        /// <summary>
        /// Exports a sheet to PDF using the Revit 2022+ API.
        /// </summary>
        public static void ExportToPdf(ViewSheet sheet, string folder, string fileName, PDFExportOptions options)
        {
            Document doc = sheet.Document;
            options.FileName = Path.GetFileNameWithoutExtension(fileName);
            
            var viewIds = new List<ElementId> { sheet.Id };
            doc.Export(folder, viewIds, options);
        }
    }
}
