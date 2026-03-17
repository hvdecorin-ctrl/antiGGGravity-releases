using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.General;

namespace antiGGGravity.Commands.General
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class WallMatchTopCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Wall sourceWall;
            try 
            { 
                Reference refSource = uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Select source wall");
                sourceWall = doc.GetElement(refSource) as Wall; 
            }
            catch { return Result.Cancelled; }

            ElementId topId = sourceWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
            bool isUnconnected = topId.GetIdValue() == -1;

            double mainUnconnectedHeight = sourceWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double mainBaseOffset = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            double sourceZOrigin = (sourceWall.Location as LocationCurve).Curve.GetEndPoint(0).Z;
            double mainZTop = sourceZOrigin + mainUnconnectedHeight + mainBaseOffset;

            while (true)
            {
                Wall targetWall;
                try 
                { 
                    Reference refTarget = uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Pick target wall to match TOP (Esc to finish)");
                    targetWall = doc.GetElement(refTarget) as Wall; 
                }
                catch { break; }

                using (Transaction t = new Transaction(doc, "Wall Match: Top"))
                {
                    t.Start();
                    targetWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(topId);
                    if (isUnconnected)
                    {
                        double targetBaseOffset = targetWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
                        double targetUnconnectedHeight = targetWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
                        double targetZOrigin = (targetWall.Location as LocationCurve).Curve.GetEndPoint(0).Z;
                        double targetZTop = targetZOrigin + targetBaseOffset + targetUnconnectedHeight;
                        
                        double newHeight = targetUnconnectedHeight + mainZTop - targetZTop;
                        targetWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(newHeight);
                    }
                    else
                    {
                        double topOffset = sourceWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();
                        targetWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
                    }
                    t.Commit();
                }
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class WallMatchBaseCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Wall sourceWall;
            try { sourceWall = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Select source wall")) as Wall; }
            catch { return Result.Cancelled; }

            ElementId baseId = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
            double baseOffset = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();

            while (true)
            {
                Wall targetWall;
                try { targetWall = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Pick target wall to match BASE (Esc to finish)")) as Wall; }
                catch { break; }

                using (Transaction t = new Transaction(doc, "Wall Match: Base"))
                {
                    t.Start();
                    targetWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).Set(baseId);
                    targetWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseOffset);
                    t.Commit();
                }
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class WallMatchBothCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            Wall sourceWall;
            try { sourceWall = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Select source wall")) as Wall; }
            catch { return Result.Cancelled; }

            // Base
            ElementId baseId = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
            double baseOffset = sourceWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            // Top
            ElementId topId = sourceWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
            double topOffset = sourceWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();
            double unconnectedHeight = sourceWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

            while (true)
            {
                Wall targetWall;
                try { targetWall = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilter_Walls(), "Pick target wall to match BOTH (Esc to finish)")) as Wall; }
                catch { break; }

                using (Transaction t = new Transaction(doc, "Wall Match: Both"))
                {
                    t.Start();
                    // Base
                    targetWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).Set(baseId);
                    targetWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(baseOffset);
                    // Top
                    targetWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(topId);
                    if (topId.GetIdValue() == -1)
                        targetWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(unconnectedHeight);
                    else
                        targetWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
                    t.Commit();
                }
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ModifyWallConstraintsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<Wall> selectedWalls;
            var selIds = uidoc.Selection.GetElementIds();
            if (selIds.Any())
            {
                selectedWalls = selIds.Select(id => doc.GetElement(id)).OfType<Wall>().ToList();
            }
            else
            {
                try 
                { 
                    var refs = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilter_Walls(), "Select walls to modify constraints");
                    selectedWalls = refs.Select(r => doc.GetElement(r)).OfType<Wall>().ToList();
                }
                catch { return Result.Cancelled; }
            }

            if (!selectedWalls.Any()) return Result.Cancelled;

            ModifyWallConstraintsView win = new ModifyWallConstraintsView(doc);
            if (win.ShowDialog() != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Modify Wall Levels"))
            {
                t.Start();
                foreach (Wall wall in selectedWalls)
                {
                    try
                    {
                        if (doc.IsWorkshared && WorksharingUtils.GetCheckoutStatus(doc, wall.Id) == CheckoutStatus.OwnedByOtherUser) continue;

                        Parameter pBaseLevel = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                        Parameter pBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);
                        Parameter pTopLevel = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                        Parameter pTopOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                        Parameter pHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);

                        double wallHeight = pHeight.AsDouble();
                        double baseOffset = pBaseOffset.AsDouble();
                        Level currentBaseLevel = doc.GetElement(pBaseLevel.AsElementId()) as Level;
                        double baseElev = currentBaseLevel.Elevation;

                        double wallBaseElevation = baseElev + baseOffset;
                        double wallTopElevation = wallBaseElevation + wallHeight;

                        if (win.ModifyBase)
                        {
                            pBaseLevel.Set(win.SelectedBaseLevel.Id);
                            pBaseOffset.Set(wallBaseElevation - win.SelectedBaseLevel.Elevation);
                        }

                        if (win.ModifyTop)
                        {
                            pTopLevel.Set(win.SelectedTopLevel.Id);
                            pTopOffset.Set(wallTopElevation - win.SelectedTopLevel.Elevation);
                        }
                    }
                    catch { }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    public class SelectionFilter_Walls : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Wall;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
