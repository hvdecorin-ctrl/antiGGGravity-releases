using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class JoinUnjoinView : Window
    {
        public ObservableCollection<CheckedListItem> LeftCategories { get; set; }
        public ObservableCollection<CheckedListItem> RightCategories { get; set; }
        public bool IsJoinOperation => UI_Radio_Join.IsChecked == true;

        public JoinUnjoinView(IEnumerable<Category> categories)
        {
            InitializeComponent();
            
            var sortedCats = categories.OrderBy(c => c.Name).ToList();
            
            LeftCategories = new ObservableCollection<CheckedListItem>(
                sortedCats.Select(c => new CheckedListItem { Name = c.Name, Category = c }));
            
            RightCategories = new ObservableCollection<CheckedListItem>(
                sortedCats.Select(c => new CheckedListItem { Name = c.Name, Category = c }));

            UI_List_Left.ItemsSource = LeftCategories;
            UI_List_Right.ItemsSource = RightCategories;
        }

        private void UI_Btn_LeftAll_Click(object sender, RoutedEventArgs e) { foreach (var item in LeftCategories) item.IsChecked = true; }
        private void UI_Btn_LeftNone_Click(object sender, RoutedEventArgs e) { foreach (var item in LeftCategories) item.IsChecked = false; }
        private void UI_Btn_RightAll_Click(object sender, RoutedEventArgs e) { foreach (var item in RightCategories) item.IsChecked = true; }
        private void UI_Btn_RightNone_Click(object sender, RoutedEventArgs e) { foreach (var item in RightCategories) item.IsChecked = false; }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            if (!LeftCategories.Any(c => c.IsChecked) || !RightCategories.Any(c => c.IsChecked))
            {
                MessageBox.Show("Please select at least one category from each panel.", "Join/Unjoin Advance");
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
