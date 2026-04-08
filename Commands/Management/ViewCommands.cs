using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;
using antiGGGravity.Views.Views;

namespace antiGGGravity.Commands.Management
{
    // ===================================================================================
    // SET CROP VIEW
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class SetCropCommand : BaseCommand
    {

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (!IsValidView(activeView))
            {
                TaskDialog.Show("antiGGGravity", "View does not support cropping.");
                return Result.Cancelled;
            }

            var selectedIds = uidoc.Selection.GetElementIds();
            dynamic result = null;
            bool isDrawn = false;

            if (selectedIds.Count > 0)
            {
                result = selectedIds.Select(id => doc.GetElement(id)).ToList();
            }
            else
            {
                SetCropView window = new SetCropView(uidoc);
                window.ShowDialog();
                if (!window.IsConfirmed && window.Result == null) return Result.Cancelled;
                
                result = window.Result;
                isDrawn = window.IsDrawn;
            }

            if (result == null) return Result.Cancelled;

            bool isLegend = activeView.ViewType == ViewType.Legend;

            using (Transaction t = new Transaction(doc, "Modify Crop"))
            {
                t.Start();
                try
                {
                    if (result is string s && s == "RESET")
                    {
                        if (isLegend)
                        {
                            var mgr = activeView.GetCropRegionShapeManager();
                            if (mgr != null)
                            {
                                try { mgr.RemoveCropRegionShape(); } catch { }
                            }
                            try { activeView.CropBoxActive = false; } catch { }
                        }
                        else
                        {
                            ResetCrop(activeView);
                        }
                        t.Commit();
                        return Result.Succeeded;
                    }

                    List<Curve> allCurves = isDrawn ? (List<Curve>)result : new List<Curve>();
                    if (!isDrawn)
                    {
                        foreach (Element elem in (List<Element>)result)
                        {
                            allCurves.AddRange(GetCurvesFromElement(elem, doc));
                        }
                    }

                    if (allCurves.Count == 0)
                    {
                        TaskDialog.Show("antiGGGravity", "No valid boundary found.");
                        t.RollBack();
                        return Result.Failed;
                    }

                    CurveLoop loop = CreateCurveLoop(allCurves);
                    if (loop == null)
                    {
                        t.RollBack();
                        return Result.Failed;
                    }

                    try
                    {
                        activeView.CropBoxActive = true;
                        activeView.CropBoxVisible = true;
                    }
                    catch { }

                    bool success = false;
                    if (!isLegend)
                    {
                        success = ApplyCrop(activeView, loop, allCurves);
                    }
                    else
                    {
                        var mgr = activeView.GetCropRegionShapeManager();
                        if (mgr != null)
                        {
                            try { mgr.SetCropShape(loop); success = true; } catch { }
                        }
                        if (!success)
                        {
                            BoundingBoxXYZ bbox = GetBBox(allCurves);
                            if (bbox != null)
                            {
                                try { activeView.CropBox = bbox; success = true; } catch { }
                            }
                        }
                    }


                    if (success && !isDrawn)
                    {
                        foreach (Element e in (List<Element>)result)
                        {
                            try { if (e.IsValidObject) doc.Delete(e.Id); } catch { }
                        }
                    }

                    t.Commit();
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    message = ex.Message;
                    return Result.Failed;
                }
            }
        }

        private bool IsValidView(View view)
        {
            if (view.ViewType == ViewType.Legend) return true;
            // Check if CropBoxActive property exists/settable
            try { var x = view.CropBoxActive; return true; } catch { return false; }
        }

        private List<Curve> GetCurvesFromElement(Element element, Document doc)
        {
            List<Curve> curves = new List<Curve>();
            if (element is CurveElement ce && ce.GeometryCurve != null)
            {
                curves.Add(ce.GeometryCurve);
            }
            else if (element is FilledRegion fr)
            {
                foreach (var loop in fr.GetBoundaries())
                {
                    foreach (Curve c in loop) curves.Add(c);
                }
            }
            else
            {
                // Try to get dependent curve elements (like lines and filled regions)
                try
                {
                    var depIds = element.GetDependentElements(new ElementClassFilter(typeof(CurveElement)));
                    foreach (var depId in depIds)
                    {
                        var ceDep = doc.GetElement(depId) as CurveElement;
                        if (ceDep?.GeometryCurve != null) curves.Add(ceDep.GeometryCurve);
                    }
                }
                catch { }
            }
            return curves;
        }

        private CurveLoop CreateCurveLoop(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0) return null;
            
            // Handle single closed loop (e.g. circle/ellipse if possible, though Line.CreateBound doesn't do that)
            if (curves.Count == 1 && curves[0].IsBound && curves[0].GetEndPoint(0).IsAlmostEqualTo(curves[0].GetEndPoint(1)))
            {
                try
                {
                    CurveLoop singleLoop = new CurveLoop();
                    singleLoop.Append(curves[0]);
                    return singleLoop;
                }
                catch { }
            }

            var ordered = OrderCurves(curves);
                if (ordered == null)
                {
                    TaskDialog.Show("antiGGGravity", "Boundary must be a closed loop.");
                    return null;
                }

            CurveLoop loop = new CurveLoop();
            foreach (Curve c in ordered) loop.Append(c);
            return loop;
        }

        private List<Curve> OrderCurves(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0) return null;
            double tol = 0.001;
            var remaining = new List<Curve>(curves);
            var ordered = new List<Curve>();
            ordered.Add(remaining[0]);
            remaining.RemoveAt(0);

            int iterations = curves.Count;
            for (int i = 0; i < iterations; i++)
            {
                if (remaining.Count == 0) break;
                XYZ currentEnd = ordered.Last().GetEndPoint(1);
                bool found = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    Curve c = remaining[j];
                    if (currentEnd.DistanceTo(c.GetEndPoint(0)) < tol)
                    {
                        ordered.Add(c);
                        remaining.RemoveAt(j);
                        found = true;
                        break;
                    }
                    else if (currentEnd.DistanceTo(c.GetEndPoint(1)) < tol)
                    {
                        ordered.Add(c.CreateReversed());
                        remaining.RemoveAt(j);
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }

            if (remaining.Count > 0 || ordered.Last().GetEndPoint(1).DistanceTo(ordered[0].GetEndPoint(0)) > tol)
            {
                return null;
            }
            return ordered;
        }

        private BoundingBoxXYZ GetBBox(List<Curve> curves)
        {
             double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
             double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

             foreach (var c in curves)
             {
                 XYZ p1 = c.GetEndPoint(0);
                 XYZ p2 = c.GetEndPoint(1);
                 minX = Math.Min(minX, Math.Min(p1.X, p2.X));
                 minY = Math.Min(minY, Math.Min(p1.Y, p2.Y));
                 minZ = Math.Min(minZ, Math.Min(p1.Z, p2.Z));
                 maxX = Math.Max(maxX, Math.Max(p1.X, p2.X));
                 maxY = Math.Max(maxY, Math.Max(p1.Y, p2.Y));
                 maxZ = Math.Max(maxZ, Math.Max(p1.Z, p2.Z));
             }
             BoundingBoxXYZ bbox = new BoundingBoxXYZ();
             bbox.Min = new XYZ(minX, minY, minZ);
             bbox.Max = new XYZ(maxX, maxY, maxZ);
             return bbox;
        }

        private bool ApplyCrop(View view, CurveLoop loop, List<Curve> allCurves)
        {
            try
            {
                if (!view.CropBoxActive) view.CropBoxActive = true;
                
                var mgr = view.GetCropRegionShapeManager();
                if (mgr != null)
                {
                    if (mgr.CanHaveShape)
                    {
                        mgr.SetCropShape(loop);
                        return true;
                    }
                    else
                    {
                        BoundingBoxXYZ bbox = GetBBox(allCurves);
                        if (bbox != null)
                        {
                            view.CropBox = bbox;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("antiGGGravity", ex.Message);
            }
            return false;
        }

        private void ResetCrop(View view)
        {
            var mgr = view.GetCropRegionShapeManager();
            if (mgr != null) { try { mgr.RemoveCropRegionShape(); } catch { } }
            try { view.CropBoxActive = false; view.CropBoxVisible = false; } catch { }
        }
    }


    // ===================================================================================
    // CROP REGION (TOGGLE ALL ON SHEETS)
    // ===================================================================================

    [Transaction(TransactionMode.Manual)]
    public class CropRegionCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Collect all views that are on any sheet in the project
            var allViewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            if (allViewports.Count == 0)
            {
                // Fallback to active view if no sheets/viewports exist
                View activeView = doc.ActiveView;
                if (CanToggleCrop(activeView))
                {
                    using (Transaction t = new Transaction(doc, "Toggle Crop Region"))
                    {
                        t.Start();
                        try { activeView.CropBoxVisible = !activeView.CropBoxVisible; } catch { }
                        t.Commit();
                    }
                }
                return Result.Succeeded;
            }

            // 2. Get unique set of views from viewports
            var viewsOnSheets = allViewports
                .Select(vp => doc.GetElement(vp.ViewId) as View)
                .Where(v => v != null && CanToggleCrop(v))
                .GroupBy(v => v.Id.GetIdValue())
                .Select(g => g.First())
                .ToList();

            if (viewsOnSheets.Count == 0) return Result.Succeeded;

            // 3. Determine toggle state based on global visibility
            bool anyVisible = viewsOnSheets.Any(v => v.CropBoxVisible);
            bool newState = !anyVisible;

            // 4. Update all views
            using (Transaction t = new Transaction(doc, "Toggle All Crop Regions"))
            {
                t.Start();
                foreach (var v in viewsOnSheets)
                {
                    try 
                    { 
                        if (v.CropBoxVisible != newState)
                            v.CropBoxVisible = newState; 
                    } 
                    catch { }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }

        private bool CanToggleCrop(View v)
        {
            if (v == null) return false;
            if (v.ViewType == ViewType.Legend || v.ViewType == ViewType.Schedule || v.ViewType == ViewType.DrawingSheet) return false;
            try { var test = v.CropBoxVisible; return true; } catch { return false; }
        }
    }


    // ===================================================================================
    // ZOOM TO SELECTION
    // ===================================================================================
    
    [Transaction(TransactionMode.Manual)]
    public class ZoomToSelectionCommand : BaseCommand
    {
        private static int _currentIndex = 0;
        private static string _lastSelectionHash = "";

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            var selection = uidoc.Selection.GetElementIds().ToList();
            if (selection.Count == 0)
            {
                TaskDialog.Show("antiGGGravity", "Please select one or more elements first.");
                return Result.Cancelled;
            }

            // Create hash of selection
            string currentHash = string.Join(",", selection.Select(id => id.ToString()).OrderBy(s => s));
            
            if (currentHash != _lastSelectionHash)
            {
                _currentIndex = 0;
                _lastSelectionHash = currentHash;
            }

            _currentIndex = _currentIndex % selection.Count;
            ElementId targetId = selection[_currentIndex];
            Element targetElem = doc.GetElement(targetId);

            View activeView = uidoc.ActiveView;
            View targetView = FindViewForElement(targetElem, doc, activeView);

            if (targetView != null)
            {
                if (uidoc.ActiveView.Id != targetView.Id)
                {
                    uidoc.ActiveView = targetView;
                }
                uidoc.ShowElements(targetId);
            }
            else
            {
                TaskDialog.Show("antiGGGravity", "No suitable view found for element.");
            }

            _currentIndex = (_currentIndex + 1) % selection.Count;
            
            return Result.Succeeded;
        }

        private View FindViewForElement(Element elem, Document doc, View activeView)
        {
            // 1. Check OwnerViewId (Dimensions, Detail Lines, etc.)
            if (elem.OwnerViewId != ElementId.InvalidElementId)
            {
                return doc.GetElement(elem.OwnerViewId) as View;
            }

            // 2. Check if already visible in ActiveView (Optimization)
            try 
            { 
               if (!activeView.IsTemplate && IsElementVisible(activeView, elem)) return activeView; 
            } catch { }

            // 3. Search for other views
            var viewTypes = new HashSet<ViewType> { 
                ViewType.FloorPlan, 
                ViewType.EngineeringPlan, 
                ViewType.CeilingPlan, 
                ViewType.AreaPlan, 
                ViewType.Section, 
                ViewType.Elevation, 
                ViewType.ThreeD 
            };

            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && viewTypes.Contains(v.ViewType))
                .OrderBy(v => {
                    if (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan) return 1;
                    if (v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation) return 2;
                    if (v.ViewType == ViewType.ThreeD) return 3;
                    return 4;
                })
                .ToList();

            foreach (View v in collector)
            {
                try 
                {
                    if (IsElementVisible(v, elem)) return v;
                }
                catch { }
            }

            return null;
        }

        private bool IsElementVisible(View v, Element elem)
        {
            try
            {
                // IsElementVisibleInView(Element) exists in Revit 2019+
                // Using reflection to avoid compiler errors in multi-version builds
                var method = typeof(View).GetMethod("IsElementVisibleInView", new[] { typeof(Element) });
                if (method != null)
                {
                    return (bool)method.Invoke(v, new object[] { elem });
                }
            }
            catch { }

            // Fallback for cases where method is missing or fails
            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
            if (bbox == null) return false;
            BoundingBoxXYZ vBox = v.get_BoundingBox(null);
            if (vBox == null) return false;

            return bbox.Min.X <= vBox.Max.X && bbox.Max.X >= vBox.Min.X &&
                   bbox.Min.Y <= vBox.Max.Y && bbox.Max.Y >= vBox.Min.Y;
        }
    }
}
