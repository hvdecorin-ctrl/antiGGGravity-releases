using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Management;

namespace antiGGGravity.Commands.Management
{
    // ===================================================================================
    // RENAME VIEWS ACTIVE SHEET
    // ===================================================================================

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

    // ===================================================================================
    // RENAME VIEWS ALL SHEETS
    // ===================================================================================

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

            // 1. Get Starting Number
            var inputWindow = new antiGGGravity.Views.Management.RenumberInputWindow();
            if (inputWindow.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            string currentNumber = inputWindow.InputValue;
            if (string.IsNullOrWhiteSpace(currentNumber)) return Result.Cancelled;

            // 2. Loop for Picking
            try
            {
                while (true)
                {
                    // Prompt to pick
                    Reference r = null;
                    try
                    {
                        r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, 
                            new ViewportSelectionFilter(), 
                            $"Select Viewport to be '{currentNumber}' (Esc to stop)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break; // User pressed Esc
                    }

                    Viewport vp = doc.GetElement(r.ElementId) as Viewport;
                    Parameter detailNumParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);

                    using (Transaction t = new Transaction(doc, "Renumber Viewport"))
                    {
                        t.Start();

                        // 3. Collision Handling
                        // Find if any OTHER viewport on this sheet has the same Detail Number
                        Viewport existingCollision = FindViewportWithDetailNumber(doc, activeView.Id, currentNumber);
                        
                        if (existingCollision != null && existingCollision.Id != vp.Id)
                        {
                            Parameter collisionParam = existingCollision.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                            // Rename valid existing viewport to temp
                            string tempName = $"{currentNumber}_temp_{Guid.NewGuid().ToString().Substring(0, 5)}";
                            collisionParam.Set(tempName);
                        }

                        // 4. Set the new number
                        detailNumParam.Set(currentNumber);
                        t.Commit();
                    }

                    // 5. Increment Number for next loop
                    currentNumber = IncrementNumber(currentNumber);
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private Viewport FindViewportWithDetailNumber(Document doc, ElementId sheetId, string detailNumber)
        {
            var viewports = new FilteredElementCollector(doc, sheetId)
                            .OfClass(typeof(Viewport))
                            .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                Parameter p = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                if (p != null && p.AsString() == detailNumber)
                {
                    return vp;
                }
            }
            return null;
        }

        private string IncrementNumber(string input)
        {
            // Regex to find the last number in the string
            var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)(?!.*\d)");
            if (match.Success)
            {
                string numberStr = match.Groups[1].Value;
                int number = int.Parse(numberStr);
                int nextNumber = number + 1;
                
                // Replace the last number with the incremented one
                // Be careful to only replace the LAST instance
                int index = match.Groups[1].Index;
                int length = match.Groups[1].Length;
                
                return input.Substring(0, index) + nextNumber + input.Substring(index + length);
            }
            
            // Fallback: append "-1" if no number found
            return input + "-1";
        }

        public class ViewportSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Viewport;
            public bool AllowReference(Reference reference, XYZ position) => false;
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
            
            // 1. Find the Sheet to place on
            ViewSheet targetSheet = null;
            if (doc.ActiveView is ViewSheet activeSheet)
            {
                targetSheet = activeSheet;
            }
            else
            {
                // If we are INSIDE an activated view on a sheet, find its viewport
                var vps = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>();
                var parentVp = vps.FirstOrDefault(v => v.ViewId == doc.ActiveView.Id);
                if (parentVp != null)
                {
                    targetSheet = doc.GetElement(parentVp.SheetId) as ViewSheet;
                }
            }

            if (targetSheet == null)
            {
                TaskDialog.Show("antiGGGravity", "Please open a Sheet or activate a View that is placed on a Sheet.");
                return Result.Cancelled;
            }

            // 2. Get Selected Views (Graphic or Browser)
            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("antiGGGravity", "Please select a view symbol (Section/Callout) or pick views from the Project Browser.");
                return Result.Cancelled;
            }

            // Pre-collect views for name matching fallback
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && 
                            v.ViewType != ViewType.DrawingSheet && 
                            v.ViewType != ViewType.Legend)
                .ToList();

            List<View> viewsToAdd = new List<View>();
            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                View v = elem as View;

                // Fallback: If selecting a Section/Callout symbol, the element name often matches the View Name
                if (v == null)
                {
                    string elemName = elem.Name;
                    v = allViews.FirstOrDefault(x => x.Name == elemName);
                }

                if (v != null)
                {
                    if (Viewport.CanAddViewToSheet(doc, targetSheet.Id, v.Id))
                    {
                        viewsToAdd.Add(v);
                    }
                }
            }

            if (viewsToAdd.Count == 0)
            {
                TaskDialog.Show("antiGGGravity", "No valid unsheeted views found in selection.\n\nNote: Views already on a sheet cannot be added again.");
                return Result.Cancelled;
            }

            // 3. Place with stacking logic
            using (Transaction t = new Transaction(doc, "Add Selected Views to Sheet"))
            {
                t.Start();
                PlaceViewsOnSheet(doc, targetSheet, viewsToAdd);
                t.Commit();
            }

            return Result.Succeeded;
        }

        public static void PlaceViewsOnSheet(Document doc, ViewSheet sheet, List<View> views)
        {
            // Find existing viewports to determine start position
            var existingVports = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            XYZ startPos = XYZ.Zero;
            
            // If viewports exist, find the "lowest" one to avoid overlaps
            if (existingVports.Any())
            {
                double minY = existingVports.Min(v => v.GetBoxCenter().Y);
                startPos = new XYZ(0, minY - 0.5, 0); // Start half a foot below the lowest view
            }

            // Grid: 3 columns
            double colSpacing = 1.0; // ~300mm
            double rowSpacing = 0.8; // ~240mm

            for (int i = 0; i < views.Count; i++)
            {
                try
                {
                    double x = startPos.X + (i % 3) * colSpacing;
                    double y = startPos.Y - (i / 3) * rowSpacing;
                    Viewport.Create(doc, sheet.Id, views[i].Id, new XYZ(x, y, 0));
                }
                catch { }
            }
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
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Find the Sheet to place on
            ViewSheet targetSheet = doc.ActiveView as ViewSheet;
            if (targetSheet == null)
            {
                // Try to find if active view is on a sheet
                var viewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>();
                var vp = viewports.FirstOrDefault(v => v.ViewId == doc.ActiveView.Id);
                if (vp != null)
                {
                    targetSheet = doc.GetElement(vp.SheetId) as ViewSheet;
                }
            }

            if (targetSheet == null)
            {
                TaskDialog.Show("antiGGGravity", "Please open a Sheet or a View that is placed on a Sheet.");
                return Result.Cancelled;
            }

            // 2. Collect Unsheeted Views
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && 
                            v.ViewType != ViewType.DrawingSheet && 
                            v.ViewType != ViewType.Legend)
                .ToList();

            List<View> unsheetedViews = allViews
                .Where(v => Viewport.CanAddViewToSheet(doc, targetSheet.Id, v.Id))
                .ToList();

            if (unsheetedViews.Count == 0)
            {
                TaskDialog.Show("antiGGGravity", "No unsheeted views found in project.");
                return Result.Cancelled;
            }

            // 3. Show Premium UI
            AddViewsWindow window = new AddViewsWindow(unsheetedViews);
            if (window.ShowDialog() != true || window.SelectedViews.Count == 0)
            {
                return Result.Cancelled;
            }

            // 4. Place with stacking logic
            using (Transaction t = new Transaction(doc, "Add Views to Sheet"))
            {
                t.Start();
                AddSelectedViewCommand.PlaceViewsOnSheet(doc, targetSheet, window.SelectedViews);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
