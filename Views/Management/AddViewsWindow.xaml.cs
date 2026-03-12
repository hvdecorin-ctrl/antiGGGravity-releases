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

        public class ViewWrapper
        {
            public View View { get; set; }
            public string Name => View.Name;
            public string ViewTypeString => View.ViewType.ToString();
        }

        public AddViewsWindow(List<View> unsheetedViews)
        {
            InitializeComponent();
            
            _allWrappers = unsheetedViews
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .Select(v => new ViewWrapper { View = v })
                .ToList();

            ViewsListBox.ItemsSource = _allWrappers;
            UpdateStatus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(filter))
            {
                ViewsListBox.ItemsSource = _allWrappers;
            }
            else
            {
                ViewsListBox.ItemsSource = _allWrappers.Where(w => 
                    w.Name.ToLower().Contains(filter) || 
                    w.ViewTypeString.ToLower().Contains(filter))
                    .ToList();
            }
            UpdateStatus();
        }

        private void ViewsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewsListBox.SelectedItem != null)
            {
                AddButton_Click(sender, null);
            }
        }

        private void UpdateStatus()
        {
            int count = ViewsListBox.SelectedItems.Count;
            int total = ViewsListBox.Items.Count;
            UI_Status.Text = count > 0 ? $"{count} selected" : $"Total {total} views found";
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatus();
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
            
            if (SelectedViews.Count == 0)
            {
                UI_Status.Text = "Please select at least one view.";
                return;
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
