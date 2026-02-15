using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.Overrides;

namespace antiGGGravity.Commands.Overrides
{
    // ===================================================================================
    // COLOR SPLASHER
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class ColorSplashCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ColorSplasherView view = new ColorSplasherView(commandData);
                view.Show();
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
    // STYLE OVERRIDES
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class Style1Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 0, 0), 1, true, 100);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style2Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Red, Weight 6, No Halftone, No Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(255, 0, 0), 6, false, 0);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style3Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Blue, Weight 6, No Halftone, No Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 0, 255), 6, false, 0);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style4Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Green, Weight 6, No Halftone, No Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 128, 0), 6, false, 0);
        }
    }

    // ===================================================================================
    // TRANSPARENCY OVERRIDES
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class Transparency60Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OverrideUtils.ApplyTransparency(commandData, 60);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Transparency100Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OverrideUtils.ApplyTransparency(commandData, 100);
        }
    }

    // ===================================================================================
    // RESET OVERRIDES
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class ResetSelectedCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Reset Overrides", "Please select elements to reset.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Reset Overrides"))
            {
                t.Start();
                OverrideGraphicSettings clearSettings = new OverrideGraphicSettings();
                foreach (ElementId id in selectedIds)
                {
                    activeView.SetElementOverrides(id, clearSettings);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ResetOverridesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // Collect all view-independent elements in the view
            var collector = new FilteredElementCollector(doc, activeView.Id)
                .WhereElementIsNotElementType()
                .ToElementIds();

            using (Transaction t = new Transaction(doc, "Reset All View Overrides"))
            {
                t.Start();
                OverrideGraphicSettings clearSettings = new OverrideGraphicSettings();
                foreach (ElementId id in collector)
                {
                    try
                    {
                        activeView.SetElementOverrides(id, clearSettings);
                    }
                    catch
                    {
                        // Some elements might not support overrides, ignore
                    }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class MatchOverridesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            try
            {
                // 1. Pick Source Element
                Reference sourceRef = null;
                try
                {
                    sourceRef = uidoc.Selection.PickObject(ObjectType.Element, "Select source element to copy overrides FROM.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                Element sourceElement = doc.GetElement(sourceRef);
                OverrideGraphicSettings sourceSettings = activeView.GetElementOverrides(sourceElement.Id);

                // 2. Pick Destination Elements
                List<Reference> destRefs = null;
                try
                {
                    destRefs = uidoc.Selection.PickObjects(ObjectType.Element, "Select destination elements to copy overrides TO.").ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (destRefs == null || destRefs.Count == 0) return Result.Cancelled;

                // 3. Apply Overrides
                using (Transaction t = new Transaction(doc, "Match Overrides"))
                {
                    t.Start();
                    foreach (Reference refElem in destRefs)
                    {
                        Element destElement = doc.GetElement(refElem);
                        activeView.SetElementOverrides(destElement.Id, sourceSettings);
                    }
                    t.Commit();
                }

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
    // UTILITIES
    // ===================================================================================

    internal static class OverrideUtils
    {
        public static Result ApplyStyle(ExternalCommandData commandData, Autodesk.Revit.DB.Color color, int weight, bool halftone, int transparency)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Override Graphics", "Please select elements to override.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Apply Override Style"))
            {
                t.Start();
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                
                // Projection Lines
                ogs.SetProjectionLineColor(color);
                ogs.SetProjectionLineWeight(weight);
                
                // Cut Lines
                ogs.SetCutLineColor(color);
                ogs.SetCutLineWeight(weight);

                // Halftone & Transparency
                ogs.SetHalftone(halftone);
                ogs.SetSurfaceTransparency(transparency);

                foreach (ElementId id in selectedIds)
                {
                    activeView.SetElementOverrides(id, ogs);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }

        public static Result ApplyTransparency(ExternalCommandData commandData, int transparency)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Apply Transparency", "Please select elements.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Apply Transparency"))
            {
                t.Start();
                foreach (ElementId id in selectedIds)
                {
                    // Get existing overrides to preserve other settings
                    OverrideGraphicSettings ogs = activeView.GetElementOverrides(id);
                    ogs.SetSurfaceTransparency(transparency);
                    activeView.SetElementOverrides(id, ogs);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
