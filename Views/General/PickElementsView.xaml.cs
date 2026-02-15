using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class PickElementsView : Window
    {
        private List<CategoryItem> _allCategories;
        public CategoryItem SelectedCategory => UI_List_Categories.SelectedItem as CategoryItem;

        public PickElementsView(IEnumerable<Category> categories)
        {
            InitializeComponent();
            _allCategories = categories.Select(c => new CategoryItem { Name = c.Name, Category = c }).OrderBy(c => c.Name).ToList();
            UI_List_Categories.ItemsSource = _allCategories;
        }

        private void UI_Text_Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Text_Filter.Text.ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                UI_List_Categories.ItemsSource = _allCategories;
            }
            else
            {
                UI_List_Categories.ItemsSource = _allCategories.Where(c => c.Name.ToLower().Contains(filter)).ToList();
            }
        }

        private void UI_List_Categories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedCategory != null) UI_Status_Text.Text = $"Ready to pick {SelectedCategory.Name}";
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("Please select a category.", "Pick Elements");
                return;
            }
            DialogResult = true;
            Close();
        }
    }

    public class CategoryItem
    {
        public string Name { get; set; }
        public Category Category { get; set; }
    }
}
