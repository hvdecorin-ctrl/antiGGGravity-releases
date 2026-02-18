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
}
