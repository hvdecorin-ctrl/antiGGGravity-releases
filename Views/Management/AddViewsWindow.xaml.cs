using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.Management
{
    public partial class AddViewsWindow : Window
    {
        public List<View> SelectedViews { get; private set; } = new List<View>();
        private List<ViewWrapper> _allWrappers;

        // Helper class for display
        private class ViewWrapper
        {
            public View View { get; set; }
            public override string ToString()
            {
                return $"{View.ViewType}: {View.Name}";
            }
        }

        public AddViewsWindow(List<View> unsheetedViews)
        {
            InitializeComponent();
            
            // Sort by Type then Name and store ALL wrappers
            _allWrappers = unsheetedViews
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .Select(v => new ViewWrapper { View = v })
                .ToList();

            ViewsListBox.ItemsSource = _allWrappers;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(filter))
            {
                ViewsListBox.ItemsSource = _allWrappers;
                return;
            }

            var filtered = _allWrappers.Where(w => 
                w.View.Name.ToLower().Contains(filter) || 
                w.View.ViewType.ToString().ToLower().Contains(filter))
                .ToList();
                
            ViewsListBox.ItemsSource = filtered;
        }

        private void ViewsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // If an item is actually selected (clicked), treat as Add
            if (ViewsListBox.SelectedItem != null)
            {
                AddButton_Click(sender, null);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ViewsListBox.SelectedItems)
            {
                if (item is ViewWrapper wrapper)
                {
                    SelectedViews.Add(wrapper.View);
                }
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
