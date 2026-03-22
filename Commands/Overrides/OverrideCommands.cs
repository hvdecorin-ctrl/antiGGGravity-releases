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
    // STYLE OVERRIDES
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class Style1Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Black, Proj: W1, Cut: W3, Halftone, 50% Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 0, 0), 1, true, 50, null, 3);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style2Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Orange, Proj: W1, Cut: W3, Halftone, 50% Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(255, 128, 0), 1, true, 50, null, 3);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style3Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Blue, Proj: W1, Cut: W3, Halftone, 50% Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 0, 255), 1, true, 50, null, 3);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style4Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Green, Proj: W1, Cut: W3, Halftone, 50% Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(0, 150, 0), 1, true, 50, null, 3);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Style5Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Purple-Blue (128, 128, 255), Projection: W1 (Hidden), Cut: W3 (Solid), Halftone, 50% Transparency
            return OverrideUtils.ApplyStyle(commandData, new Color(128, 128, 255), 1, true, 50, "Hidden", 3);
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
    public class DisableFiltersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            using (Transaction t = new Transaction(doc, "Disable Filters"))
            {
                t.Start();
                foreach (ElementId filterId in activeView.GetFilters())
                {
                    activeView.SetIsFilterEnabled(filterId, false);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class EnableFiltersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            using (Transaction t = new Transaction(doc, "Enable Filters"))
            {
                t.Start();
                foreach (ElementId filterId in activeView.GetFilters())
                {
                    activeView.SetIsFilterEnabled(filterId, true);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class RemoveFiltersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            using (Transaction t = new Transaction(doc, "Remove Filters"))
            {
                t.Start();
                foreach (ElementId filterId in activeView.GetFilters())
                {
                    activeView.RemoveFilter(filterId);
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

                // 2. Continuous Destination Selection Loop
                while (true)
                {
                    Reference destRef = null;
                    try
                    {
                        destRef = uidoc.Selection.PickObject(ObjectType.Element, "Select destination element to copy overrides TO (ESC to exit).");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break; // User pressed ESC
                    }

                    if (destRef == null) break;

                    // 3. Apply Overrides Immediately
                    using (Transaction t = new Transaction(doc, "Match Overrides"))
                    {
                        t.Start();
                        activeView.SetElementOverrides(destRef.ElementId, sourceSettings);
                        t.Commit();
                    }
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

    [Transaction(TransactionMode.Manual)]
    public class ProjectCadOverrideCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show Selection UI
            var selectionView = new CadStyleSelectionView();
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var wrapper = new System.Windows.Interop.WindowInteropHelper(selectionView);
                wrapper.Owner = process.MainWindowHandle;
            }
            catch { }

            if (selectionView.ShowDialog() != true || selectionView.IsCancelled) return Result.Cancelled;

            int styleIdx = selectionView.SelectedStyle;
            bool applyProject = selectionView.ApplyToProject;

            try
            {
                // 2. Define Style Settings
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                Color color = new Color(0, 0, 0);
                string patternName = null;

                switch (styleIdx)
                {
                    case 1: color = new Color(0, 0, 0); break;
                    case 2: color = new Color(255, 128, 0); break;
                    case 3: color = new Color(0, 0, 255); break;
                    case 4: color = new Color(0, 150, 0); break;
                    case 5: color = new Color(128, 128, 255); patternName = "Hidden"; break;
                }

                ogs.SetProjectionLineColor(color);
                ogs.SetProjectionLineWeight(1);
                ogs.SetCutLineColor(color);
                ogs.SetCutLineWeight(3);
                ogs.SetHalftone(true);
                ogs.SetSurfaceTransparency(50);

                if (!string.IsNullOrEmpty(patternName))
                {
                    ElementId patternId = OverrideUtils.GetLinePatternId(doc, patternName);
                    if (patternId != ElementId.InvalidElementId)
                    {
                        ogs.SetProjectionLinePatternId(patternId);
                    }
                }
                
                // For Style 5, ensure Cut Pattern is Solid (InvalidElementId reverts to default/solid)
                // Also ensure other styles have default solid cut pattern
                ogs.SetCutLinePatternId(ElementId.InvalidElementId);

                // 3. Collect CAD Elements
                var cadIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .ToElementIds();

                if (!cadIds.Any())
                {
                    TaskDialog.Show("Project CAD Override", "No CAD files found in the project.");
                    return Result.Succeeded;
                }

                using (Transaction t = new Transaction(doc, "Project CAD Override"))
                {
                    t.Start();

                    if (!applyProject)
                    {
                        // Current View Only
                        View activeView = doc.ActiveView;
                        foreach (ElementId id in cadIds)
                        {
                            try { activeView.SetElementOverrides(id, ogs); } catch { }
                        }
                    }
                    else
                    {
                        // All Views
                        var views = new FilteredElementCollector(doc)
                            .OfClass(typeof(View))
                            .Cast<View>()
                            .Where(v => !v.IsTemplate && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser);

                        foreach (View v in views)
                        {
                            foreach (ElementId id in cadIds)
                            {
                                try { v.SetElementOverrides(id, ogs); } catch { }
                            }
                        }
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
        public static Result ApplyStyle(ExternalCommandData commandData, Autodesk.Revit.DB.Color color, int weight, bool halftone, int transparency, string patternName = null, int? cutWeight = null, string cutPatternName = null)
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
                
                if (!string.IsNullOrEmpty(patternName))
                {
                    ElementId patternId = GetLinePatternId(doc, patternName);
                    if (patternId != ElementId.InvalidElementId)
                    {
                        ogs.SetProjectionLinePatternId(patternId);
                    }
                }
                
                // Cut Lines
                ogs.SetCutLineColor(color);
                ogs.SetCutLineWeight(cutWeight ?? weight);

                if (!string.IsNullOrEmpty(cutPatternName))
                {
                    ElementId patternId = GetLinePatternId(doc, cutPatternName);
                    if (patternId != ElementId.InvalidElementId)
                    {
                        ogs.SetCutLinePatternId(patternId);
                    }
                }
                else if (cutWeight.HasValue)
                {
                    // If cut weight is provided but no pattern, ensure it's solid (Style 5 requirement)
                    ogs.SetCutLinePatternId(ElementId.InvalidElementId);
                }

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

        public static ElementId GetLinePatternId(Document doc, string patternName)
        {
            var pattern = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .FirstOrDefault(x => x.Name.Contains(patternName));

            return pattern?.Id ?? ElementId.InvalidElementId;
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
