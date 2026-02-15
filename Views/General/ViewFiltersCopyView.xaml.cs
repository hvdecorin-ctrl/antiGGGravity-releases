using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using System.Runtime.CompilerServices;

namespace antiGGGravity.Views.General
{
    public partial class ViewFiltersCopyView : Window, INotifyPropertyChanged
    {
        private Document _doc;
        private List<ViewItem> _allSourceItems;
        private List<ViewItem> _allDestItems;
        
        // Observable collections bound to UI
        public ObservableCollection<ViewItem> SourceViews { get; set; }
        public ObservableCollection<FilterItem> FiltersList { get; set; }
        public ObservableCollection<ViewItem> DestViews { get; set; }
        public ObservableCollection<FilterItem> DestFiltersPreview { get; set; }

        // Selection State
        private ViewItem _currentSourceView;
        private List<ViewItem> _selectedDestinations = new List<ViewItem>();
        private List<FilterItem> _selectedFilters = new List<FilterItem>();

        public ViewFiltersCopyView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            DataContext = this;

            SourceViews = new ObservableCollection<ViewItem>();
            FiltersList = new ObservableCollection<FilterItem>();
            DestViews = new ObservableCollection<ViewItem>();
            DestFiltersPreview = new ObservableCollection<FilterItem>();

            LoadData();

            // Bind Data Sources
            UI_ListBox_Src_Views.ItemsSource = SourceViews;
            UI_ListBox_Dest_Views.ItemsSource = DestViews;
            UI_ListBox_Filters.ItemsSource = FiltersList;
            UI_ListBox_Dest_Filters.ItemsSource = DestFiltersPreview;
        }

        private void LoadData()
        {
            _allSourceItems = new List<ViewItem>();
            _allDestItems = new List<ViewItem>();

            // Use OfClass(typeof(View)) for safety and CAST
            var collector = new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<Autodesk.Revit.DB.View>();
            foreach (Autodesk.Revit.DB.View v in collector)
            {
                if (!v.AreGraphicsOverridesAllowed()) continue;

                if (v.IsTemplate) 
                {
                    // Python Logic check: The original script used VIEW_TYPE_MAP for everything.
                    // But standard Generic naming often uses [T].
                    // User asked to "refer to original python code".
                    // Python: prefix = VIEW_TYPE_MAP.get(v.ViewType, '[?]')
                    // It does NOT have a special case for templates in the naming, only in sorting.
                    // So [FLOOR] Template Name is the python behavior.
                    // However, to be more helpful, distinguishing templates is good.
                    // But sticking to "Copy Original Code" instruction strictly:
                    string prefix = ViewTypeMap.ContainsKey(v.ViewType) ? ViewTypeMap[v.ViewType] : "[?]";
                    string name = $"{prefix} {v.Name}";
                    var item = new ViewItem(name, v);
                    
                    // Python used separate lists for templates/views, then merged.
                    // Here we have flags.
                    
                     if (v.GetFilters().Count > 0) _allSourceItems.Add(item);
                     _allDestItems.Add(item);
                }
                else
                {
                    string prefix = ViewTypeMap.ContainsKey(v.ViewType) ? ViewTypeMap[v.ViewType] : "[?]";
                    string name = $"{prefix} {v.Name}";
                    var item = new ViewItem(name, v);
                    
                    if (v.GetFilters().Count > 0) _allSourceItems.Add(item);
                    _allDestItems.Add(item);
                }
            }

            // Re-create Dest Items as distinct objects
            _allDestItems = _allDestItems.Select(x => new ViewItem(x.Name, x.View)).OrderBy(x => x.Name).ToList();
            _allSourceItems = _allSourceItems.OrderBy(x => x.Name).ToList();

            UpdateSourceList();
            UpdateDestList();
        }

        #region Source Panel Logic

        private void Check_Source_Views_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSourceList();
        }

        private void Filter_Source_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSourceList();
        }

        private void UpdateSourceList()
        {
            if (SourceViews == null) return;

            SourceViews.Clear();
            bool showViews = UI_checkbox_src_views.IsChecked == true;
            bool showTemplates = UI_checkbox_src_templates.IsChecked == true;
            string filter = textbox_src_filter.Text?.ToLower() ?? "";

            foreach (var item in _allSourceItems)
            {
                bool isTemplate = item.View.IsTemplate;
                if ((isTemplate && !showTemplates) || (!isTemplate && !showViews)) continue;
                if (!string.IsNullOrEmpty(filter) && !item.Name.ToLower().Contains(filter)) continue;

                // Reset check state if it was checked but now hidden? 
                // No, keep state, but practically specific logic below handles selection.
                // We need to re-bind the checked state listener or handle it via DataBinding.
                // Since we are re-populating ObservableCollection using existing objects, state is preserved if object is same.
                SourceViews.Add(item);
            }
        }

        private void Source_View_Checked(object sender, RoutedEventArgs e)
        {
            // Enforce Single Selection
             var cb = sender as CheckBox;
             if (cb?.DataContext is ViewItem checkedItem)
             {
                 _currentSourceView = checkedItem;
                 
                 // Uncheck others
                 foreach (var item in _allSourceItems)
                 {
                     if (item != checkedItem && item.IsChecked)
                     {
                         item.IsChecked = false; // Triggers notify?
                     }
                 }
                 
                 checkedItem.IsChecked = true; // Ensure visual sync
                 
                 LoadFiltersForSource(checkedItem.View);
                 UpdateStatus();
             }
        }

        private void Source_View_Unchecked(object sender, RoutedEventArgs e)
        {
             var cb = sender as CheckBox;
             if (cb?.DataContext is ViewItem uncheckedItem)
             {
                 if (_currentSourceView == uncheckedItem)
                 {
                     _currentSourceView = null;
                     FiltersList.Clear();
                     _selectedFilters.Clear();
                     UpdateStatus();
                 }
                 uncheckedItem.IsChecked = false;
             }
        }

        private void LoadFiltersForSource(Autodesk.Revit.DB.View view)
        {
            FiltersList.Clear();
            _selectedFilters.Clear();

            ICollection<ElementId> filterIds = view.GetFilters(); // GetOrderedFilters for newer Revit versions if needed
            
            List<FilterItem> fItems = new List<FilterItem>();
            foreach (var id in filterIds)
            {
                var f = _doc.GetElement(id);
                if (f != null)
                {
                    fItems.Add(new FilterItem(f.Name, f));
                }
            }
            
            foreach (var f in fItems.OrderBy(x => x.Name))
            {
                FiltersList.Add(f);
            }
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox)?.DataContext is FilterItem item)
            {
                if (!_selectedFilters.Contains(item)) _selectedFilters.Add(item);
                item.IsChecked = true;
                UpdateStatus();
            }
        }

        private void Filter_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox)?.DataContext is FilterItem item)
            {
                if (_selectedFilters.Contains(item)) _selectedFilters.Remove(item);
                item.IsChecked = false;
                UpdateStatus();
            }
        }

        private void Select_All_Filters_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FiltersList)
            {
                item.IsChecked = true;
                if (!_selectedFilters.Contains(item)) _selectedFilters.Add(item);
            }
            UpdateStatus();
        }

        private void Select_None_Filters_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FiltersList)
            {
                item.IsChecked = false;
            }
            _selectedFilters.Clear();
            UpdateStatus();
        }

        #endregion

        #region Destination Panel Logic

        private void Check_Dest_Views_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDestList();
        }

        private void Filter_Dest_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDestList();
        }

        private void UpdateDestList()
        {
            if (DestViews == null) return;

            DestViews.Clear();
            bool showViews = UI_checkbox_dest_views.IsChecked == true;
            bool showTemplates = UI_checkbox_dest_templates.IsChecked == true;
            string filter = textbox_dest_filter.Text?.ToLower() ?? "";

            foreach (var item in _allDestItems)
            {
                bool isTemplate = item.View.IsTemplate;
                if ((isTemplate && !showTemplates) || (!isTemplate && !showViews)) continue;
                if (!string.IsNullOrEmpty(filter) && !item.Name.ToLower().Contains(filter)) continue;

                DestViews.Add(item);
            }
        }

        private void Dest_View_Checked(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox)?.DataContext is ViewItem item)
            {
                if (!_selectedDestinations.Contains(item)) _selectedDestinations.Add(item);
                item.IsChecked = true;
                UpdateDestPreview();
                UpdateStatus();
            }
        }

        private void Dest_View_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox)?.DataContext is ViewItem item)
            {
                if (_selectedDestinations.Contains(item)) _selectedDestinations.Remove(item);
                item.IsChecked = false;
                UpdateDestPreview();
                UpdateStatus();
            }
        }

        private void UpdateDestPreview()
        {
            DestFiltersPreview.Clear();
            if (_selectedDestinations.Count == 0)
            {
                UI_Label_Dest_Filters.Text = "Existing Filters in Selected View:";
                return;
            }

            var first = _selectedDestinations[0];
            int count = _selectedDestinations.Count;
            
            if (count == 1)
                UI_Label_Dest_Filters.Text = $"Existing Filters in '{Truncate(first.Name, 25)}':";
            else
                UI_Label_Dest_Filters.Text = $"Filters in '{Truncate(first.Name, 20)}' (+{count-1}):";

            try
            {
                 var ids = first.View.GetFilters();
                 foreach(var id in ids)
                 {
                     var f = _doc.GetElement(id);
                     if (f!=null) DestFiltersPreview.Add(new FilterItem(f.Name, f));
                 }
                 if (DestFiltersPreview.Count == 0) DestFiltersPreview.Add(new FilterItem("(No filters applied)", null));
            }
            catch
            {
                DestFiltersPreview.Add(new FilterItem("(Unable to read)", null));
            }
        }

        private string Truncate(string s, int len)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= len ? s : s.Substring(0, len) + "...";
        }

        #endregion

        #region Actions

        private void UpdateStatus()
        {
            string src = _currentSourceView != null ? Truncate(_currentSourceView.Name, 20) : "None";
            int fc = _selectedFilters.Count;
            int dc = _selectedDestinations.Count;

            if (_currentSourceView == null)
                UI_Status_Text.Text = "Select a source view";
            else if (fc == 0)
                UI_Status_Text.Text = $"Source: {src} | Select filters";
            else if (dc == 0)
                UI_Status_Text.Text = $"Source: {src} | {fc} filter(s) | Select destinations";
            else
                UI_Status_Text.Text = $"Ready: {fc} filter(s) -> {dc} view(s)";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CopyFilters_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSourceView == null) { MessageBox.Show("Please select a Source View.", "Missing Source"); return; }
            if (_selectedFilters.Count == 0) { MessageBox.Show("Please select at least one Filter.", "Missing Filters"); return; }
            if (_selectedDestinations.Count == 0) { MessageBox.Show("Please select at least one Destination View.", "Missing Destination"); return; }

            int success = 0;
            int fail = 0;

            using (Transaction t = new Transaction(_doc, "Copy Filters"))
            {
                t.Start();
                
                foreach (var destItem in _selectedDestinations)
                {
                    var destView = destItem.View;
                    foreach (var filterItem in _selectedFilters)
                    {
                        try
                        {
                            ElementId filterId = filterItem.FilterElement.Id;
                            // Check if filter exists in view? SetFilterOverrides adds it if not present, and updates if present.
                            // But we need to make sure the filter is valid for the view. 
                            // Revit API handles this usually, or strict check.
                            
                            // Get overrides from source
                            OverrideGraphicSettings settings = _currentSourceView.View.GetFilterOverrides(filterId);
                            bool visible = _currentSourceView.View.GetFilterVisibility(filterId);

                            // Apply to dest
                            // Note: If filter is not applied, SetFilterOverrides adds it.
                            if (!destView.IsFilterApplied(filterId))
                            {
                                destView.AddFilter(filterId);
                            }
                            
                            destView.SetFilterOverrides(filterId, settings);
                            destView.SetFilterVisibility(filterId, visible);

                            success++;
                        }
                        catch
                        {
                            fail++;
                        }
                    }
                }

                t.Commit();
            }

            if (fail == 0)
                UI_Status_Text.Text = $"✓ Copied {_selectedFilters.Count} filter(s) to {_selectedDestinations.Count} view(s) successfully!";
            else
                UI_Status_Text.Text = $"Copied with {success} success, {fail} failed.";

            UpdateDestPreview(); // Refresh to show newly added filters
        }

        #endregion

        #region Helpers

        private Dictionary<ViewType, string> ViewTypeMap = new Dictionary<ViewType, string>
        {
            { ViewType.FloorPlan, "[FLOOR]" }, { ViewType.CeilingPlan, "[CEIL]" }, { ViewType.ThreeD, "[3D]" },
            { ViewType.Section, "[SEC]" }, { ViewType.Elevation, "[EL]" }, { ViewType.DraftingView, "[DRAFT]" },
            { ViewType.AreaPlan, "[AREA]" }, { ViewType.Rendering, "[CAM]" }, { ViewType.Legend, "[LEG]" },
            { ViewType.EngineeringPlan, "[STR]" }, { ViewType.Walkthrough, "[WALK]" }
        };

        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

    // Helper Classes
    public class ViewItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public Autodesk.Revit.DB.View View { get; set; }
        private bool _isChecked;
        public bool IsChecked 
        { 
            get => _isChecked; 
            set { _isChecked = value; OnPropertyChanged(); } 
        }

        public ViewItem(string name, Autodesk.Revit.DB.View view)
        {
            Name = name;
            View = view;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class FilterItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public Element FilterElement { get; set; }
        private bool _isChecked;
        public bool IsChecked 
        { 
            get => _isChecked; 
            set { _isChecked = value; OnPropertyChanged(); } 
        }

        public FilterItem(string name, Element element)
        {
            Name = name;
            FilterElement = element;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
