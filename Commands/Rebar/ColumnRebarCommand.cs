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
    public class ColumnRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Show UI
            var view = new ColumnRebarView(doc);
            view.ShowDialog();

            if (!view.IsConfirmed) return Result.Cancelled;

            // 2. Select Columns
            List<Element> columns = new List<Element>();
            try
            {
                // Pre-selection check (optional) or just prompt
                var selection = uidoc.Selection.GetElementIds();
                if (selection.Count > 0)
                {
                    foreach (var id in selection)
                    {
                        Element e = doc.GetElement(id);
                        if (e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                            columns.Add(e);
                    }
                }

                if (!columns.Any())
                {
                    var refs = uidoc.Selection.PickObjects(ObjectType.Element, new ColumnSelectionFilter(), "Select Structural Columns");
                    columns = refs.Select(r => doc.GetElement(r.ElementId)).ToList();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (!columns.Any()) return Result.Cancelled;

            // 3. Process
            int count = 0;
            using (Transaction t = new Transaction(doc, "Column Rebar"))
            {
                t.Start();

                foreach (var col in columns)
                {
                    try
                    {
                        if (view.RemoveExisting)
                        {
                            DeleteExistingRebar(doc, col);
                        }

                        if (GenerateRebar(doc, col, view))
                        {
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error on column {col.Id}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Result", $"Reinforced {count} columns.");
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

        private bool GenerateRebar(Document doc, Element col, ColumnRebarView view)
        {
            // Geometry Analysis
            BoundingBoxXYZ bbox = col.get_BoundingBox(null);
            if (bbox == null) return false;

            double totalHeight = bbox.Max.Z - bbox.Min.Z;

            Transform trans = null;
            if (col is FamilyInstance fi)
            {
                trans = fi.GetTransform();
            }
            // If not family instance (e.g. system family?), try to get transform from geometry?
            // Columns are usually FamilyInstance.
            if (trans == null) return false;

            XYZ basisX = trans.BasisX.Normalize();
            XYZ basisY = trans.BasisY.Normalize();
            XYZ basisZ = trans.BasisZ.Normalize();

            // Global Z bounds
            double zMin = bbox.Min.Z;
            double zMax = bbox.Max.Z;

            // Origin
            XYZ colOrigin = trans.Origin;
            XYZ bottomOrigin;
            if (Math.Abs(basisZ.Z) > 0.001)
            {
                double distToBottom = colOrigin.Z - zMin;
                bottomOrigin = colOrigin - basisZ * (distToBottom / basisZ.Z);
            }
            else
            {
                bottomOrigin = colOrigin;
            }

            // Dimensions
            double width = GetParameter(col, "Width", "b") ?? (bbox.Max.X - bbox.Min.X);
            double depth = GetParameter(col, "Depth", "h") ?? (bbox.Max.Y - bbox.Min.Y);

            // Cover
            double cSide = GeometryUtils.GetCoverDistance(doc, col, BuiltInParameter.CLEAR_COVER_OTHER);

            // Inputs
            RebarBarType vType = view.VerticalType;
            RebarBarType tType = view.TieType;
            if (vType == null || tType == null) return false;

            double vDia = vType.BarModelDiameter;
            double tDia = tType.BarModelDiameter;

            // --- 1. Ties ---
            double wTie = width - 2 * cSide;
            double dTie = depth - 2 * cSide;

            double tieOffBot = view.TieBotOffsetMM / 304.8;
            XYZ tieOrigin = bottomOrigin + basisZ * tieOffBot;

            // Points in Local Basis
            XYZ p1 = tieOrigin - basisX * (wTie / 2) - basisY * (dTie / 2);
            XYZ p2 = tieOrigin + basisX * (wTie / 2) - basisY * (dTie / 2);
            XYZ p3 = tieOrigin + basisX * (wTie / 2) + basisY * (dTie / 2);
            XYZ p4 = tieOrigin - basisX * (wTie / 2) + basisY * (dTie / 2);

            List<Curve> curves = new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            DBRebar tieRebar = DBRebar.CreateFromCurves(
                doc, RebarStyle.StirrupTie, tType, view.TieHookStart, view.TieHookEnd,
                col, basisZ, curves,
                RebarHookOrientation.Left, RebarHookOrientation.Left, true, true);

            if (tieRebar != null)
            {
                // try { tieRebar.RebarShapeId = view.TieShape.Id; } catch { }

                var accessor = tieRebar.GetShapeDrivenAccessor();
                double tieOffTop = view.TieTopOffsetMM / 304.8;
                double tieLen = totalHeight - tieOffBot - tieOffTop;

                if (tieLen > 0)
                {
                    double spacing = view.TieSpacingMM / 304.8;
                    accessor.SetLayoutAsMaximumSpacing(spacing, tieLen, true, true, true);
                }
                
                if (doc.ActiveView is View3D v3d)
                    tieRebar.SetUnobscuredInView(v3d, true); // Keep implicit 'Unobscured' check
            }

            // --- 2. Vertical Bars ---
            double innerOff = cSide + tDia + vDia / 2;
            int nx = view.CountX;
            int ny = view.CountY;

            List<double> xPts = new List<double>();
            if (nx > 1)
            {
                double stepX = (width - 2 * innerOff) / (nx - 1);
                for (int i = 0; i < nx; i++) xPts.Add(-width / 2 + innerOff + i * stepX);
            }
            else xPts.Add(0);

            List<double> yPts = new List<double>();
            if (ny > 1)
            {
                double stepY = (depth - 2 * innerOff) / (ny - 1);
                for (int i = 0; i < ny; i++) yPts.Add(-depth / 2 + innerOff + i * stepY);
            }
            else yPts.Add(0);

            double topExt = view.EnableTopExt ? view.TopExtMM / 304.8 : 0;
            double botExt = view.EnableBotExt ? view.BotExtMM / 304.8 : 0; // Negative handled in logic??
            // Logic usually says: cover - botExt. If botExt is "Extension", maybe it means "Extend downwards"?
            // Python: pos + basisZ * (cover - botExt)
            // If botExt positive, it lowers the start point.

            double coverBot = GeometryUtils.GetCoverDistance(doc, col, BuiltInParameter.CLEAR_COVER_BOTTOM);
            double coverTop = GeometryUtils.GetCoverDistance(doc, col, BuiltInParameter.CLEAR_COVER_TOP);
            
            // Python used hardcoded cover logic for vertical bars?
            // "cover = 0.04" (Side cover) used for vertical bars positioning.
            // But Top/Bot limits: 
            // Start: pos + basisZ * (cover - botExt)
            // End: pos + basisZ * (totalHeight - cover + topExt)

            for (int ix = 0; ix < xPts.Count; ix++)
            {
                for (int iy = 0; iy < yPts.Count; iy++)
                {
                    bool isXEdge = (ix == 0 || ix == xPts.Count - 1);
                    bool isYEdge = (iy == 0 || iy == yPts.Count - 1);

                    if (isXEdge || isYEdge)
                    {
                        double x = xPts[ix];
                        double y = yPts[iy];

                        XYZ outDir;
                        if (isXEdge && isYEdge)
                            outDir = (basisX * (x > 0 ? 1 : -1) + basisY * (y > 0 ? 1 : -1)).Normalize();
                        else if (isXEdge)
                            outDir = basisX * (x > 0 ? 1 : -1);
                        else
                            outDir = basisY * (y > 0 ? 1 : -1);

                        XYZ hookNormal = basisZ.CrossProduct(outDir);

                        XYZ pos = bottomOrigin + basisX * x + basisY * y;
                        XYZ pStart = pos + basisZ * (cSide - botExt); // Using Side cover as default logic per script
                        XYZ pEnd = pos + basisZ * (totalHeight - cSide + topExt);

                        Line vLine = Line.CreateBound(pStart, pEnd);

                        RebarHookOrientation orientB = view.VHookBotOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;
                        RebarHookOrientation orientT = view.VHookTopOut ? RebarHookOrientation.Left : RebarHookOrientation.Right;

                        DBRebar vRebar = DBRebar.CreateFromCurves(
                            doc, RebarStyle.Standard, vType, view.VHookBot, view.VHookTop,
                            col, hookNormal, new List<Curve> { vLine },
                            orientB, orientT, true, true);

                        if (vRebar != null)
                        {
                            // try { vRebar.RebarShapeId = view.VerticalShape.Id; } catch { }
                             if (doc.ActiveView is View3D v3d)
                                vRebar.SetUnobscuredInView(v3d, true);
                        }
                    }
                }
            }

            return true;
        }

        private double? GetParameter(Element e, params string[] names)
        {
            foreach (var name in names)
            {
                Parameter p = e.LookupParameter(name) ?? (e as FamilyInstance)?.Symbol?.LookupParameter(name);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            return null;
        }
    }

    public class ColumnSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
