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
    public class FlipElementsCommand : IExternalCommand
    {
        private const int CORE_CENTERLINE = 1;
        private static readonly Dictionary<int, int> LOCATION_LINE_FLIP = new Dictionary<int, int>
        {
            {0, 0}, {1, 1}, {2, 3}, {3, 2}, {4, 5}, {5, 4}
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selIds = uidoc.Selection.GetElementIds();
            var selectedElements = selIds.Select(id => doc.GetElement(id)).ToList();

            if (!selectedElements.Any())
            {
                try { var refs = uidoc.Selection.PickObjects(ObjectType.Element, "Select elements to flip"); selectedElements = refs.Select(r => doc.GetElement(r)).ToList(); }
                catch { return Result.Cancelled; }
            }

            TaskDialog td = new TaskDialog("Flip Elements");
            td.MainContent = "Select flip operation:";
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Flip Facing (Maintain Wall Position)");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Flip Hand");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Flip Wall Interior/Exterior Face");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Reset Flipped Walls");

            TaskDialogResult res = td.Show();

            using (Transaction t = new Transaction(doc, "Flip Elements"))
            {
                t.Start();
                if (res == TaskDialogResult.CommandLink1)
                {
                    foreach (var el in selectedElements)
                    {
                        if (el is Wall wall) FlipWallMaintained(wall);
                        else if (el is FamilyInstance fi && fi.CanFlipFacing) fi.flipFacing();
                    }
                }
                else if (res == TaskDialogResult.CommandLink2)
                {
                    foreach (var el in selectedElements)
                    {
                        if (el is FamilyInstance fi && fi.CanFlipHand) fi.flipHand();
                    }
                }
                else if (res == TaskDialogResult.CommandLink3)
                {
                    foreach (var el in selectedElements)
                    {
                        if (el is Wall wall)
                        {
                            Parameter p = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            int current = p.AsInteger();
                            if (LOCATION_LINE_FLIP.ContainsKey(current)) p.Set(LOCATION_LINE_FLIP[current]);
                        }
                    }
                }
                else if (res == TaskDialogResult.CommandLink4)
                {
                    foreach (var el in selectedElements)
                    {
                        if (el is Wall wall && wall.Flipped) FlipWallMaintained(wall);
                    }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }

        private void FlipWallMaintained(Wall wall)
        {
            Parameter p = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
            int original = p.AsInteger();
            p.Set(CORE_CENTERLINE);
            wall.Flip();
            p.Set(original);
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ElementsRotateMultipleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selIds = uidoc.Selection.GetElementIds();
            if (!selIds.Any()) 
            {
                TaskDialog.Show("Rotate", "Please select elements to rotate.");
                return Result.Cancelled;
            }

            RotateElementsView win = new RotateElementsView();
            if (win.ShowDialog() != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Rotate Elements Multiple"))
            {
                t.Start();
                foreach (ElementId id in selIds)
                {
                    Element el = doc.GetElement(id);
                    BoundingBoxXYZ bbox = el.get_BoundingBox(null);
                    if (bbox == null) continue;

                    XYZ center = (bbox.Min + bbox.Max) * 0.5;
                    Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
                    
                    try { ElementTransformUtils.RotateElement(doc, id, axis, win.AngleRadians); }
                    catch { }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class JoinUnjoinGeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var categories = doc.Settings.Categories.Cast<Category>().Where(c => c.CategoryType == Autodesk.Revit.DB.CategoryType.Model).ToList();
            JoinUnjoinView win = new JoinUnjoinView(categories);
            if (win.ShowDialog() != true) return Result.Cancelled;

            var leftCats = win.LeftCategories.Where(c => c.IsChecked).Select(c => c.Category.Id).ToList();
            var rightCats = win.RightCategories.Where(c => c.IsChecked).Select(c => c.Category.Id).ToList();

            var collector = new FilteredElementCollector(doc, doc.ActiveView.Id).WhereElementIsNotElementType();
            var allElements = collector.ToElements();

            var leftElements = allElements.Where(e => e.Category != null && leftCats.Contains(e.Category.Id)).ToList();
            var rightElements = allElements.Where(e => e.Category != null && rightCats.Contains(e.Category.Id)).ToList();

            using (TransactionGroup tg = new TransactionGroup(doc, "Join/Unjoin Advance"))
            {
                tg.Start();
                foreach (var left in leftElements)
                {
                    foreach (var right in rightElements)
                    {
                        if (left.Id == right.Id) continue;
                        
                        using (Transaction t = new Transaction(doc, "Process Single Join"))
                        {
                            t.Start();
                            try
                            {
                                if (win.IsJoinOperation)
                                {
                                    if (!JoinGeometryUtils.AreElementsJoined(doc, left, right))
                                        JoinGeometryUtils.JoinGeometry(doc, left, right);
                                }
                                else
                                {
                                    if (JoinGeometryUtils.AreElementsJoined(doc, left, right))
                                        JoinGeometryUtils.UnjoinGeometry(doc, left, right);
                                }
                                t.Commit();
                            }
                            catch { t.RollBack(); }
                        }
                    }
                }
                tg.Assimilate();
            }

            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class TransferTemplatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Transfer Templates", "This tool will be fully implemented in a separate batch due to source/destination document complexity.");
            return Result.Succeeded;
        }
    }
}
