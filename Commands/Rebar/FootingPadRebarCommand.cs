using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Utilities;
using antiGGGravity.Views.Rebar;
using DBRebar = Autodesk.Revit.DB.Structure.Rebar;
using System;
using System.Collections.Generic;
using System.Linq;

using antiGGGravity.Commands;

namespace antiGGGravity.Commands.Rebar
{
    [Transaction(TransactionMode.Manual)]
    public class FootingPadRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new FootingPadRebarView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Foundations
            List<Element> foundations = new List<Element>();
            try
            {
                var selection = uidoc.Selection.GetElementIds();
                if (selection.Count > 0)
                {
                    foreach (var id in selection)
                    {
                        Element e = doc.GetElement(id);
                        if (e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation)
                            foundations.Add(e);
                    }
                }

                if (!foundations.Any())
                {
                    var refs = uidoc.Selection.PickObjects(ObjectType.Element, new FoundationSelectionFilter(), "Select Structural Foundations");
                    foundations = refs.Select(r => doc.GetElement(r.ElementId)).ToList();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (!foundations.Any()) return Result.Cancelled;

            // 3. Process
            int count = 0;
            using (Transaction t = new Transaction(doc, "Footing Pad Rebar"))
            {
                t.Start();

                foreach (var found in foundations)
                {
                    try
                    {
                        if (view.RemoveExisting)
                        {
                            DeleteExistingRebar(doc, found);
                        }

                        if (GenerateRebar(doc, found, view))
                        {
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error on foundation {found.Id}: {ex}");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Result", $"Reinforced {count} foundations.");
            return Result.Succeeded;
        }

        private void DeleteExistingRebar(Document doc, Element host)
        {
            var rebarHostData = RebarHostData.GetRebarHostData(host);
            if (rebarHostData != null)
            {
                foreach (var refRebar in rebarHostData.GetRebarsInHost())
                {
                    try { doc.Delete(new List<ElementId> { refRebar.Id }); } catch { }
                }
            }
        }

        private bool GenerateRebar(Document doc, Element found, FootingPadRebarView view)
        {
            BoundingBoxXYZ bbox = found.get_BoundingBox(null);
            if (bbox == null) return false;

            double sizeX = bbox.Max.X - bbox.Min.X;
            double sizeY = bbox.Max.Y - bbox.Min.Y;
            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;

            // Determine short edge direction
            bool shortIsX = sizeX <= sizeY;

            // Covers
            double cTop = GeometryUtils.GetCoverDistance(doc, found, BuiltInParameter.CLEAR_COVER_TOP);
            double cBot = GeometryUtils.GetCoverDistance(doc, found, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double cSide = GeometryUtils.GetCoverDistance(doc, found, BuiltInParameter.CLEAR_COVER_OTHER);

            // --- Bottom Mat (B1, B2) ---
            if (view.EnableBottom && view.BottomType != null)
            {
                RebarBarType bType = view.BottomType;
                RebarShape bShape = view.BottomShape;
                RebarHookType bHook = view.BottomHook;
                double spacing = view.BottomSpacingMM / 304.8;
                
                double dia = bType.BarModelDiameter;
                double off = cSide + (2.5 * dia);
                
                double zB1 = zMin + cBot + dia / 2;
                double zB2 = zB1 + dia;

                if (shortIsX)
                {
                    // B1: Along X (short), distributed along Y
                    CreateRebarSet(doc, found, bType, bShape, bHook,
                        new XYZ(bbox.Min.X + cSide, bbox.Min.Y + off, zB1),
                        new XYZ(bbox.Max.X - cSide, bbox.Min.Y + off, zB1),
                        XYZ.BasisY, sizeY - 2 * off, spacing);

                    // B2: Along Y (long), distributed along X
                    CreateRebarSet(doc, found, bType, bShape, bHook,
                        new XYZ(bbox.Max.X - off, bbox.Min.Y + cSide, zB2),
                        new XYZ(bbox.Max.X - off, bbox.Max.Y - cSide, zB2),
                        -XYZ.BasisX, sizeX - 2 * off, spacing);
                }
                else
                {
                    // B1: Along Y (short), distributed along X
                    CreateRebarSet(doc, found, bType, bShape, bHook,
                        new XYZ(bbox.Max.X - off, bbox.Min.Y + cSide, zB1),
                        new XYZ(bbox.Max.X - off, bbox.Max.Y - cSide, zB1),
                        -XYZ.BasisX, sizeX - 2 * off, spacing);

                    // B2: Along X (long), distributed along Y
                     CreateRebarSet(doc, found, bType, bShape, bHook,
                        new XYZ(bbox.Min.X + cSide, bbox.Min.Y + off, zB2),
                        new XYZ(bbox.Max.X - cSide, bbox.Min.Y + off, zB2),
                        XYZ.BasisY, sizeY - 2 * off, spacing);
                }
            }

            // --- Top Mat (T1, T2) ---
            if (view.EnableTop && view.TopType != null)
            {
                RebarBarType tType = view.TopType;
                RebarShape tShape = view.TopShape;
                RebarHookType tHook = view.TopHook;
                double spacing = view.TopSpacingMM / 304.8;

                double dia = tType.BarModelDiameter;
                double off = cSide + (2.5 * dia);
                
                double zT1 = zMax - cTop - dia / 2;
                double zT2 = zT1 - dia;

                if (shortIsX)
                {
                    // T1: Along X (short), distributed along Y
                    CreateRebarSet(doc, found, tType, tShape, tHook,
                        new XYZ(bbox.Min.X + cSide, bbox.Max.Y - off, zT1),
                        new XYZ(bbox.Max.X - cSide, bbox.Max.Y - off, zT1),
                        -XYZ.BasisY, sizeY - 2 * off, spacing);

                    // T2: Along Y (long), distributed along X
                    CreateRebarSet(doc, found, tType, tShape, tHook,
                        new XYZ(bbox.Min.X + off, bbox.Min.Y + cSide, zT2),
                        new XYZ(bbox.Min.X + off, bbox.Max.Y - cSide, zT2),
                        XYZ.BasisX, sizeX - 2 * off, spacing);
                }
                else
                {
                    // T1: Along Y (short), distributed along X
                    CreateRebarSet(doc, found, tType, tShape, tHook,
                        new XYZ(bbox.Min.X + off, bbox.Min.Y + cSide, zT1),
                        new XYZ(bbox.Min.X + off, bbox.Max.Y - cSide, zT1),
                        XYZ.BasisX, sizeX - 2 * off, spacing);

                    // T2: Along X (long), distributed along Y
                    CreateRebarSet(doc, found, tType, tShape, tHook,
                        new XYZ(bbox.Min.X + cSide, bbox.Max.Y - off, zT2),
                        new XYZ(bbox.Max.X - cSide, bbox.Max.Y - off, zT2),
                        -XYZ.BasisY, sizeY - 2 * off, spacing);
                }
            }

            return true;
        }

        private void CreateRebarSet(Document doc, Element host, RebarBarType type, RebarShape shape, RebarHookType hook, XYZ p1, XYZ p2, XYZ distDir, double distLen, double spacing)
        {
            Line line = Line.CreateBound(p1, p2);
            DBRebar rebar = DBRebar.CreateFromCurves(
                doc, RebarStyle.Standard, type, hook, hook,
                host, distDir, new List<Curve> { line },
                RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            
            if (rebar != null)
            {
                // if (shape != null)
                // {
                //    try { rebar.RebarShapeId = shape.Id; } catch { }
                // }
                
                var accessor = rebar.GetShapeDrivenAccessor();
                accessor.SetLayoutAsMaximumSpacing(spacing, distLen, true, true, true);
                
                if (doc.ActiveView is View3D v3d)
                    rebar.SetUnobscuredInView(v3d, true);
            }
        }
    }

    public class FoundationSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
