using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ThreeD;

namespace antiGGGravity.Commands.ThreeD
{
    // ===================================================================================
    // AUTO 3D VIEW
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class Auto3DCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            View sourceView = null;
            string defaultName = "";
            string sourceDesc = "";

            if (activeView is ViewSheet sheet)
            {
                try
                {
                    // Prompt to select viewport
                    Reference r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, new ViewportSelectionFilter(), "Select a viewport");
                    Viewport vp = doc.GetElement(r.ElementId) as Viewport;
                    sourceView = doc.GetElement(vp.ViewId) as View;
                    
                    Parameter detailNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    defaultName = (detailNum != null && detailNum.HasValue) ? detailNum.AsString() : sourceView.Name;
                    sourceDesc = $"{sourceView.Name} (from viewport)";
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }
            else if (activeView is View3D)
            {
                TaskDialog.Show("Auto 3D", "Cannot create 3D view from another 3D view.");
                return Result.Cancelled;
            }
            else
            {
                sourceView = activeView;
                if (!HasCropBox(sourceView))
                {
                    TaskDialog.Show("Auto 3D", "View does not have a crop region.");
                    return Result.Cancelled;
                }
                defaultName = sourceView.Name;
                sourceDesc = $"{sourceView.Name} (from active view)";
            }

            // Show UI
            Auto3DView form = new Auto3DView(sourceView, defaultName, sourceDesc);
            form.ShowDialog();

            if (form.Result != null)
            {
                return Result.Succeeded;
            }

            return Result.Cancelled;
        }

        private bool HasCropBox(View view)
        {
             // Simplified check
             try { var x = view.CropBox; return true; } catch { return false; }
        }

        public class ViewportSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Viewport;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }


    // ===================================================================================
    // TOGGLE 3D SECTION BOX
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class Toggle3DSectionBoxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;

            if (view3D == null)
            {
                TaskDialog.Show("Toggle 3D", "This tool only works in 3D views.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Toggle Section Box"))
            {
                t.Start();
                if (view3D.IsSectionBoxActive)
                {
                    // Save state would go here (omitted for brevity/complexity of finding storage)
                    // Just turn off
                    view3D.IsSectionBoxActive = false;
                }
                else
                {
                    // Turn on
                    view3D.IsSectionBoxActive = true;
                    // Ideally restore saved state
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }


    // ===================================================================================
    // TOGGLE SECTION BOX VISIBILITY
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class ToggleSectionBoxVisibilityCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            var sbIds = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_SectionBox)
                .WhereElementIsNotElementType()
                .ToElementIds();

            if (sbIds.Count == 0) return Result.Succeeded;

            List<ElementId> toHide = new List<ElementId>();
            List<ElementId> toShow = new List<ElementId>();

            foreach (ElementId id in sbIds)
            {
                Element e = doc.GetElement(id);
                if (e.CanBeHidden(view))
                {
                    if (e.IsHidden(view)) toShow.Add(id);
                    else toHide.Add(id);
                }
            }

            using (Transaction t = new Transaction(doc, "Toggle Section Box Visibility"))
            {
                t.Start();
                if (toHide.Count > 0) view.HideElements(toHide);
                else if (toShow.Count > 0) view.UnhideElements(toShow);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
