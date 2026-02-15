using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using antiGGGravity.Views.General;

namespace antiGGGravity.Commands.General
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Grid3D2DCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (view.ViewType != ViewType.FloorPlan && view.ViewType != ViewType.EngineeringPlan)
            {
                TaskDialog.Show("Grid 3D/2D", "Please run this script from a floor or structural plan view.");
                return Result.Cancelled;
            }

            TaskDialog td = new TaskDialog("Grid 3D/2D");
            td.MainContent = "Set grid extents to 2D (View Specific) or 3D (Model)?";
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "2D (View Specific)");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "3D (Model)");
            
            TaskDialogResult result = td.Show();
            DatumExtentType extentType;
            if (result == TaskDialogResult.CommandLink1) extentType = DatumExtentType.ViewSpecific;
            else if (result == TaskDialogResult.CommandLink2) extentType = DatumExtentType.Model;
            else return Result.Cancelled;

            var grids = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).Cast<Grid>().ToList();

            using (Transaction t = new Transaction(doc, "Toggle Grid Extents"))
            {
                t.Start();
                foreach (Grid grid in grids)
                {
                    try
                    {
                        if (grid.CanBeHidden(view))
                        {
                            grid.SetDatumExtentType(DatumEnds.End0, view, extentType);
                            grid.SetDatumExtentType(DatumEnds.End1, view, extentType);
                        }
                    }
                    catch { }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleAllGridsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!(view is ViewPlan viewPlan))
            {
                TaskDialog.Show("Toggle Grids", "This tool only works in plan views.");
                return Result.Cancelled;
            }

            ToggleGridsView win = new ToggleGridsView();
            if (win.ShowDialog() != true) return Result.Cancelled;

            var selected = win.SelectedDirections;
            var selIds = uidoc.Selection.GetElementIds();
            
            List<Grid> grids;
            if (selIds.Any())
            {
                grids = selIds.Select(id => doc.GetElement(id)).OfType<Grid>().ToList();
            }
            else
            {
                grids = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Grids)
                    .WhereElementIsNotElementType()
                    .Cast<Grid>()
                    .ToList();
            }

            if (!grids.Any()) return Result.Succeeded;

            XYZ viewRight = viewPlan.RightDirection.Normalize();
            XYZ viewUp = viewPlan.UpDirection.Normalize();

            using (Transaction t = new Transaction(doc, "Toggle Grid Bubbles"))
            {
                t.Start();
                foreach (Grid grid in grids)
                {
                    var ends = GetDirectionLabel(grid, viewRight, viewUp);
                    foreach (var d in new[] { "Top", "Bottom", "Left", "Right" })
                    {
                        if (ends.ContainsKey(d))
                        {
                            if (selected.Contains(d)) grid.ShowBubbleInView(ends[d], view);
                            else grid.HideBubbleInView(ends[d], view);
                        }
                    }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }

        private Dictionary<string, DatumEnds> GetDirectionLabel(Grid grid, XYZ viewRight, XYZ viewUp)
        {
            var dict = new Dictionary<string, DatumEnds>();
            Curve curve = grid.Curve;
            if (curve == null || curve.IsCyclic) return dict;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            if (p0.IsAlmostEqualTo(p1)) return dict;

            XYZ lineDir = (p1 - p0).Normalize();
            double dotUp = Math.Abs(lineDir.DotProduct(viewUp));
            double dotRight = Math.Abs(lineDir.DotProduct(viewRight));

            if (dotUp > dotRight)
            {
                if (p0.Y > p1.Y) { dict["Top"] = DatumEnds.End0; dict["Bottom"] = DatumEnds.End1; }
                else { dict["Top"] = DatumEnds.End1; dict["Bottom"] = DatumEnds.End0; }
            }
            else
            {
                double proj0 = (p0 - XYZ.Zero).DotProduct(viewRight);
                double proj1 = (p1 - XYZ.Zero).DotProduct(viewRight);
                if (proj0 > proj1) { dict["Right"] = DatumEnds.End0; dict["Left"] = DatumEnds.End1; }
                else { dict["Right"] = DatumEnds.End1; dict["Left"] = DatumEnds.End0; }
            }
            return dict;
        }
    }
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ViewFiltersCopyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Show UI
            // The View handles data loading internally now, just pass the Document.
            try
            {
                ViewFiltersCopyView win = new ViewFiltersCopyView(doc);
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ViewFiltersLegendCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // 2. Show UI
            // Data loading is now handled internally by the View to match Python logic (specifically identifying templates vs views)
            ViewFiltersLegendView win = new ViewFiltersLegendView(doc);
            if (win.ShowDialog() != true) return Result.Cancelled;

            // 3. User Selection
            View sourceView = win.SelectedSourceView;
            TextNoteType textType = win.SelectedTextType;
            string colourSource = win.ColourSource;
            double w_mm = win.BoxWidth;
            double h_mm = win.BoxHeight;
            double offset_mm = win.BoxOffset;

            // 4. Prompt for Location
            XYZ pt = null;
            try
            {
                pt = uidoc.Selection.PickPoint("Pick location for legend");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            // 5. Drawing Logic
            // Python Scale Logic: scale = float(view.Scale) / 100
            double scale = (double)activeView.Scale / 100.0;
            
            double mmToFeet = 1.0 / 304.8;
            double w = w_mm * mmToFeet * scale;
            double h = h_mm * mmToFeet * scale;
            // Increase text offset to avoid clashing (User input: "too close")
            // 50mm seems reasonable for a 1000mm box.
            double text_offset = 200.0 * mmToFeet * scale; 
            double shift = (offset_mm + h_mm) * mmToFeet * scale;

            // Geometry Helpers
             ElementId solidFillId = GetSolidFillPattern(doc);
             ElementId lineStyleId = GetSolidLineStyle(doc);
             ElementId filledRegionTypeId = GetSolidFilledRegionType(doc);

             if (solidFillId == null || filledRegionTypeId == null)
             {
                 TaskDialog.Show("Error", "Could not find Solid Fill Pattern or Filled Region Type.");
                 return Result.Failed;
             }

            using (Transaction t = new Transaction(doc, "Draw Filters Legend"))
            {
                t.Start();
                
                var filterIds = sourceView.GetFilters();
                var filters = new List<ParameterFilterElement>();
                foreach(var id in filterIds)
                {
                    if (doc.GetElement(id) is ParameterFilterElement pfe) filters.Add(pfe);
                }

                // Sort by name
                filters = filters.OrderBy(f => f.Name).ToList();

                double currentY = pt.Y;

                foreach (var filter in filters)
                {
                     OverrideGraphicSettings ogs = sourceView.GetFilterOverrides(filter.Id);
                     
                     // Check and extract data
                     Color fgColor = null;
                     ElementId fgPattern = null;
                     Color bgColor = null;
                     ElementId bgPattern = null;
                     bool hasOverride = false;

                     if (colourSource == "Projection")
                     {
                         if (ogs.SurfaceForegroundPatternColor.IsValid) { fgColor = ogs.SurfaceForegroundPatternColor; fgPattern = ogs.SurfaceForegroundPatternId; hasOverride = true; }
                         if (ogs.SurfaceBackgroundPatternColor.IsValid) { bgColor = ogs.SurfaceBackgroundPatternColor; bgPattern = ogs.SurfaceBackgroundPatternId; hasOverride = true; }
                         if (!hasOverride && ogs.ProjectionLineColor.IsValid) { fgColor = ogs.ProjectionLineColor; fgPattern = solidFillId; hasOverride = true; }
                     }
                     else // Cut
                     {
                         if (ogs.CutForegroundPatternColor.IsValid) { fgColor = ogs.CutForegroundPatternColor; fgPattern = ogs.CutForegroundPatternId; hasOverride = true; }
                         if (ogs.CutBackgroundPatternColor.IsValid) { bgColor = ogs.CutBackgroundPatternColor; bgPattern = ogs.CutBackgroundPatternId; hasOverride = true; }
                         if (!hasOverride && ogs.CutLineColor.IsValid) { fgColor = ogs.CutLineColor; fgPattern = solidFillId; hasOverride = true; }
                     }

                     if (!hasOverride) continue;

                     // Fix invalid patterns to Solid if needed or keep logic
                     if (fgPattern != null && fgPattern == ElementId.InvalidElementId) fgPattern = solidFillId;
                     if (bgPattern != null && bgPattern == ElementId.InvalidElementId) bgPattern = solidFillId;

                     // Draw Rectangle
                     XYZ p1 = new XYZ(pt.X, currentY, 0);
                     XYZ p2 = new XYZ(pt.X + w, currentY, 0);
                     XYZ p3 = new XYZ(pt.X + w, currentY + h, 0); // Logic check: Y goes up? Python: p4, p1.
                     // Python: p1(x,y), p2(x+w,y), p3(x+w, y+h), p4(x, y+h). 
                     // Typically in Revit Y is Up. 
                     // If we want to list DOWN, we should decrement Y.
                     // Python loop used offset += shift, but applied translation vector (0, -1, 0) * offset. So yes, going down.
                     // Here I am calculating currentY manually. Let's start from pt.Y and subtract shift.
                     // Note: Rectangle should be drawn relative to currentY.

                     List<CurveLoop> loops = new List<CurveLoop>();
                     CurveLoop loop = new CurveLoop();
                     loop.Append(Line.CreateBound(p1, p2));
                     loop.Append(Line.CreateBound(p2, p3));
                     loop.Append(Line.CreateBound(p3, new XYZ(pt.X, currentY + h, 0)));
                     loop.Append(Line.CreateBound(new XYZ(pt.X, currentY + h, 0), p1));
                     loops.Add(loop);

                     try
                     {
                         FilledRegion fr = FilledRegion.Create(doc, filledRegionTypeId, activeView.Id, loops);
                         if (lineStyleId != null) fr.SetLineStyleId(lineStyleId);

                         // Apply Overrides to Region
                         OverrideGraphicSettings regionOGS = new OverrideGraphicSettings();
                         if (fgColor != null)
                         {
                             regionOGS.SetSurfaceForegroundPatternId(fgPattern);
                             regionOGS.SetSurfaceForegroundPatternColor(fgColor);
                             regionOGS.SetSurfaceForegroundPatternVisible(true);
                         }
                         if (bgColor != null)
                         {
                             regionOGS.SetSurfaceBackgroundPatternId(bgPattern);
                             regionOGS.SetSurfaceBackgroundPatternColor(bgColor);
                             regionOGS.SetSurfaceBackgroundPatternVisible(true);
                         }
                         activeView.SetElementOverrides(fr.Id, regionOGS);

                         // Text
                         // Move text abit up for better presentation (User input: "move text abit up")
                         // Using h * 0.85 to center/top-align text better relative to the box.
                         XYZ textPos = new XYZ(pt.X + w + text_offset, currentY + (h * 0.85), 0);
                         TextNote.Create(doc, activeView.Id, textPos, filter.Name, textType.Id);

                     }
                     catch {}

                     currentY -= shift;
                }
                
                t.Commit();
            }

            return Result.Succeeded;
        }

        private ElementId GetSolidFillPattern(Document doc)
        {
            var pattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
            return pattern?.Id;
        }

        private ElementId GetSolidFilledRegionType(Document doc)
        {
             var allTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .ToList();

             var preferred = new[] { "filled region", "solid", "opaque", "masking" };
             
             foreach (var p in preferred)
             {
                 var match = allTypes.FirstOrDefault(t => t.Name.ToLower().Contains(p));
                 if (match != null) return match.Id;
             }

             return allTypes.FirstOrDefault()?.Id;
        }

        private ElementId GetSolidLineStyle(Document doc)
        {
             Category lineCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
             if (lineCat == null) return null;

             var subCats = lineCat.SubCategories;
             
             // Try Thin or Medium
             foreach (Category sc in subCats)
             {
                 string n = sc.Name.ToLower();
                 if (n.Contains("thin") || n.Contains("medium"))
                     return sc.GetGraphicsStyle(GraphicsStyleType.Projection).Id;
             }
             
             // Try any visible
             foreach (Category sc in subCats)
             {
                 string n = sc.Name.ToLower();
                 if (!n.Contains("invisible") && !n.Contains("hidden") && !n.Contains("<"))
                     return sc.GetGraphicsStyle(GraphicsStyleType.Projection).Id;
             }

             return null; 
        }

    }
}
