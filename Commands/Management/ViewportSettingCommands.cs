using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

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
}
