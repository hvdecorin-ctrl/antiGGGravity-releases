using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.Overrides
{
    public enum ColorSplashAction
    {
        Apply,
        Reset,
        CreateLegend,
        CreateFilters
    }

    public class ColorSplashHandler : IExternalEventHandler
    {
        private ColorSplasherView _view;
        public ColorSplashAction CurrentAction { get; set; }

        public ColorSplashHandler(ColorSplasherView view)
        {
            _view = view;
        }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            try
            {
                switch (CurrentAction)
                {
                    case ColorSplashAction.Apply:
                        ApplyColors(doc, activeView);
                        break;
                    case ColorSplashAction.Reset:
                        ResetColors(doc, activeView);
                        break;
                    case ColorSplashAction.CreateLegend:
                        CreateLegend(doc);
                        break;
                    case ColorSplashAction.CreateFilters:
                        TaskDialog.Show("Filters", "Filter creation not yet fully implemented in C# port.");
                        break;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Color Splasher Handler";
        }

        private void ApplyColors(Document doc, View view)
        {
            if (_view.UI_Combo_Category.SelectedItem is CategoryItem catItem &&
                _view.UI_List_Parameters.SelectedItem is ParameterItem paramItem)
            {
                using (Transaction t = new Transaction(doc, "Color Splash"))
                {
                    t.Start();

                    // Apply Overrides
                    var collector = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(catItem.Category.Id);

                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();

                    // Get Solid Fill Pattern
                    FillPatternElement solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

                    foreach (Element e in collector)
                    {
                        Parameter p = null;
                        
                        if (paramItem.IsTypeParameter)
                        {
                            Element typeElem = doc.GetElement(e.GetTypeId());
                            if (typeElem != null) p = typeElem.LookupParameter(paramItem.Name);
                        }
                        else
                        {
                            p = e.LookupParameter(paramItem.Name);
                        }

                        if (p == null) continue;

                        string val = p.AsValueString() ?? p.AsString();
                         if (val == null)
                        {
                            if (p.StorageType == StorageType.Double) val = p.AsDouble().ToString("F2");
                            else if (p.StorageType == StorageType.Integer) val = p.AsInteger().ToString();
                            else if (p.StorageType == StorageType.ElementId) val = p.AsElementId().ToString();
                            else val = "<null>";
                        }
                        if (string.IsNullOrEmpty(val)) val = "<empty>";

                        // Find color for this value
                        var valItem = _view.Values.FirstOrDefault(x => x.Value == val);
                        if (valItem != null)
                        {
                            ogs = new OverrideGraphicSettings();
                            ogs.SetSurfaceForegroundPatternColor(valItem.RevitColor);
                            ogs.SetCutForegroundPatternColor(valItem.RevitColor);
                            
                            if (solidFill != null)
                            {
                                ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                                ogs.SetCutForegroundPatternId(solidFill.Id);
                            }
                            
                            view.SetElementOverrides(e.Id, ogs);
                        }
                    }

                    t.Commit();
                }
            }
        }

        private void ResetColors(Document doc, View view)
        {
            if (_view.UI_Combo_Category.SelectedItem is CategoryItem catItem)
            {
                using (Transaction t = new Transaction(doc, "Reset Colors"))
                {
                    t.Start();
                    var collector = new FilteredElementCollector(doc, view.Id)
                        .OfCategoryId(catItem.Category.Id);

                    OverrideGraphicSettings clear = new OverrideGraphicSettings();
                    foreach (Element e in collector)
                    {
                        view.SetElementOverrides(e.Id, clear);
                    }
                    t.Commit();
                }
            }
        }
        private void CreateLegend(Document doc)
        {
             if (!(_view.UI_Combo_Category.SelectedItem is CategoryItem catItem) ||
                !(_view.UI_List_Parameters.SelectedItem is ParameterItem paramItem))
            {
                TaskDialog.Show("Error", "Select category and parameter first.");
                return;
            }

            using (Transaction t = new Transaction(doc, "Create Legend"))
            {
                t.Start();

                try
                {
                    // 1. Find existing Legend View to duplicate
                    View legendView = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(x => x.ViewType == ViewType.Legend && !x.IsTemplate);

                    if (legendView == null)
                    {
                        TaskDialog.Show("Error", "No Legend View found in project to duplicate. Create one first.");
                         t.RollBack();
                        return;
                    }

                    // 2. Duplicate Legend
                    ElementId newLegendId = legendView.Duplicate(ViewDuplicateOption.Duplicate);
                    View newLegend = doc.GetElement(newLegendId) as View;

                    // 3. Rename
                    string newName = $"Color Splasher - {catItem.Name} - {paramItem.Name}";
                    try
                    {
                        newLegend.Name = newName;
                    }
                    catch
                    {
                        newLegend.Name = newName + " " + DateTime.Now.ToString("HHmmss");
                    }

                    // 4. Prepare Types (Text & FilledRegion)
                    TextNoteType textType = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).FirstElement() as TextNoteType;
                    
                    // Find/Create Solid Filled Region Type
                    FilledRegionType solidRegionType = GetOrCreateSolidRegionType(doc);

                    // 5. Create Entries
                    double y = 0;
                    double x = 0;
                    double rowHeight = 0.5; // Feet? Text size dependent usually.
                    
                    // Simple fixed spacing for now
                    XYZ origin = XYZ.Zero;

                    // Get Solid Fill Pattern Id for Overrides
                    FillPatternElement solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);

                    foreach (var valItem in _view.Values)
                    {
                        // Create Text
                        string text = $"{catItem.Name} / {paramItem.Name} - {valItem.Value}";
                        TextNote note = TextNote.Create(doc, newLegend.Id, new XYZ(x + 2.0, y, 0), text, textType.Id); // Offset X element
                        
                        // Create Filled Region (Rectangle)
                        // Needs CurveLoop
                        double boxSize = 0.5;
                        List<CurveLoop> loops = new List<CurveLoop>();
                        CurveLoop loop = new CurveLoop();
                        loop.Append(Line.CreateBound(new XYZ(x, y, 0), new XYZ(x + boxSize, y, 0)));
                        loop.Append(Line.CreateBound(new XYZ(x + boxSize, y, 0), new XYZ(x + boxSize, y - boxSize, 0)));
                        loop.Append(Line.CreateBound(new XYZ(x + boxSize, y - boxSize, 0), new XYZ(x, y - boxSize, 0)));
                        loop.Append(Line.CreateBound(new XYZ(x, y - boxSize, 0), new XYZ(x, y, 0)));
                        loops.Add(loop);

                        FilledRegion region = FilledRegion.Create(doc, solidRegionType.Id, newLegend.Id, loops);

                        // Override Color
                        OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                        ogs.SetSurfaceForegroundPatternColor(valItem.RevitColor);
                        if (solidFill != null) ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        newLegend.SetElementOverrides(region.Id, ogs);

                        y -= (boxSize * 1.5);
                    }

                    t.Commit();
                    TaskDialog.Show("Success", $"Created Legend: {newLegend.Name}");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show("Error", "Failed to create legend: " + ex.Message);
                }
            }
        }

        private FilledRegionType GetOrCreateSolidRegionType(Document doc)
        {
            // Find existing
            var types = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>();
            foreach(var t in types)
            {
                 // Check if solid? 
                 // It's hard to check pattern easily without getting the element, skipping detailed check for speed
                 // Just return first valid one or duplicate
                 return t; 
            }
            return null; // Should handle creation if null
        }
    }
}
