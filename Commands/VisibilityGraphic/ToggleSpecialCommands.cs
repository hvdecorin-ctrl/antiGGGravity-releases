using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    // --- TOGGLE CAD LINKS ---
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleCadLinksCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var cadLinks = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .ToElements();

            if (!cadLinks.Any()) return Result.Succeeded;

            var hidden = cadLinks.Where(e => e.IsHidden(view)).ToList();
            var visible = cadLinks.Where(e => !e.IsHidden(view)).ToList();

            using (var t = new Transaction(doc, "Toggle CAD Links"))
            {
                t.Start();
                if (hidden.Any()) view.UnhideElements(hidden.Select(e => e.Id).ToList());
                else if (visible.Any()) view.HideElements(visible.Select(e => e.Id).ToList());
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    // --- TOGGLE STRUCTURAL PACKS (MULTI-CATEGORY) ---
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleStructuralPacksCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation, BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows, BuiltInCategory.OST_Roofs, BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_CurtainWallPanels, BuiltInCategory.OST_CurtainWallMullions,
                BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Casework, BuiltInCategory.OST_Furniture,
                BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_Rebar,
                BuiltInCategory.OST_StructConnections
            };

            var filter = new ElementMulticategoryFilter(categories.Select(c => new ElementId(c)).ToList());
            var elementsToToggle = new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .ToElements();

            if (!elementsToToggle.Any()) return Result.Succeeded;

            var hidden = elementsToToggle.Where(e => e.IsHidden(view)).ToList();
            var visible = elementsToToggle.Where(e => !e.IsHidden(view)).ToList();

            using (var t = new Transaction(doc, "Toggle Structural Packs"))
            {
                t.Start();
                if (hidden.Any()) view.UnhideElements(hidden.Select(e => e.Id).ToList());
                else if (visible.Any()) view.HideElements(visible.Select(e => e.Id).ToList());
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    // --- TOGGLE 2D ELEMENTS ---
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Toggle2DCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var exclude3D = new HashSet<ElementId> {
                new ElementId(BuiltInCategory.OST_Walls), new ElementId(BuiltInCategory.OST_Floors),
                new ElementId(BuiltInCategory.OST_Columns), new ElementId(BuiltInCategory.OST_StructuralColumns),
                new ElementId(BuiltInCategory.OST_StructuralFraming), new ElementId(BuiltInCategory.OST_StructuralFoundation),
                new ElementId(BuiltInCategory.OST_Doors), new ElementId(BuiltInCategory.OST_Windows),
                new ElementId(BuiltInCategory.OST_Roofs), new ElementId(BuiltInCategory.OST_Ceilings),
                new ElementId(BuiltInCategory.OST_Stairs), new ElementId(BuiltInCategory.OST_Rebar)
            };

            var allElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            var twodElements = allElements.Where(e => 
                e.Category != null && 
                !exclude3D.Contains(e.Category.Id) && 
                !(e is ImportInstance) &&
                e.Id != view.Id).ToList();

            if (!twodElements.Any()) return Result.Succeeded;

            bool hasHidden = twodElements.Any(e => e.IsHidden(view));

            using (var t = new Transaction(doc, "Toggle 2D Elements"))
            {
                t.Start();
                if (hasHidden)
                {
                    var ids = twodElements.Select(e => e.Id).ToList();
                    if (ids.Any()) view.UnhideElements(ids);
                }
                else
                {
                    var ids = twodElements.Where(e => e.CanBeHidden(view)).Select(e => e.Id).ToList();
                    if (ids.Any()) view.HideElements(ids);
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    // --- TOGGLE 3D ELEMENTS ---
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Toggle3DCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var exclude2D = new HashSet<ElementId> {
                new ElementId(BuiltInCategory.OST_Grids), new ElementId(BuiltInCategory.OST_Levels),
                new ElementId(BuiltInCategory.OST_CLines), new ElementId(BuiltInCategory.OST_Dimensions),
                new ElementId(BuiltInCategory.OST_TextNotes), new ElementId(BuiltInCategory.OST_GenericAnnotation)
            };

            var allElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            var threedElements = allElements.Where(e => 
                e.Category != null && 
                !exclude2D.Contains(e.Category.Id) && 
                !(e is ImportInstance) &&
                e.Id != view.Id).ToList();

            if (!threedElements.Any()) return Result.Succeeded;

            bool hasHidden = threedElements.Any(e => e.IsHidden(view));

            using (var t = new Transaction(doc, "Toggle 3D Elements"))
            {
                t.Start();
                if (hasHidden)
                {
                    var ids = threedElements.Select(e => e.Id).ToList();
                    if (ids.Any()) view.UnhideElements(ids);
                }
                else
                {
                    var ids = threedElements.Where(e => e.CanBeHidden(view)).Select(e => e.Id).ToList();
                    if (ids.Any()) view.HideElements(ids);
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }
}
