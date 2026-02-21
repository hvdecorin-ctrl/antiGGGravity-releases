using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Commands;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Engine;
using antiGGGravity.StructuralRebar.UI;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar
{
    /// <summary>
    /// Single ribbon command that opens the unified Rebar Suite window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarSuiteCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show unified UI
            var window = new RebarSuiteWindow(doc);
            window.ShowDialog();

            if (!window.IsConfirmed) return Result.Cancelled;

            // 2. Route by element type
            switch (window.SelectedHostType)
            {
                case ElementHostType.Beam:
                    return ProcessBeams(uidoc, doc, window);
                case ElementHostType.Wall:
                    return ProcessWalls(uidoc, doc, window);
                case ElementHostType.Column:
                    return ProcessColumns(uidoc, doc, window);
                case ElementHostType.StripFooting:
                    return ProcessStripFootings(uidoc, doc, window);
                case ElementHostType.FootingPad:
                    return ProcessFootingPads(uidoc, doc, window);
                case ElementHostType.WallCornerL:
                    return ProcessWallCornerL(uidoc, doc, window);
                case ElementHostType.WallCornerU:
                    return ProcessWallCornerU(uidoc, doc, window);
                default:
                    TaskDialog.Show("Rebar Suite", "Element type not yet supported.");
                    return Result.Cancelled;
            }
        }

        private Result ProcessBeams(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select beams
            List<FamilyInstance> beams;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new BeamSelectionFilter(),
                    "Select beams to reinforce (press Finish)");
                beams = refs
                    .Select(r => doc.GetElement(r.ElementId) as FamilyInstance)
                    .Where(b => b != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (beams.Count == 0) return Result.Cancelled;

            // Build request from panel
            var request = window.BeamPanel.BuildRequest(window.RemoveExisting);

            // Run engine
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateBeamRebar(beams, request);

            TaskDialog.Show("Rebar Suite", $"Successfully reinforced {processed} of {total} beams.");
            return Result.Succeeded;
        }

        private Result ProcessWalls(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select walls
            List<Wall> walls;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select walls to reinforce (press Finish)");
                walls = refs
                    .Select(r => doc.GetElement(r.ElementId) as Wall)
                    .Where(w => w != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (walls.Count == 0) return Result.Cancelled;

            // Build request from panel
            var request = window.WallPanel.GetRequest();

            // Run engine
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallRebar(walls, request);

            TaskDialog.Show("Rebar Suite", $"Successfully reinforced {processed} of {total} walls.");
            return Result.Succeeded;
        }

        private Result ProcessColumns(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select columns
            List<FamilyInstance> columns;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new ColumnSelectionFilter(),
                    "Select columns to reinforce (press Finish)");
                columns = refs
                    .Select(r => doc.GetElement(r.ElementId) as FamilyInstance)
                    .Where(c => c != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (columns.Count == 0) return Result.Cancelled;

            // Build request from panel
            var request = window.ColumnPanel.GetRequest();

            // Run engine
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateColumnRebar(columns, request);

            TaskDialog.Show("Rebar Suite", $"Successfully reinforced {processed} of {total} columns.");
            return Result.Succeeded;
        }

        private Result ProcessStripFootings(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select foundations
            List<Element> foundations;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StripFootingSelectionFilter(),
                    "Select strip footings to reinforce (press Finish)");
                foundations = refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(f => f != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (foundations.Count == 0) return Result.Cancelled;

            // Build request from panel
            var request = window.StripFootingPanel.GetRequest();

            // Run engine
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateStripFootingRebar(foundations, request);

            TaskDialog.Show("Rebar Suite", $"Successfully reinforced {processed} of {total} strip footings.");
            return Result.Succeeded;
        }

        private Result ProcessFootingPads(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select foundations
            List<Element> foundations;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StripFootingSelectionFilter(), // Same filter for pads
                    "Select footing pads to reinforce (press Finish)");
                foundations = refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(f => f != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (foundations.Count == 0) return Result.Cancelled;

            // Build request from panel
            var request = window.FootingPadPanel.GetRequest();

            // Run engine
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateFootingPadRebar(foundations, request);

            TaskDialog.Show("Rebar Suite", $"Successfully reinforced {processed} of {total} footing pads.");
            return Result.Succeeded;
        }

        private Result ProcessWallCornerL(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select walls
            List<Wall> walls;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select walls forming L-corners (press Finish)");
                walls = refs
                    .Select(r => doc.GetElement(r.ElementId) as Wall)
                    .Where(w => w != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (walls.Count < 2) return Result.Cancelled;

            // Build request from panel
            var request = window.WallCornerLPanel.GetRequest();

            // Run engine specialized corner logic
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallCornerRebar(walls, request);

            TaskDialog.Show("Rebar Suite", $"Successfully generated rebar at {processed} of {total} identified corners.");
            return Result.Succeeded;
        }

        private Result ProcessWallCornerU(UIDocument uidoc, Document doc, RebarSuiteWindow window)
        {
            // Select walls
            List<Wall> walls;
            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select walls forming U-corners (press Finish)");
                walls = refs
                    .Select(r => doc.GetElement(r.ElementId) as Wall)
                    .Where(w => w != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (walls.Count < 2) return Result.Cancelled;

            // Build request from panel
            var request = window.WallCornerUPanel.GetRequest();

            // Run engine specialized corner logic
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallCornerRebar(walls, request);

            TaskDialog.Show("Rebar Suite", $"Successfully generated rebar at {processed} of {total} identified corners.");
            return Result.Succeeded;
        }

        private class BeamSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private class ColumnSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private class StripFootingSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
