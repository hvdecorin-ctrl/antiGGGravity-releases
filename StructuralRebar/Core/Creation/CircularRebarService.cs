using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.Core.Geometry;
using System;
using System.Collections.Generic;

namespace antiGGGravity.StructuralRebar.Core.Creation
{
    public static class CircularRebarService
    {
        public static Rebar CreateCircularTie(
            Document doc,
            Element host,
            XYZ center,
            double radius,
            double elevation,
            RebarBarType barType,
            RebarShape shape = null)
        {
            XYZ origin = new XYZ(center.X, center.Y, elevation);
            Rebar tie = null;

            if (shape != null)
            {
                try
                {
                    tie = Rebar.CreateFromRebarShape(doc, shape, barType, host, origin, XYZ.BasisX, XYZ.BasisY);
                    if (tie != null)
                    {
                        try
                        {
                            string[] diaNames = { "Geometry Dia", "Diameter", "Dia", "A", "B", "C", "D", "E", "F", "O" };
                            foreach (var pName in diaNames)
                            {
                                var p = tie.LookupParameter(pName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(2 * radius); break; }
                            }

                            string[] radNames = { "Geometry Rad", "Radius", "Rad", "R" };
                            foreach (var pName in radNames)
                            {
                                var p = tie.LookupParameter(pName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(radius); break; }
                            }
                        } catch { }

                        try
                        {
                            doc.Regenerate();
                            var drivingCurves = tie.GetShapeDrivenAccessor().ComputeDrivingCurves();
                            if (drivingCurves != null && drivingCurves.Count > 0)
                            {
                                XYZ rebarCenter = null;
                                foreach (var curve in drivingCurves)
                                {
                                    if (curve is Arc arc && (rebarCenter == null || arc.Length > Math.PI * radius / 2))
                                    {
                                        rebarCenter = arc.Center;
                                    }
                                }

                                if (rebarCenter != null)
                                {
                                    XYZ translation = new XYZ(origin.X - rebarCenter.X, origin.Y - rebarCenter.Y, 0);
                                    if (translation.GetLength() > 0.001) ElementTransformUtils.MoveElement(doc, tie.Id, translation);
                                }
                            }
                        } catch { }
                        try
                        {
                            Parameter pComment = tie.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (pComment != null && !pComment.IsReadOnly) pComment.Set("Stirrup");
                        } catch { }
                    }
                } catch { }
            }

            if (tie == null)
            {
                try
                {
                    Arc circle = Arc.Create(origin, radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
                    tie = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, barType, null, null, host, XYZ.BasisZ, new List<Curve> { circle }, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                } catch { }
            }

            return tie;
        }

        public static Rebar CreateSpiralFromRing(
            Document doc,
            Element host,
            XYZ center,
            double radius,
            double bottom,
            double top,
            RebarBarType tieBarType,
            double pitch,
            RebarShape shape = null)
        {
            double height = top - bottom;
            XYZ origin = new XYZ(center.X, center.Y, bottom);
            Rebar spiral = null;

            if (shape != null)
            {
                try
                {
                    spiral = Rebar.CreateFromRebarShape(doc, shape, tieBarType, host, origin, XYZ.BasisX, XYZ.BasisY);
                    if (spiral != null)
                    {
                        try
                        {
                            string[] diaNames = { "Geometry Dia", "Diameter", "Dia", "A", "B", "C", "O" };
                            foreach (var pName in diaNames)
                            {
                                var p = spiral.LookupParameter(pName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(2 * radius); break; }
                            }

                            string[] radNames = { "Geometry Rad", "Radius", "Rad", "R" };
                            foreach (var pName in radNames)
                            {
                                var p = spiral.LookupParameter(pName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(radius); break; }
                            }
                            
                            string[] hNames = { "Height", "H", "Length", "L", "Pitch Height", "Spiral Height", "D", "E" };
                            foreach (var hName in hNames)
                            {
                                var p = spiral.LookupParameter(hName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(height); break; }
                            }

                            string[] pNames = { "Pitch", "P" };
                            foreach (var pName in pNames)
                            {
                                var p = spiral.LookupParameter(pName);
                                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(pitch); break; }
                            }
                        } catch { }

                        try
                        {
                            doc.Regenerate();
                            var drivingCurves = spiral.GetShapeDrivenAccessor().ComputeDrivingCurves();
                            if (drivingCurves != null && drivingCurves.Count > 0)
                            {
                                XYZ rebarCenter = null;
                                foreach (var curve in drivingCurves)
                                {
                                    if (curve is Arc arc && (rebarCenter == null || arc.Length > Math.PI * radius / 2))
                                    {
                                        rebarCenter = arc.Center;
                                    }
                                }

                                if (rebarCenter != null)
                                {
                                    XYZ translation = new XYZ(origin.X - rebarCenter.X, origin.Y - rebarCenter.Y, 0);
                                    if (translation.GetLength() > 0.001) ElementTransformUtils.MoveElement(doc, spiral.Id, translation);
                                }
                            }
                        } catch { }
                        try
                        {
                            Parameter pComment = spiral.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (pComment != null && !pComment.IsReadOnly) pComment.Set("Stirrup");
                        } catch { }
                    }
                } catch { }
            }

            if (spiral == null)
            {
                try
                {
                    Arc circle = Arc.Create(origin, radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
                    spiral = Rebar.CreateFromCurves(doc, RebarStyle.StirrupTie, tieBarType, null, null, host, XYZ.BasisZ, new List<Curve> { circle }, RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);
                } catch { }
            }

            if (spiral != null)
            {
                spiral.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(pitch, height, true, true, true);
            }

            return spiral;
        }
    }
}
