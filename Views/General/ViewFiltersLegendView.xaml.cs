using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class ViewFiltersLegendView : Window
    {
        private Document _doc;
        private List<LegendViewItem> _allViews;
        private List<TextNoteType> _textTypes;

        // Result Properties
        public Autodesk.Revit.DB.View SelectedSourceView { get; private set; }
        public TextNoteType SelectedTextType { get; private set; }
        public string ColourSource { get; private set; }
        public double BoxWidth { get; private set; }
        public double BoxHeight { get; private set; }
        public double BoxOffset { get; private set; }

        public ViewFiltersLegendView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
        }

        private void LoadData()
        {
            // 1. Load Views
            _allViews = new List<LegendViewItem>();
            var allowedTypes = new[] { 
                ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.Elevation, 
                ViewType.ThreeD, ViewType.DraftingView, ViewType.AreaPlan, 
                ViewType.Section, ViewType.Detail, ViewType.Legend, 
                ViewType.EngineeringPlan 
            };

            // Use OST_Views to ensure we match Python's broad collection
            var collector = new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_Views).WhereElementIsNotElementType();
            
            foreach (var elem in collector)
            {
                if (elem is Autodesk.Revit.DB.View v)
                {
                    // Check if view supports overrides to avoid crash
                    if (!v.AreGraphicsOverridesAllowed()) continue;

                    bool isTemplate = v.IsTemplate;
                    
                    if (isTemplate)
                    {
                        // Match Python: [T] prefix
                        _allViews.Add( new LegendViewItem($"[T] {v.Name}", v) { IsTemplate = true });
                    }
                    else if (allowedTypes.Contains(v.ViewType))
                    {
                        // Match Python: No prefix
                        _allViews.Add( new LegendViewItem(v.Name, v) { IsTemplate = false });
                    }
                }
            }
            
            _allViews = _allViews.OrderBy(x => x.Name).ToList();
            
            // Initial Populate
            UpdateViewList();

            // Try to select active view
            if (_doc.ActiveView != null)
            {
                 var active = UI_List_Views.Items.Cast<LegendViewItem>().FirstOrDefault(v => v.View.Id == _doc.ActiveView.Id);
                 if (active != null) 
                 {
                     UI_List_Views.SelectedItem = active;
                     UI_List_Views.ScrollIntoView(active);
                 }
                 else if (UI_List_Views.Items.Count > 0)
                 {
                     UI_List_Views.SelectedIndex = 0;
                 }
            }
            else if (UI_List_Views.Items.Count > 0)
            {
                UI_List_Views.SelectedIndex = 0;
            }

            // 2. Load Text Types
            _textTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .OrderBy(t => t.Name)
                .ToList();

            UI_Combo_TextStyle.ItemsSource = _textTypes;
            UI_Combo_TextStyle.DisplayMemberPath = "Name";
            if (_textTypes.Any()) UI_Combo_TextStyle.SelectedIndex = 0;
        }

        private void UpdateViewList()
        {
            if (UI_List_Views == null) return;

            string filter = UI_Txt_Search?.Text?.ToLower() ?? "";
            bool showViews = UI_Check_Views?.IsChecked == true;
            bool showTemplates = UI_Check_Templates?.IsChecked == true;

            var filtered = _allViews.Where(item => 
            {
                if (item.IsTemplate && !showTemplates) return false;
                if (!item.IsTemplate && !showViews) return false;
                if (!string.IsNullOrEmpty(filter) && !item.Name.ToLower().Contains(filter)) return false;
                return true;
            }).ToList();

            UI_List_Views.ItemsSource = filtered;
        }

        private void OnViewFilterChanged(object sender, RoutedEventArgs e)
        {
            UpdateViewList();
        }

        private void OnViewSearchChanged(object sender, TextChangedEventArgs e)
        {
            UpdateViewList();
        }

        private void UI_List_Views_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update Filter Preview
            UpdateFilterPreview();
        }

        private void UI_Combo_ColourSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFilterPreview();
        }

        private void UpdateFilterPreview()
        {
            if (UI_List_Filters == null) return;

            var selectedItem = UI_List_Views.SelectedItem as LegendViewItem; // Changed from Combo to List
            SelectedSourceView = selectedItem?.View;

            if (SelectedSourceView == null)
            {
                UI_List_Filters.ItemsSource = null;
                UI_Label_Filters.Text = "Existing Filters in Selected View";
                return;
            }
            
            // Truncate name for label if needed, or just show full
            string viewName = SelectedSourceView.Name;
            if (viewName.Length > 30) viewName = viewName.Substring(0, 27) + "...";
            UI_Label_Filters.Text = $"Existing Filters in: {viewName}";

            var filters = SelectedSourceView.GetFilters();
            if (!filters.Any())
            {
                UI_List_Filters.ItemsSource = new List<string> { "(No filters applied)" };
                return;
            }

            string source = (UI_Combo_ColourSource.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Projection";
            if (UI_Combo_ColourSource.SelectedItem is ComboBoxItem cbi && cbi.Content is string s) source = s;
            else if (UI_Combo_ColourSource.SelectedValue is string sv) source = sv; 

            var filterNames = new List<string>();
            foreach(var id in filters)
            {
                var elem = _doc.GetElement(id);
                if (elem == null) continue;
                filterNames.Add(elem.Name);
            }
            
            if (!filterNames.Any())
            {
                UI_List_Filters.ItemsSource = new List<string> { "(No filters applied to this view)" };
            }
            else
            {
                 UI_List_Filters.ItemsSource = filterNames.OrderBy(n => n).ToList();
            }
        }

        private bool CheckOverride(OverrideGraphicSettings overrides, string source)
        {
            if (source == "Projection")
            {
                if (overrides.SurfaceForegroundPatternId != ElementId.InvalidElementId || overrides.SurfaceForegroundPatternColor.IsValid) return true;
                if (overrides.SurfaceBackgroundPatternId != ElementId.InvalidElementId || overrides.SurfaceBackgroundPatternColor.IsValid) return true;
                if (overrides.ProjectionLineColor.IsValid) return true;
            }
            else // Cut
            {
                if (overrides.CutForegroundPatternId != ElementId.InvalidElementId || overrides.CutForegroundPatternColor.IsValid) return true;
                if (overrides.CutBackgroundPatternId != ElementId.InvalidElementId || overrides.CutBackgroundPatternColor.IsValid) return true;
                if (overrides.CutLineColor.IsValid) return true;
            }
            return false;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UI_Btn_Create_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSourceView == null)
            {
                MessageBox.Show("Please select a source view.", "Filters Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedTextType = UI_Combo_TextStyle.SelectedItem as TextNoteType;
            if (SelectedTextType == null)
            {
                 MessageBox.Show("Please select a text style.", "Filters Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var cbi = UI_Combo_ColourSource.SelectedItem as ComboBoxItem;
            ColourSource = cbi?.Content?.ToString() ?? "Projection";

            if (!double.TryParse(UI_Txt_Width.Text, out double w) || !double.TryParse(UI_Txt_Height.Text, out double h) || !double.TryParse(UI_Txt_Offset.Text, out double o))
            {
                 MessageBox.Show("Please enter valid numeric values for dimensions.", "Filters Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BoxWidth = w;
            BoxHeight = h;
            BoxOffset = o;

            DialogResult = true;
            Close();
        }
    }

    public class LegendViewItem
    {
        public string Name { get; }
        public Autodesk.Revit.DB.View View { get; }
        public bool IsTemplate { get; set; }
        public LegendViewItem(string name, Autodesk.Revit.DB.View view)
        {
            Name = name;
            View = view;
        }
    }
}
