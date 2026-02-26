using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Engine;
using antiGGGravity.StructuralRebar.UI;
using antiGGGravity.StructuralRebar.UI.Panels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core
{
    /// <summary>
    /// IExternalEventHandler that bridges the modeless RebarSuiteWindow to Revit's main thread.
    /// When the user clicks "Generate", the window hides and raises the ExternalEvent.
    /// Revit then calls Execute() on the main thread, where we can safely use the API.
    /// After execution, the window re-shows so the user can generate again.
    /// </summary>
    public class RebarGenerateHandler : IExternalEventHandler
    {
        private RebarSuiteWindow _window;

        public RebarGenerateHandler(RebarSuiteWindow window)
        {
            _window = window;
        }

        public void SetWindow(RebarSuiteWindow window)
        {
            _window = window;
        }

        public string GetName() => "RebarSuite_Generate";

        public void Execute(UIApplication app)
        {
            try
            {
                if (_window == null) return;

                UIDocument uidoc = app.ActiveUIDocument;
                Document doc = uidoc.Document;

                ElementHostType hostType = _window.SelectedHostType;
                bool removeExisting = _window.RemoveExisting;

                string resultMessage;

                switch (hostType)
                {
                    case ElementHostType.Beam:
                        resultMessage = ProcessBeams(uidoc, doc);
                        break;
                    case ElementHostType.Wall:
                        resultMessage = ProcessWalls(uidoc, doc);
                        break;
                    case ElementHostType.Column:
                        resultMessage = ProcessColumns(uidoc, doc);
                        break;
                    case ElementHostType.StripFooting:
                        resultMessage = ProcessStripFootings(uidoc, doc);
                        break;
                    case ElementHostType.FootingPad:
                        resultMessage = ProcessFootingPads(uidoc, doc);
                        break;
                    case ElementHostType.WallCornerL:
                        resultMessage = ProcessWallCornerL(uidoc, doc);
                        break;
                    case ElementHostType.WallCornerU:
                        resultMessage = ProcessWallCornerU(uidoc, doc);
                        break;
                    default:
                        resultMessage = null;
                        TaskDialog.Show("Rebar Suite", "Element type not yet supported.");
                        break;
                }

                if (resultMessage != null)
                    TaskDialog.Show("Rebar Suite", resultMessage);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled selection — that's fine, just re-show window
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Suite Error", $"An error occurred:\n{ex.Message}");
            }
            finally
            {
                // Always re-show the window so the user can adjust and try again
                _window?.ReShow();
            }
        }

        // ────────────────────────────────────────
        //  Process methods (extracted from old RebarSuiteCommand)
        // ────────────────────────────────────────

        private string ProcessBeams(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new BeamSelectionFilter(),
                "Select beams to reinforce (press Finish)");
            var beams = refs
                .Select(r => doc.GetElement(r.ElementId) as FamilyInstance)
                .Where(b => b != null)
                .ToList();

            if (beams.Count == 0) return null;

            var request = _window.BeamPanel.BuildRequest(_window.RemoveExisting);
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateBeamRebar(beams, request);
            return $"Successfully reinforced {processed} of {total} beams.";
        }

        private string ProcessWalls(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new WallSelectionFilter(),
                "Select walls to reinforce (press Finish)");
            var walls = refs
                .Select(r => doc.GetElement(r.ElementId) as Wall)
                .Where(w => w != null)
                .ToList();

            if (walls.Count == 0) return null;

            var request = _window.WallPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallRebar(walls, request);
            return $"Successfully reinforced {processed} of {total} walls.";
        }

        private string ProcessColumns(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new ColumnSelectionFilter(),
                "Select columns to reinforce (press Finish)");
            var columns = refs
                .Select(r => doc.GetElement(r.ElementId) as FamilyInstance)
                .Where(c => c != null)
                .ToList();

            if (columns.Count == 0) return null;

            var request = _window.ColumnPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateColumnRebar(columns, request);
            return $"Successfully reinforced {processed} of {total} columns.";
        }

        private string ProcessStripFootings(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new StripFootingSelectionFilter(),
                "Select strip footings to reinforce (press Finish)");
            var foundations = refs
                .Select(r => doc.GetElement(r.ElementId))
                .Where(f => f != null)
                .ToList();

            if (foundations.Count == 0) return null;

            var request = _window.StripFootingPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateStripFootingRebar(foundations, request);
            return $"Successfully reinforced {processed} of {total} strip footings.";
        }

        private string ProcessFootingPads(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new StripFootingSelectionFilter(),
                "Select footing pads to reinforce (press Finish)");
            var foundations = refs
                .Select(r => doc.GetElement(r.ElementId))
                .Where(f => f != null)
                .ToList();

            if (foundations.Count == 0) return null;

            var request = _window.FootingPadPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateFootingPadRebar(foundations, request);
            return $"Successfully reinforced {processed} of {total} footing pads.";
        }

        private string ProcessWallCornerL(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new WallSelectionFilter(),
                "Select walls forming L-corners (press Finish)");
            var walls = refs
                .Select(r => doc.GetElement(r.ElementId) as Wall)
                .Where(w => w != null)
                .ToList();

            if (walls.Count < 2) return null;

            var request = _window.WallCornerLPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallCornerRebar(walls, request);
            return $"Successfully generated rebar at {processed} of {total} identified corners.";
        }

        private string ProcessWallCornerU(UIDocument uidoc, Document doc)
        {
            var refs = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new WallSelectionFilter(),
                "Select walls forming U-corners (press Finish)");
            var walls = refs
                .Select(r => doc.GetElement(r.ElementId) as Wall)
                .Where(w => w != null)
                .ToList();

            if (walls.Count < 2) return null;

            var request = _window.WallCornerUPanel.GetRequest();
            request.RemoveExisting = _window.RemoveExisting;
            request.EnableLapSplice = _window.EnableLapSplice;
            request.DesignCode = _window.DesignCode;
            var engine = new RebarEngine(doc);
            var (processed, total) = engine.GenerateWallCornerRebar(walls, request);
            return $"Successfully generated rebar at {processed} of {total} identified corners.";
        }

        // ────────────────────────────────────────
        //  Selection Filters
        // ────────────────────────────────────────

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
