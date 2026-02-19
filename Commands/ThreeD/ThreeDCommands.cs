using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
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
        // Unique ID for the Extensible Storage Schema - V4 (Serialized String)
        private static readonly Guid SchemaGuid = new Guid("A47E8A1D-E2F4-4C5A-9D8B-123456789EE0");

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
                    // Turning OFF: Save bounds first
                    BoundingBoxXYZ bbox = view3D.GetSectionBox();
                    SaveSectionBoxBounds(view3D, bbox);
                    view3D.IsSectionBoxActive = false;
                }
                else
                {
                    // Turning ON: Try to restore
                    view3D.IsSectionBoxActive = true; 
                    BoundingBoxXYZ savedBbox = LoadSectionBoxBounds(view3D);
                    if (savedBbox != null)
                    {
                        view3D.SetSectionBox(savedBbox);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }

        private void SaveSectionBoxBounds(View3D view, BoundingBoxXYZ bbox)
        {
            Schema schema = GetOrBuildSchema();
            Entity entity = new Entity(schema);

            // Serialize bounds to string to avoid all Revit 2026 Unit issues
            // Format: MinX,MinY,MinZ|MaxX,MaxY,MaxZ|OriginX,OriginY,OriginZ|BXx,BXy,BXz|BYx,BYy,BYz|BZx,BZy,BZz
            Transform trf = bbox.Transform;
            string data = string.Join("|", 
                $"{bbox.Min.X},{bbox.Min.Y},{bbox.Min.Z}",
                $"{bbox.Max.X},{bbox.Max.Y},{bbox.Max.Z}",
                $"{trf.Origin.X},{trf.Origin.Y},{trf.Origin.Z}",
                $"{trf.BasisX.X},{trf.BasisX.Y},{trf.BasisX.Z}",
                $"{trf.BasisY.X},{trf.BasisY.Y},{trf.BasisY.Z}",
                $"{trf.BasisZ.X},{trf.BasisZ.Y},{trf.BasisZ.Z}"
            );

            entity.Set("SerializedBounds", data);
            view.SetEntity(entity);
        }

        private BoundingBoxXYZ LoadSectionBoxBounds(View3D view)
        {
            Schema schema = GetOrBuildSchema();
            Entity entity = view.GetEntity(schema);

            if (!entity.IsValid()) return null;

            try 
            {
                string data = entity.Get<string>("SerializedBounds");
                if (string.IsNullOrEmpty(data)) return null;

                string[] parts = data.Split('|');
                if (parts.Length < 6) return null;

                BoundingBoxXYZ bbox = new BoundingBoxXYZ();
                bbox.Min = ParseXYZ(parts[0]);
                bbox.Max = ParseXYZ(parts[1]);

                Transform trf = Transform.Identity;
                trf.Origin = ParseXYZ(parts[2]);
                trf.BasisX = ParseXYZ(parts[3]);
                trf.BasisY = ParseXYZ(parts[4]);
                trf.BasisZ = ParseXYZ(parts[5]);

                bbox.Transform = trf;
                return bbox;
            }
            catch { return null; }
        }

        private XYZ ParseXYZ(string s)
        {
            string[] c = s.Split(',');
            return new XYZ(double.Parse(c[0]), double.Parse(c[1]), double.Parse(c[2]));
        }

        private Schema GetOrBuildSchema()
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null) return schema;

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.SetSchemaName("SectionBoxStorageV4");
            builder.SetDocumentation("Stores serialized section box bounds for a 3D view.");

            // Storing as string bypasses all Revit unit/spec compatibility issues
            builder.AddSimpleField("SerializedBounds", typeof(string));

            return builder.Finish();
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
