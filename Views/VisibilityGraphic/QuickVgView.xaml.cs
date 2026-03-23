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
                
                var states = QuickVgLogic.GetCategoryStates(_activeView);
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
            var searchText = CoordSearchBox.Text?.ToLower() ?? "";
            var view = CollectionViewSource.GetDefaultView(CoordListBox.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    var cat = item as CategoryVisibilityModel;
                    return string.IsNullOrEmpty(searchText) || cat.Name.ToLower().Contains(searchText);
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
                QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()));
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

            QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()));
        }

        private void StructListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement el && el.DataContext is CategoryVisibilityModel item)
            {
                var existing = _structCategories.FirstOrDefault(c => c.Id == item.Id);
                if (existing != null)
                {
                    _structCategories.Remove(existing);
                    QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()));
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
                    
                    QuickVgLogic.SaveCustomCategoryIds(_structCategories.Select(c => c.Id.GetIdValue()));
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
