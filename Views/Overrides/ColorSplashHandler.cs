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
                        CreateFilters(doc, activeView);
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
                    
                    // Conversion: 1 mm = 0.00328084 feet
                    double mmToFeet = 0.00328084;
                    double boxWidth = 120.0 * mmToFeet;
                    double boxHeight = 60.0 * mmToFeet;
                    double textGap = 50.0 * mmToFeet;
                    
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
                        
                        // Default Revit text origin is Top-Left but can vary.
                        // Y = y is the top of the box. y - boxHeight is the bottom.
                        // Center of the box mathematically is y - (boxHeight / 2)
                        // Note: text height offsets slightly visually, so we might need a minor correction
                        XYZ textPos = new XYZ(x + boxWidth + textGap, y - (boxHeight / 2) + (5.0 * mmToFeet), 0);
                        
                        TextNoteOptions options = new TextNoteOptions(textType.Id);
                        options.HorizontalAlignment = HorizontalTextAlignment.Left;
                        options.VerticalAlignment = VerticalTextAlignment.Middle;

                        TextNote note = TextNote.Create(doc, newLegend.Id, textPos, text, options); 
                        
                        // Create Filled Region (Rectangle)
                        List<CurveLoop> loops = new List<CurveLoop>();
                        CurveLoop loop = new CurveLoop();
                        loop.Append(Line.CreateBound(new XYZ(x,            y,             0), new XYZ(x + boxWidth, y,             0)));
                        loop.Append(Line.CreateBound(new XYZ(x + boxWidth, y,             0), new XYZ(x + boxWidth, y - boxHeight, 0)));
                        loop.Append(Line.CreateBound(new XYZ(x + boxWidth, y - boxHeight, 0), new XYZ(x,            y - boxHeight, 0)));
                        loop.Append(Line.CreateBound(new XYZ(x,            y - boxHeight, 0), new XYZ(x,            y,             0)));
                        loops.Add(loop);

                        FilledRegion region = FilledRegion.Create(doc, solidRegionType.Id, newLegend.Id, loops);

                        // Override Color
                        OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                        ogs.SetSurfaceForegroundPatternColor(valItem.RevitColor);
                        if (solidFill != null) ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                        newLegend.SetElementOverrides(region.Id, ogs);

                        y -= (boxHeight * 1.5); // Add 50% spacing vertically between boxes
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

        private void CreateFilters(Document doc, View view)
        {
            if (!(_view.UI_Combo_Category.SelectedItem is CategoryItem catItem) ||
                !(_view.UI_List_Parameters.SelectedItem is ParameterItem paramItem))
            {
                TaskDialog.Show("Error", "Select category and parameter first.");
                return;
            }

            using (Transaction t = new Transaction(doc, "Create Filters"))
            {
                t.Start();
                try
                {
                    ElementId paramId = paramItem.Id;
                    var catIdList = new List<ElementId> { catItem.Category.Id };

                    var existingFilters = new FilteredElementCollector(doc)
                        .OfClass(typeof(ParameterFilterElement))
                        .Cast<ParameterFilterElement>()
                        .ToList();

                    var viewFilters = view.GetFilters();

                    FillPatternElement solidFill = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

                    foreach (var valItem in _view.Values)
                    {
                        OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                        ogs.SetSurfaceForegroundPatternColor(valItem.RevitColor);
                        ogs.SetCutForegroundPatternColor(valItem.RevitColor);
                        if (solidFill != null)
                        {
                            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                            ogs.SetCutForegroundPatternId(solidFill.Id);
                        }

                        // Sanitize filter name
                        string filterName = $"{catItem.Name} {paramItem.Name} - {valItem.Value}";
                        char[] invalidChars = { '{', '}', '[', ']', ':', '\\', '|', '?', '/', '<', '>', '*' };
                        foreach (char c in invalidChars) { filterName = filterName.Replace(c.ToString(), ""); }

                        ParameterFilterElement filterElement = existingFilters.FirstOrDefault(f => f.Name == filterName);

                        if (filterElement != null)
                        {
                            if (!viewFilters.Contains(filterElement.Id))
                            {
                                view.AddFilter(filterElement.Id);
                            }
                            view.SetFilterOverrides(filterElement.Id, ogs);
                        }
                        else
                        {
                            // Create new filter
                            FilterRule rule = null;
                            if (paramItem.StorageType == StorageType.Double)
                            {
                                rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, valItem.DoubleValue, 0.001);
                            }
                            else if (paramItem.StorageType == StorageType.ElementId)
                            {
                                if (long.TryParse(valItem.Value, out long idAsLong))
                                {
                                    rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, new ElementId(idAsLong));
                                }
                                else
                                {
                                    rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, ElementId.InvalidElementId);
                                }
                            }
                            else if (paramItem.StorageType == StorageType.Integer)
                            {
                                int val = 0;
                                int.TryParse(valItem.Value, out val);
                                rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, val);
                            }
                            else if (paramItem.StorageType == StorageType.String)
                            {
                                string val = valItem.Value == "<empty>" || valItem.Value == "<null>" ? "" : valItem.Value;
                                rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, val);
                            }

                            if (rule != null)
                            {
                                ElementParameterFilter elemFilter = new ElementParameterFilter(rule);
                                ParameterFilterElement newFilter = ParameterFilterElement.Create(doc, filterName, catIdList, elemFilter);
                                existingFilters.Add(newFilter);
                                view.AddFilter(newFilter.Id);
                                view.SetFilterOverrides(newFilter.Id, ogs);
                            }
                        }
                    }
                    t.Commit();
                    TaskDialog.Show("Success", "Filters created successfully!");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show("Error", "Failed to create filters: " + ex.Message);
                }
            }
        }
    }
}
