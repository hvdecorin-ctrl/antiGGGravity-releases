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
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var activeView = doc.ActiveView;
            if (activeView == null) return Result.Failed;

            // 3D Model categories to EXCLUDE (plus all Links)
            var exclude3DCategories = new List<BuiltInCategory> {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Roofs, BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Stairs, BuiltInCategory.OST_Rebar,
                BuiltInCategory.OST_StructConnections, BuiltInCategory.OST_CurtainWallPanels,
                BuiltInCategory.OST_CurtainWallMullions, BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_Casework, BuiltInCategory.OST_Furniture,
                BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_SpecialityEquipment, 
                BuiltInCategory.OST_RvtLinks,  // Revit Links
            };

            var excludeCatIds = new HashSet<ElementId>(exclude3DCategories.Select(c => new ElementId(c)));
            // ImportObjectStyles is usually a category for CAD imports
            excludeCatIds.Add(new ElementId(BuiltInCategory.OST_ImportObjectStyles));

            // Collect ALL elements from document
            var allElementIds = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElementIds();

            // Filter to get only 2D elements (exclude 3D model categories and links)
            var twodElementIds = new List<ElementId>();
            bool hasHidden = false;

            foreach (var elemId in allElementIds)
            {
                var element = doc.GetElement(elemId);
                if (element == null || element.Category == null) continue;
                if (element is ImportInstance) continue;

                if (!excludeCatIds.Contains(element.Category.Id) && element.Id != activeView.Id)
                {
                    twodElementIds.Add(elemId);
                    if (!hasHidden && element.IsHidden(activeView))
                    {
                        hasHidden = true;
                    }
                }
            }

            using (var t = new Transaction(doc, "Toggle 2D Elements"))
            {
                t.Start();
                if (hasHidden)
                {
                    activeView.UnhideElements(twodElementIds);
                }
                else
                {
                    var hideableIds = twodElementIds.Where(id => doc.GetElement(id).CanBeHidden(activeView)).ToList();
                    if (hideableIds.Any())
                        activeView.HideElements(hideableIds);
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
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var activeView = doc.ActiveView;
            if (activeView == null) return Result.Failed;

            // 2D Annotation categories to EXCLUDE (plus all Links)
            var exclude2DCategories = new List<BuiltInCategory> {
                BuiltInCategory.OST_Grids, BuiltInCategory.OST_Levels,
                BuiltInCategory.OST_CLines, BuiltInCategory.OST_Dimensions,
                BuiltInCategory.OST_TextNotes, BuiltInCategory.OST_GenericAnnotation,
                BuiltInCategory.OST_Tags, BuiltInCategory.OST_Callouts,
                BuiltInCategory.OST_Elev, BuiltInCategory.OST_Sections,
                BuiltInCategory.OST_DetailComponents, BuiltInCategory.OST_Lines,
                BuiltInCategory.OST_FilledRegion, BuiltInCategory.OST_MaskingRegion,
                BuiltInCategory.OST_Viewers, BuiltInCategory.OST_RoomTags,
                BuiltInCategory.OST_Matchline,
                BuiltInCategory.OST_RvtLinks,  // Revit Links
            };

            var excludeCatIds = new HashSet<ElementId>(exclude2DCategories.Select(c => new ElementId(c)));
            excludeCatIds.Add(new ElementId(BuiltInCategory.OST_ImportObjectStyles));

            // Collect ALL elements from document
            var allElementIds = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElementIds();

            // Filter to get only 3D elements (exclude 2D annotation categories and links)
            var threedElementIds = new List<ElementId>();
            bool hasHidden = false;

            foreach (var elemId in allElementIds)
            {
                var element = doc.GetElement(elemId);
                if (element == null || element.Category == null) continue;
                if (element is ImportInstance) continue;

                if (!excludeCatIds.Contains(element.Category.Id) && element.Id != activeView.Id)
                {
                    threedElementIds.Add(elemId);
                    if (!hasHidden && element.IsHidden(activeView))
                    {
                        hasHidden = true;
                    }
                }
            }

            using (var t = new Transaction(doc, "Toggle 3D Elements"))
            {
                t.Start();
                if (hasHidden)
                {
                    activeView.UnhideElements(threedElementIds);
                }
                else
                {
                    var hideableIds = threedElementIds.Where(id => doc.GetElement(id).CanBeHidden(activeView)).ToList();
                    if (hideableIds.Any())
                        activeView.HideElements(hideableIds);
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }
}
