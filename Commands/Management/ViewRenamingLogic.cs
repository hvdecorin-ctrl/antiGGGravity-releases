using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Management
{
    public static class ViewRenamingLogic
    {
        public static readonly Dictionary<ViewType, string> ViewTypeTitleMap = new Dictionary<ViewType, string>
        {
            { ViewType.Section, "SECTION" },
            { ViewType.Detail, "DETAIL" },
            { ViewType.DraftingView, "DETAIL" }
        };

        public static readonly Regex DecimalNumberPattern = new Regex(@"^\d+\.\d+[a-zA-Z]?$");

        public static void RenameViewsOnSheet(ViewSheet sheet, HashSet<string> existingViewNames)
        {
            Document doc = sheet.Document;
            var placedViewIds = sheet.GetAllPlacedViews();

            // Regex for default names: "Section 1", "Detail 1", "Drafting 1", "Callout of Section 1"
            // Captures: Name + space + number
            var defaultNamePattern = new Regex(@"^(Section|Detail|Drafting|Callout of .*?) \d+$", RegexOptions.IgnoreCase);

            foreach (ElementId id in placedViewIds)
            {
                if (doc.GetElement(id) is View view)
                {
                    if (ShouldSkipView(view)) continue;

                    Parameter detailNumParam = view.LookupParameter("Detail Number");
                    if (detailNumParam != null && detailNumParam.HasValue)
                    {
                        string detailNumber = detailNumParam.AsString();
                        if (string.IsNullOrEmpty(detailNumber)) continue;

                        string originalName = view.Name;
                        string content = originalName;

                        // 1. Check if matches "Standard" renaming pattern (e.g. "1_Something", "2.1B_Section 1")
                        // Use [\w\.-] to allow Detail Numbers like "1", "1A", "2.1B", "A-101"
                        var matchPrefix = Regex.Match(originalName, @"^[\w\.-]+_(.*)$");
                        if (matchPrefix.Success)
                        {
                            content = matchPrefix.Groups[1].Value;
                        }
                        
                        // 2. Check if content is just a Default Revit Name (e.g. "Section 1")
                        // matches default pattern OR is just digits/dots/dash/letter (e.g. "1", "2.1", "2.1a")
                        if (defaultNamePattern.IsMatch(content) || Regex.IsMatch(content, @"^[\d\.-]+[a-zA-Z]?$"))
                        {
                            content = "";
                        }

                        // Rule: Detail Number + underscore + content (if content exists)
                        // If content is empty, just Detail Number
                        string newNameBase = string.IsNullOrEmpty(content) 
                            ? detailNumber 
                            : $"{detailNumber}_{content}";
                        
                        // If already correctly named, skip
                        if (originalName == newNameBase) continue;

                        // Handle duplicates at project level
                        int counter = 1;
                        string finalName = newNameBase;
                        while (existingViewNames.Contains(finalName) && finalName != originalName)
                        {
                            finalName = $"{newNameBase}-{counter}";
                            counter++;
                            if (counter > 100) break;
                        }

                        if (finalName != originalName)
                        {
                            try
                            {
                                view.Name = finalName;
                                existingViewNames.Remove(originalName);
                                existingViewNames.Add(finalName);
                            }
                            catch { /* Ignore naming errors */ }
                        }
                    }
                }
            }
        }

        public static void SetViewportTitles(ViewSheet sheet)
        {
            Document doc = sheet.Document;
            var viewportIds = sheet.GetAllViewports();

            foreach (ElementId vpId in viewportIds)
            {
                Viewport viewport = doc.GetElement(vpId) as Viewport;
                if (viewport == null) continue;

                View view = doc.GetElement(viewport.ViewId) as View;
                if (view == null || !ViewTypeTitleMap.ContainsKey(view.ViewType)) continue;

                string desiredTitle = ViewTypeTitleMap[view.ViewType];
                Parameter titleParam = view.LookupParameter("Title on Sheet");

                if (titleParam != null && !titleParam.IsReadOnly)
                {
                    string currentTitle = titleParam.AsString() ?? "";
                    bool isShort = currentTitle.Length < 4;
                    bool isDecimal = DecimalNumberPattern.IsMatch(currentTitle);

                    if ((isShort || isDecimal) && currentTitle != desiredTitle)
                    {
                        titleParam.Set(desiredTitle);
                    }
                }
            }
        }

        private static bool ShouldSkipView(View view)
        {
            if (view.IsTemplate) return true;
            ViewType vt = view.ViewType;
            if (vt == ViewType.DrawingSheet || vt == ViewType.Legend || 
                vt == ViewType.Schedule || vt == ViewType.ThreeD) return true;
            
            if (vt.ToString().Contains("Plan")) return true;

            if (vt == ViewType.DraftingView)
            {
                Parameter typeParam = view.LookupParameter("View - Type");
                if (typeParam != null && typeParam.HasValue)
                {
                    string typeVal = typeParam.AsString();
                    if (typeVal == "Standards" || typeVal == "General Notes" || typeVal == "General Arrangement G.A.") return true;
                }
            }
            return false;
        }
    }
}
