using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ThreeD;

namespace antiGGGravity.Commands.Management
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
                    SaveSectionBox(view3D);
                    view3D.IsSectionBoxActive = false;
                }
                else
                {
                    // Must activate first, then set bounds
                    view3D.IsSectionBoxActive = true;
                    LoadSectionBox(view3D);
                }
                t.Commit();
            }

            return Result.Succeeded;
        }

        private string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pyRevitFolder = System.IO.Path.Combine(appData, "pyRevit");
            if (!System.IO.Directory.Exists(pyRevitFolder))
            {
                System.IO.Directory.CreateDirectory(pyRevitFolder);
            }
            return System.IO.Path.Combine(pyRevitFolder, "antiGGGravity_Toggle3D.json");
        }

        private void SaveSectionBox(View3D view)
        {
            BoundingBoxXYZ bbox = view.GetSectionBox();
            if (bbox == null) return;

            Transform t = bbox.Transform ?? Transform.Identity;
            
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                { "view_id", view.Id.Value },
                { "min_x", bbox.Min.X }, { "min_y", bbox.Min.Y }, { "min_z", bbox.Min.Z },
                { "max_x", bbox.Max.X }, { "max_y", bbox.Max.Y }, { "max_z", bbox.Max.Z },
                { "origin_x", t.Origin.X }, { "origin_y", t.Origin.Y }, { "origin_z", t.Origin.Z },
                { "basis_x_x", t.BasisX.X }, { "basis_x_y", t.BasisX.Y }, { "basis_x_z", t.BasisX.Z },
                { "basis_y_x", t.BasisY.X }, { "basis_y_y", t.BasisY.Y }, { "basis_y_z", t.BasisY.Z },
                { "basis_z_x", t.BasisZ.X }, { "basis_z_y", t.BasisZ.Y }, { "basis_z_z", t.BasisZ.Z }
            };

            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(data);
                System.IO.File.WriteAllText(GetConfigFilePath(), json);
            }
            catch { }
        }

        private void LoadSectionBox(View3D view)
        {
            string path = GetConfigFilePath();
            if (!System.IO.File.Exists(path)) return;

            try
            {
                string json = System.IO.File.ReadAllText(path);
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(json);

                if (data == null || !data.ContainsKey("view_id")) return;

                // Check if this saved box belongs to the current view
                long savedViewId = data["view_id"].GetInt64();
                if (savedViewId != view.Id.Value) return;

                XYZ origin = new XYZ(data["origin_x"].GetDouble(), data["origin_y"].GetDouble(), data["origin_z"].GetDouble());
                XYZ basisX = new XYZ(data["basis_x_x"].GetDouble(), data["basis_x_y"].GetDouble(), data["basis_x_z"].GetDouble());
                XYZ basisY = new XYZ(data["basis_y_x"].GetDouble(), data["basis_y_y"].GetDouble(), data["basis_y_z"].GetDouble());
                XYZ basisZ = new XYZ(data["basis_z_x"].GetDouble(), data["basis_z_y"].GetDouble(), data["basis_z_z"].GetDouble());

                Transform t = Transform.Identity;
                t.Origin = origin;
                t.BasisX = basisX;
                t.BasisY = basisY;
                t.BasisZ = basisZ;

                BoundingBoxXYZ bbox = new BoundingBoxXYZ();
                bbox.Transform = t;
                bbox.Min = new XYZ(data["min_x"].GetDouble(), data["min_y"].GetDouble(), data["min_z"].GetDouble());
                bbox.Max = new XYZ(data["max_x"].GetDouble(), data["max_y"].GetDouble(), data["max_z"].GetDouble());

                view.SetSectionBox(bbox);
            }
            catch { }
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
