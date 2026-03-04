using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.Model;

namespace antiGGGravity.Commands.Model
{
    public abstract class BracingCommandBase : IExternalCommand
    {
        public abstract Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements);

        protected FamilySymbol SelectBeamType(Document doc, Selection selection)
        {
            try
            {
                Reference selectedRef = selection.PickObject(ObjectType.Element, "Select a structural framing element (beam) to use its type for the brace");
                Element selectedElement = doc.GetElement(selectedRef);

                if (selectedElement.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming)
                {
                    if (selectedElement is FamilyInstance fi)
                        return fi.Symbol;
                }

                TaskDialog.Show("Error", "Selected element is not a structural framing element.");
                return null;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "An error occurred during family selection: " + ex.Message);
                return null;
            }
        }

        protected List<Element> SelectStructuralElements(Document doc, Selection selection, int count)
        {
            try
            {
                IList<Reference> selectedRefs = selection.PickObjects(ObjectType.Element, $"Select {count} structural elements (columns or beams)");

                if (selectedRefs.Count != count)
                {
                    TaskDialog.Show("Error", $"Please select exactly {count} structural elements.");
                    return null;
                }

                List<Element> elements = selectedRefs.Select(r => doc.GetElement(r)).ToList();

                foreach (var element in elements)
                {
                    long catId = element.Category.Id.Value;
                    if (catId != (long)BuiltInCategory.OST_StructuralFraming && catId != (long)BuiltInCategory.OST_StructuralColumns)
                    {
                        TaskDialog.Show("Error", "Selected elements must be structural framing or structural columns.");
                        return null;
                    }
                }

                return elements;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        protected Level GetElementLevel(Element element)
        {
            // Try INSTANCE_REFERENCE_LEVEL_PARAM first (works for beams)
            Parameter param = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (param != null && param.HasValue)
            {
                Level level = element.Document.GetElement(param.AsElementId()) as Level;
                if (level != null) return level;
            }
            // Fallback: try FAMILY_BASE_LEVEL_PARAM (works for columns)
            param = element.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
            if (param != null && param.HasValue)
            {
                Level level = element.Document.GetElement(param.AsElementId()) as Level;
                if (level != null) return level;
            }
            // Last resort: find the nearest level below the element
            if (element.Location is LocationPoint lp)
            {
                return new FilteredElementCollector(element.Document)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Where(l => l.Elevation <= lp.Point.Z + 1.0)
                    .OrderByDescending(l => l.Elevation)
                    .FirstOrDefault();
            }
            return null;
        }

        protected (XYZ Start, XYZ End) GetVerticalColumnPoints(Element element)
        {
            LocationPoint locPoint = element.Location as LocationPoint;
            if (locPoint == null) return (null, null);

            double x = locPoint.Point.X;
            double y = locPoint.Point.Y;

            // Step 1: Default to LocationPoint Z just in case
            double minZ = locPoint.Point.Z;
            double maxZ = locPoint.Point.Z;

            // Step 2: Extract real solid geometry to find the exact bottom and top Z
            Options opt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElem = element.get_Geometry(opt);
            if (geomElem != null)
            {
                bool pointsFound = false;
                foreach (GeometryObject geomObj in geomElem)
                {
                    Solid solid = null;
                    if (geomObj is Solid s && s.Faces.Size > 0 && s.Volume > 0)
                    {
                        solid = s;
                    }
                    else if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Solid s2 && s2.Faces.Size > 0 && s2.Volume > 0)
                            {
                                // We'll just check all valid solids
                                foreach (Face face in s2.Faces)
                                {
                                    Mesh mesh = face.Triangulate();
                                    foreach (XYZ v in mesh.Vertices)
                                    {
                                        if (!pointsFound)
                                        {
                                            minZ = v.Z;
                                            maxZ = v.Z;
                                            pointsFound = true;
                                        }
                                        else
                                        {
                                            if (v.Z < minZ) minZ = v.Z;
                                            if (v.Z > maxZ) maxZ = v.Z;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (solid != null)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            Mesh mesh = face.Triangulate();
                            foreach (XYZ v in mesh.Vertices)
                            {
                                if (!pointsFound)
                                {
                                    minZ = v.Z;
                                    maxZ = v.Z;
                                    pointsFound = true;
                                }
                                else
                                {
                                    if (v.Z < minZ) minZ = v.Z;
                                    if (v.Z > maxZ) maxZ = v.Z;
                                }
                            }
                        }
                    }
                }
            }

            // Step 3: Handle X,Y offset checks
            BoundingBoxXYZ bb = element.get_BoundingBox(null);
            if (bb != null)
            {
                double bbCenterX = (bb.Min.X + bb.Max.X) / 2;
                double bbCenterY = (bb.Min.Y + bb.Max.Y) / 2;

                if (System.Math.Abs(x - bbCenterX) > 10 || System.Math.Abs(y - bbCenterY) > 10)
                {
                    x = bbCenterX;
                    y = bbCenterY;
                }
            }

            XYZ bottomPoint = new XYZ(x, y, minZ);
            XYZ topPoint = new XYZ(x, y, maxZ);
            return (bottomPoint, topPoint);
        }

        protected (XYZ Start, XYZ End) GetElementPoints(Element element)
        {
            Location location = element.Location;
            if (location is LocationCurve locCurve)
            {
                return (locCurve.Curve.GetEndPoint(0), locCurve.Curve.GetEndPoint(1));
            }
            if (location is LocationPoint)
            {
                // Handle vertical columns by computing actual top/bottom points
                if (element.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                    return GetVerticalColumnPoints(element);

                return (((LocationPoint)location).Point, ((LocationPoint)location).Point);
            }
            return (null, null);
        }

        protected (XYZ Start, XYZ End) GetElementPointsWithOffset(Element element, double offsetFeet)
        {
            Location location = element.Location;
            if (location is LocationCurve locCurve)
            {
                XYZ start = locCurve.Curve.GetEndPoint(0);
                XYZ end = locCurve.Curve.GetEndPoint(1);
                XYZ dir = (end - start).Normalize();

                return (start + dir * offsetFeet, end - dir * offsetFeet);
            }
            if (location is LocationPoint)
            {
                // Handle vertical columns by computing actual top/bottom points with offset
                if (element.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                {
                    var (bottom, top) = GetVerticalColumnPoints(element);
                    if (bottom != null && top != null)
                    {
                        XYZ dir = (top - bottom).Normalize();
                        return (bottom + dir * offsetFeet, top - dir * offsetFeet);
                    }
                }

                XYZ pt = ((LocationPoint)location).Point;
                return (pt, pt);
            }
            return (null, null);
        }

        protected FamilyInstance CreateBeam(Document doc, Line line, FamilySymbol beamType, Level level)
        {
            if (!beamType.IsActive)
            {
                beamType.Activate();
                doc.Regenerate();
            }
            return doc.Create.NewFamilyInstance(line, beamType, level, StructuralType.Beam);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class HFrameCommand : BracingCommandBase
    {
        public override Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            FamilySymbol beamType = SelectBeamType(doc, uidoc.Selection);
            if (beamType == null) return Result.Cancelled;

            List<Element> selectedElements = SelectStructuralElements(doc, uidoc.Selection, 2);
            if (selectedElements == null) return Result.Cancelled;

            var view = new BracingParametersView(doc, "H-Frame");
            if (view.ShowDialog() != true) return Result.Cancelled;

            double offsetFeet = UnitUtils.ConvertToInternalUnits(view.OffsetMm, UnitTypeId.Millimeters);
            int numBraces = view.NumBraces;

            var pointsA = GetElementPointsWithOffset(selectedElements[0], offsetFeet);
            var pointsB = GetElementPointsWithOffset(selectedElements[1], offsetFeet);

            XYZ pA1 = pointsA.Start;
            XYZ pA2 = pointsA.End;
            XYZ pB1 = pointsB.Start;
            XYZ pB2 = pointsB.End;

            // Sort B points so B1 is closest to A1
            if (pB2.DistanceTo(pA1) < pB1.DistanceTo(pA1))
            {
                XYZ temp = pB1;
                pB1 = pB2;
                pB2 = temp;
            }

            Level level = GetElementLevel(selectedElements[0]);

            using (Transaction t = new Transaction(doc, "Create H-Frame"))
            {
                t.Start();
                if (numBraces == 1)
                {
                    XYZ midA = pA1 + (pA2 - pA1) * 0.5;
                    XYZ midB = pB1 + (pB2 - pB1) * 0.5;
                    CreateBeam(doc, Line.CreateBound(midA, midB), beamType, level);
                }
                else
                {
                    CreateBeam(doc, Line.CreateBound(pA1, pB1), beamType, level);
                    CreateBeam(doc, Line.CreateBound(pA2, pB2), beamType, level);

                    if (numBraces > 2)
                    {
                        for (int i = 0; i < numBraces - 2; i++)
                        {
                            double fraction = (i + 1.0) / (numBraces - 1.0);
                            XYZ midA = pA1 + (pA2 - pA1) * fraction;
                            XYZ midB = pB1 + (pB2 - pB1) * fraction;
                            CreateBeam(doc, Line.CreateBound(midA, midB), beamType, level);
                        }
                    }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class KBraceCommand : BracingCommandBase
    {
        public override Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            FamilySymbol beamType = SelectBeamType(doc, uidoc.Selection);
            if (beamType == null) return Result.Cancelled;

            List<Element> selectedElements = SelectStructuralElements(doc, uidoc.Selection, 2);
            if (selectedElements == null) return Result.Cancelled;

            var view = new BracingParametersView(doc, "K-Brace");
            if (view.ShowDialog() != true) return Result.Cancelled;

            double offsetFeet = UnitUtils.ConvertToInternalUnits(view.OffsetMm, UnitTypeId.Millimeters);
            
            // Re-select apex element via prompt vs just picking from the two?
            // User script uses PickObject for apex.
            Element apexElement = null;
            try 
            {
                Reference apexRef = uidoc.Selection.PickObject(ObjectType.Element, "Select which element will be the V-point (apex)");
                apexElement = doc.GetElement(apexRef);
                if (apexElement.Id != selectedElements[0].Id && apexElement.Id != selectedElements[1].Id)
                {
                    TaskDialog.Show("Error", "Selected apex must be one of the two elements selected.");
                    return Result.Failed;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            Element otherElement = (apexElement.Id == selectedElements[0].Id) ? selectedElements[1] : selectedElements[0];

            var apexPoints = GetElementPoints(apexElement);
            XYZ apex = (apexPoints.Start + apexPoints.End) * 0.5;

            var otherPoints = GetElementPointsWithOffset(otherElement, offsetFeet);
            XYZ end1 = otherPoints.Start;
            XYZ end2 = otherPoints.End;

            Level level = GetElementLevel(apexElement);

            using (Transaction t = new Transaction(doc, "Create K-Brace"))
            {
                t.Start();
                CreateBeam(doc, Line.CreateBound(apex, end1), beamType, level);
                CreateBeam(doc, Line.CreateBound(apex, end2), beamType, level);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class XBraceCommand : BracingCommandBase
    {
        public override Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            FamilySymbol beamType = SelectBeamType(doc, uidoc.Selection);
            if (beamType == null) return Result.Cancelled;

            List<Element> selectedElements = SelectStructuralElements(doc, uidoc.Selection, 2);
            if (selectedElements == null) return Result.Cancelled;

            var view = new BracingParametersView(doc, "X-Brace");
            if (view.ShowDialog() != true) return Result.Cancelled;

            double offsetFeet = UnitUtils.ConvertToInternalUnits(view.OffsetMm, UnitTypeId.Millimeters);

            var pointsA = GetElementPointsWithOffset(selectedElements[0], offsetFeet);
            var pointsB = GetElementPointsWithOffset(selectedElements[1], offsetFeet);

            XYZ pA1 = pointsA.Start;
            XYZ pA2 = pointsA.End;
            XYZ pB1 = pointsB.Start;
            XYZ pB2 = pointsB.End;

            if (pB2.DistanceTo(pA1) < pB1.DistanceTo(pA1))
            {
                XYZ temp = pB1;
                pB1 = pB2;
                pB2 = temp;
            }

            Level level = GetElementLevel(selectedElements[0]);

            using (Transaction t = new Transaction(doc, "Create X-Brace"))
            {
                t.Start();
                CreateBeam(doc, Line.CreateBound(pA1, pB2), beamType, level);
                CreateBeam(doc, Line.CreateBound(pA2, pB1), beamType, level);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
