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
    public class MergeRegionsCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selIds = uidoc.Selection.GetElementIds();
            var regions = selIds.Select(id => doc.GetElement(id)).OfType<FilledRegion>().ToList();

            if (regions.Count < 2)
            {
                try { regions = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilter_FilledRegions(), "Select Filled Regions to merge").Select(r => doc.GetElement(r)).OfType<FilledRegion>().ToList(); }
                catch { return Result.Cancelled; }
            }

            if (regions.Count < 2) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Merge Filled Regions"))
            {
                t.Start();
                try
                {
                    List<Solid> solids = new List<Solid>();
                    foreach (var region in regions)
                    {
                        var boundaries = region.GetBoundaries();
                        if (boundaries.Count == 0) continue;
                        
                        // Create 1-foot high extrusion for boolean operations
                        Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(boundaries, XYZ.BasisZ, 1.0);
                        if (solid != null) solids.Add(solid);
                    }

                    if (solids.Count < 2) return Result.Failed;

                    // Boolean Union
                    Solid combinedSolid = solids[0];
                    for (int i = 1; i < solids.Count; i++)
                    {
                        combinedSolid = BooleanOperationsUtils.ExecuteBooleanOperation(combinedSolid, solids[i], BooleanOperationsType.Union);
                    }

                    // Extract Top Faces (Normal == BasisZ)
                    List<CurveLoop> finalLoops = new List<CurveLoop>();
                    foreach (Face face in combinedSolid.Faces)
                    {
                        if (face is PlanarFace planar && planar.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                        {
                            finalLoops.AddRange(face.GetEdgesAsCurveLoops());
                        }
                    }

                    if (finalLoops.Count > 0)
                    {
                        FilledRegion first = regions.First();
                        FilledRegion.Create(doc, first.GetTypeId(), doc.ActiveView.Id, finalLoops);
                        doc.Delete(regions.Select(r => r.Id).ToList());
                    }
                }
                catch (Exception ex) 
                { 
                    TaskDialog.Show("Merge Error", "Could not merge regions. Ensure they are on the same plane and overlap correctly.\n\n" + ex.Message);
                    return Result.Failed; 
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RegionChangeLineStyleCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var regions = uidoc.Selection.GetElementIds().Select(id => doc.GetElement(id)).OfType<FilledRegion>().ToList();
            if (!regions.Any())
            {
                try { regions = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilter_FilledRegions(), "Select Filled Regions").Select(r => doc.GetElement(r)).OfType<FilledRegion>().ToList(); }
                catch { return Result.Cancelled; }
            }

            if (!regions.Any()) return Result.Cancelled;

            // Get valid styles
            var styleIds = FilledRegion.GetValidLineStyleIdsForFilledRegion(doc);
            var styles = styleIds.Select(id => doc.GetElement(id)).OfType<GraphicsStyle>().ToList();

            LineStyleSelectionView win = new LineStyleSelectionView(styles);
            if (win.ShowDialog() != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Change Region LineStyle"))
            {
                t.Start();
                foreach (var region in regions)
                {
                    try { region.SetLineStyleId(win.SelectedStyle.Id); } catch { }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RegionsToFloorsCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var regions = uidoc.Selection.GetElementIds().Select(id => doc.GetElement(id)).OfType<FilledRegion>().ToList();
            if (!regions.Any())
            {
                try { regions = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilter_FilledRegions(), "Select regions to convert to Floors").Select(r => doc.GetElement(r)).OfType<FilledRegion>().ToList(); }
                catch { return Result.Cancelled; }
            }

            if (!regions.Any()) return Result.Cancelled;

            var floorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
            GeometricConversionView win = new GeometricConversionView(doc, "Regions to Floors", "Select Floor Type:", floorTypes);
            if (win.ShowDialog() != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Convert Regions to Floors"))
            {
                t.Start();
                var newIds = new List<ElementId>();
                foreach (var region in regions)
                {
                    try
                    {
                        var loops = region.GetBoundaries().ToList();
                        Floor floor = Floor.Create(doc, loops, win.SelectedType.Id, win.SelectedLevel.Id);
                        floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(win.Offset);
                        newIds.Add(floor.Id);
                    }
                    catch { }
                }
                t.Commit();
                uidoc.Selection.SetElementIds(newIds);
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RegionsToCeilingsCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var regions = uidoc.Selection.GetElementIds().Select(id => doc.GetElement(id)).OfType<FilledRegion>().ToList();
            if (!regions.Any())
            {
                try { regions = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilter_FilledRegions(), "Select regions to convert to Ceilings").Select(r => doc.GetElement(r)).OfType<FilledRegion>().ToList(); }
                catch { return Result.Cancelled; }
            }

            if (!regions.Any()) return Result.Cancelled;

            var ceilTypes = new FilteredElementCollector(doc).OfClass(typeof(CeilingType)).Cast<CeilingType>().ToList();
            GeometricConversionView win = new GeometricConversionView(doc, "Regions to Ceilings", "Select Ceiling Type:", ceilTypes);
            if (win.ShowDialog() != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Convert Regions to Ceilings"))
            {
                t.Start();
                var newIds = new List<ElementId>();
                foreach (var region in regions)
                {
                    try
                    {
                        var loops = region.GetBoundaries().ToList();
                        Ceiling ceiling = Ceiling.Create(doc, loops, win.SelectedType.Id, win.SelectedLevel.Id);
                        ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(win.Offset);
                        newIds.Add(ceiling.Id);
                    }
                    catch { }
                }
                t.Commit();
                uidoc.Selection.SetElementIds(newIds);
            }
            return Result.Succeeded;
        }
    }

    public class SelectionFilter_FilledRegions : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is FilledRegion;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
