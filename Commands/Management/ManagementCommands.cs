using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Management;

namespace antiGGGravity.Commands.Management
{
    // ===================================================================================
    // RENAME VIEWS
    // ===================================================================================

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
                        
                        // Check for existing prefix using Regex (matches [numbers/dots]_ followed by anything)
                        var match = Regex.Match(originalName, @"^[\d\.]+_(.*)$");
                        string content = match.Success ? match.Groups[1].Value : originalName;

                        // Rule: Detail Number + underscore + content
                        string newNameBase = $"{detailNumber}_{content}";
                        
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

    [Transaction(TransactionMode.Manual)]
    public class RenameViewsActiveSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (!(doc.ActiveView is ViewSheet currentSheet))
            {
                TaskDialog.Show("Sheet Required", "Please open a Sheet View to run this command.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Rename Views (Active Sheet)"))
            {
                t.Start();
                try
                {
                    HashSet<string> existingViewNames = new HashSet<string>(
                        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name)
                    );

                    ViewRenamingLogic.RenameViewsOnSheet(currentSheet, existingViewNames);
                    ViewRenamingLogic.SetViewportTitles(currentSheet);

                    t.Commit();
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    t.RollBack();
                    return Result.Failed;
                }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class RenameViewsAllSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (Transaction t = new Transaction(doc, "Rename Views (All Sheets)"))
            {
                t.Start();
                try
                {
                    HashSet<string> existingViewNames = new HashSet<string>(
                        new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name)
                    );

                    var allSheets = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>();

                    foreach (ViewSheet sheet in allSheets)
                    {
                        ViewRenamingLogic.RenameViewsOnSheet(sheet, existingViewNames);
                        ViewRenamingLogic.SetViewportTitles(sheet);
                    }

                    t.Commit();
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    t.RollBack();
                    return Result.Failed;
                }
            }
        }
    }


    // ===================================================================================
    // DUPLICATE SHEETS
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class DuplicateSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                DuplicateSheetsView view = new DuplicateSheetsView(commandData);
                view.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    // ===================================================================================
    // ALIGN SCHEMATIC
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class AlignSchematicCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                AlignSchematicView view = new AlignSchematicView(commandData);
                view.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    // ===================================================================================
    // ADD SELECTED VIEW
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class AddSelectedViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (activeView.ViewType != ViewType.DrawingSheet)
            {
                TaskDialog.Show("Add Selected View", "Active view must be a Sheet.");
                return Result.Cancelled;
            }

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Add Selected View", "Please select views from the Project Browser.");
                return Result.Cancelled;
            }

            List<View> viewsToAdd = new List<View>();
            foreach (ElementId id in selectedIds)
            {
                View v = doc.GetElement(id) as View;
                if (v != null && !v.IsTemplate && v.ViewType != ViewType.DrawingSheet && Viewport.CanAddViewToSheet(doc, activeView.Id, id))
                {
                    viewsToAdd.Add(v);
                }
            }

            if (viewsToAdd.Count == 0)
            {
                TaskDialog.Show("Add Selected View", "No valid views selected or views already on sheet.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Add Views to Sheet"))
            {
                t.Start();
                // Simple placement at 0,0 for now, manual adjustment required by user
                // Could implement stacking logic similar to python script if needed
                foreach (View v in viewsToAdd)
                {
                     try 
                     {
                         Viewport.Create(doc, activeView.Id, v.Id, XYZ.Zero);
                     }
                     catch { }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }


    // ===================================================================================
    // ADD VIEWS UI
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class AddViewsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Placeholder for UI implementation
            TaskDialog.Show("Add Views", "UI feature coming soon."); 
            return Result.Succeeded;
        }
    }


    // ===================================================================================
    // RENUMBER VIEWPORTS
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class RenumberViewportsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (activeView.ViewType != ViewType.DrawingSheet)
            {
                TaskDialog.Show("Renumber", "Must be on a Sheet.");
                return Result.Cancelled;
            }

            // Simple input dialog replacement (using basic logic or task dialog for now)
            // Real implementation requires a custom input form.
            // Implementing a minimal interaction loop.
            
            int counter = 1;
            try
            {
                while (true)
                {
                    Reference r = null;
                    try 
                    { 
                        r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, new ViewportSelectionFilter(), $"Select Viewport to be {counter} (Esc to stop)"); 
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) 
                    { 
                        break; 
                    }

                    Viewport vp = doc.GetElement(r.ElementId) as Viewport;
                    Parameter detailNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    
                    using (Transaction t = new Transaction(doc, "Renumber"))
                    {
                        t.Start();
                        // Swap logic to handle duplicates
                        string newVal = counter.ToString();
                        
                        try
                        {
                            detailNum.Set(newVal); 
                        }
                        catch
                        {
                            // If collision, try swapping - simplified
                            // Real implementation would find the colliding viewport and swap
                        }
                        t.Commit();
                    }
                    counter++;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        public class ViewportSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Viewport;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
