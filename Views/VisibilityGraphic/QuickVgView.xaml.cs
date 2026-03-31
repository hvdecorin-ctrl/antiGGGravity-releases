using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Collections.ObjectModel;
using antiGGGravity.Commands.VisibilityGraphic;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.VisibilityGraphic
{
    public partial class QuickVgView : Window
    {
        private View _activeView;
        private ObservableCollection<CategoryVisibilityModel> _structCategories;
        private ObservableCollection<CategoryVisibilityModel> _coordCategories;
        private string _currentSlot = "A";

        private readonly ExternalEvent _externalEvent;
        private readonly QuickVgEventHandler _handler;
        private readonly UIApplication _uiApp;

        public QuickVgView(UIApplication uiApp, ExternalEvent externalEvent, QuickVgEventHandler handler)
        {
            InitializeComponent();
            _uiApp = uiApp;
            _externalEvent = externalEvent;
            _handler = handler;
            _activeView = uiApp.ActiveUIDocument.ActiveView;

            LoadCategories();
            
            this.Closed += (s, e) => 
            { 
                _activeView = null;
                StructListBox.ItemsSource = null;
                CoordListBox.ItemsSource = null;
            };
        }

        public void RefreshView(View view)
        {
            if (view == null) return;
            _activeView = view;
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                if (_activeView == null || !_activeView.IsValidObject) return;
                
                var states = QuickVgLogic.GetCategoryStates(_activeView, _currentSlot);
                _structCategories = new ObservableCollection<CategoryVisibilityModel>(states.Structural);
                _coordCategories = new ObservableCollection<CategoryVisibilityModel>(states.Coordinate);
                
                Dispatcher.Invoke(() =>
                {
                    StructListBox.ItemsSource = _structCategories;
                    CoordListBox.ItemsSource = _coordCategories;

                    var structView = CollectionViewSource.GetDefaultView(StructListBox.ItemsSource);
                    structView.SortDescriptions.Clear();
                    structView.SortDescriptions.Add(new System.ComponentModel.SortDescription("Name", System.ComponentModel.ListSortDirection.Ascending));

                    StructSearchBox_TextChanged(null, null);
                    CoordSearchBox_TextChanged(null, null);
                });
            }
            catch (System.Exception ex)
            {
                // Handle or log
            }
        }

        private void BtnCustomA_Click(object sender, RoutedEventArgs e)
        {
            SwitchSlot("A");
        }

        private void BtnCustomB_Click(object sender, RoutedEventArgs e)
        {
            SwitchSlot("B");
        }

        private void BtnCustomC_Click(object sender, RoutedEventArgs e)
        {
            SwitchSlot("C");
        }

        private void SwitchSlot(string slot)
        {
            if (_currentSlot == slot) return;
            _currentSlot = slot;
            UpdateTabVisuals();
            LoadCategories();
        }

        private void UpdateTabVisuals()
        {
            // Reset all buttons to inactive state
            var inactiveBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99));
            var activeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
            var accentBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x88, 0x55));

            BtnCustomA.FontWeight = FontWeights.Normal;
            BtnCustomA.Foreground = inactiveBrush;
            BtnCustomA.BorderBrush = System.Windows.Media.Brushes.Transparent;

            BtnCustomB.FontWeight = FontWeights.Normal;
            BtnCustomB.Foreground = inactiveBrush;
            BtnCustomB.BorderBrush = System.Windows.Media.Brushes.Transparent;

            BtnCustomC.FontWeight = FontWeights.Normal;
            BtnCustomC.Foreground = inactiveBrush;
            BtnCustomC.BorderBrush = System.Windows.Media.Brushes.Transparent;

            // Set current slot to active
            switch (_currentSlot)
            {
                case "A":
                    BtnCustomA.FontWeight = FontWeights.SemiBold;
                    BtnCustomA.Foreground = activeBrush;
                    BtnCustomA.BorderBrush = accentBrush;
                    break;
                case "B":
                    BtnCustomB.FontWeight = FontWeights.SemiBold;
                    BtnCustomB.Foreground = activeBrush;
                    BtnCustomB.BorderBrush = accentBrush;
                    break;
                case "C":
                    BtnCustomC.FontWeight = FontWeights.SemiBold;
                    BtnCustomC.Foreground = activeBrush;
                    BtnCustomC.BorderBrush = accentBrush;
                    break;
            }
        }

        private void StructSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var searchText = StructSearchBox.Text?.ToLower() ?? "";
            var view = CollectionViewSource.GetDefaultView(StructListBox.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    var cat = item as CategoryVisibilityModel;
                    return string.IsNullOrEmpty(searchText) || cat.Name.ToLower().Contains(searchText);
                };
            }
        }

        private void CoordSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyCoordFilters();
        }

        private void DisciplineFilterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyCoordFilters();
        }

        private VgDisciplineFilter GetSelectedDiscipline()
        {
            if (DisciplineFilterCombo == null) return VgDisciplineFilter.All;
            switch (DisciplineFilterCombo.SelectedIndex)
            {
                case 1: return VgDisciplineFilter.Architecture;
                case 2: return VgDisciplineFilter.Structure;
                case 3: return VgDisciplineFilter.Mechanical;
                case 4: return VgDisciplineFilter.Electrical;
                case 5: return VgDisciplineFilter.Piping;
                case 6: return VgDisciplineFilter.Infrastructure;
                default: return VgDisciplineFilter.All;
            }
        }

        private void ApplyCoordFilters()
        {
            var searchText = CoordSearchBox?.Text?.ToLower() ?? "";
            var discipline = GetSelectedDiscipline();
            var view = CollectionViewSource.GetDefaultView(CoordListBox?.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    var cat = item as CategoryVisibilityModel;
                    if (cat == null) return false;

                    // Text search filter
                    bool matchesSearch = string.IsNullOrEmpty(searchText) || cat.Name.ToLower().Contains(searchText);

                    // Discipline filter
                    bool matchesDiscipline = discipline == VgDisciplineFilter.All || 
                                             QuickVgLogic.GetDiscipline(cat.Id) == discipline;

                    return matchesSearch && matchesDiscipline;
                };
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void StructSelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ToggleStructVisibleItems(true);
        }

        private void StructSelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleStructVisibleItems(false);
        }

        private void CoordSelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ToggleCoordVisibleItems(true);
        }

        private void CoordSelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleCoordVisibleItems(false);
        }

        private void ToggleStructVisibleItems(bool state)
        {
            var view = CollectionViewSource.GetDefaultView(StructListBox.ItemsSource);
            if (view != null)
            {
                foreach (CategoryVisibilityModel item in view) { item.IsVisible = state; }
            }
        }

        private void ToggleCoordVisibleItems(bool state)
        {
            var view = CollectionViewSource.GetDefaultView(CoordListBox.ItemsSource);
            if (view != null)
            {
                foreach (CategoryVisibilityModel item in view) { item.IsVisible = state; }
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = CoordListBox.SelectedItems.Cast<CategoryVisibilityModel>().ToList();
            if (!selectedItems.Any()) return;

            bool changed = false;
            foreach (var item in selectedItems)
            {
                if (!_structCategories.Any(c => c.Id == item.Id))
                {
                    _structCategories.Add(new CategoryVisibilityModel 
                    { 
                        Name = item.Name, 
                        Id = item.Id, 
                        IsVisible = item.IsVisible 
                    });
                    changed = true;
                }
            }

            if (changed)
            {
                QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()), _currentSlot);
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = StructListBox.SelectedItems.Cast<CategoryVisibilityModel>().ToList();
            if (!selectedItems.Any()) return;

            foreach (var item in selectedItems)
            {
                var existing = _structCategories.FirstOrDefault(c => c.Id == item.Id);
                if (existing != null)
                {
                    _structCategories.Remove(existing);
                }
            }

            QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()), _currentSlot);
        }

        private void StructListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement el && el.DataContext is CategoryVisibilityModel item)
            {
                var existing = _structCategories.FirstOrDefault(c => c.Id == item.Id);
                if (existing != null)
                {
                    _structCategories.Remove(existing);
                    QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()), _currentSlot);
                    e.Handled = true;
                }
            }
        }

        private void CoordListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement el && el.DataContext is CategoryVisibilityModel item)
            {
                if (!_structCategories.Any(c => c.Id == item.Id))
                {
                    _structCategories.Add(new CategoryVisibilityModel 
                    { 
                        Name = item.Name, 
                        Id = item.Id, 
                        IsVisible = item.IsVisible 
                    });
                    
                    QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()), _currentSlot);
                    e.Handled = true;
                }
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_coordCategories == null || _structCategories == null || _activeView == null || !_activeView.IsValidObject) return;

                // Merge Both Lists: Create a unified set of instructions for Revit.
                var mergedDict = new Dictionary<ElementId, CategoryVisibilityModel>();
                
                // Add all settings from Coordinate side
                foreach (var c in _coordCategories)
                {
                    mergedDict[c.Id] = new CategoryVisibilityModel { Name = c.Name, Id = c.Id, IsVisible = c.IsVisible };
                }
                
                // Add/Overwrite settings from Structural side (takes precedence)
                foreach (var s in _structCategories)
                {
                    mergedDict[s.Id] = new CategoryVisibilityModel { Name = s.Name, Id = s.Id, IsVisible = s.IsVisible };
                }

                var clonedData = mergedDict.Values.ToList();

                _handler.TargetView = _activeView;
                _handler.CategoriesToApply = clonedData;
                _externalEvent.Raise();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to apply visibility: {ex.Message}");
            }
        }
    }
}
