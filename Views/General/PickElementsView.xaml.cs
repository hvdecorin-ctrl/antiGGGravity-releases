using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public enum FilterMode { SingleCategory, All3D, All2D, SpecificCategory }

    public partial class PickElementsView : Window
    {
        private List<CategoryItem> _allCategories;
        
        public CategoryItem SelectedCategory => UI_List_Categories.SelectedItem as CategoryItem;
        public FilterMode Mode { get; private set; } = FilterMode.SingleCategory;
        public BuiltInCategory QuickCategory { get; private set; }

        public PickElementsView(IEnumerable<Category> categories)
        {
            InitializeComponent();
            _allCategories = categories.Select(c => new CategoryItem { Name = c.Name, Category = c }).OrderBy(c => c.Name).ToList();
            UI_List_Categories.ItemsSource = _allCategories;
            LoadCustomDefinitions();
        }

        private void LoadCustomDefinitions()
        {
            string cfgPath = GetConfigPath();
            if (File.Exists(cfgPath))
            {
                try
                {
                    string json = File.ReadAllText(cfgPath);
                    List<string> customs = JsonSerializer.Deserialize<List<string>>(json);
                    
                    // Clear existing (except the Add button if we re-render later, but for now we just add them)
                    // The Add button is defined in XAML. We will insert before it.
                    if (customs != null)
                    {
                        foreach(var catName in customs)
                        {
                            AddCustomButton(catName);
                        }
                    }
                }
                catch { }
            }
        }

        private void AddCustomButton(string categoryName)
        {
            Button btn = new Button
            {
                Content = categoryName,
                Tag = categoryName,
                Style = FindResource("PremiumPrimaryButtonStyle") as Style,
                Margin = new Thickness(0, 0, 5, 5),
                Padding = new Thickness(10, 4, 10, 4), // Left,Top,Right,Bottom
                FontSize = 12,
                Height = 28
            };
            btn.Click += UI_Btn_CustomPick_Click;
            btn.MouseRightButtonUp += UI_Btn_CustomPick_RightClick;
            
            // Insert before the last item (The 'Add Custom' button)
            UI_Panel_CustomPicks.Children.Insert(UI_Panel_CustomPicks.Children.Count - 1, btn);
        }

        private void UI_Btn_CustomPick_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var result = MessageBox.Show($"Remove '{tag}' from custom favorites?", "Remove Custom Category", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // Remove from UI
                    UI_Panel_CustomPicks.Children.Remove(btn);

                    // Remove from JSON
                    string cfgPath = GetConfigPath();
                    if (File.Exists(cfgPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(cfgPath);
                            List<string> customs = JsonSerializer.Deserialize<List<string>>(json);
                            if (customs != null && customs.Contains(tag))
                            {
                                customs.Remove(tag);
                                File.WriteAllText(cfgPath, JsonSerializer.Serialize(customs));
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Autodesk", "Revit", "Addins", "antiGGGravity");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "PickElementsFavorites.json");
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
            // UI_Status_Text has been removed from the UI
        }

        private void UI_List_Categories_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // If the user double clicks on an item, we instantly trigger the pick
            if (SelectedCategory != null)
            {
                Mode = FilterMode.SingleCategory;
                ActionPick();
            }
        }

        private void UI_Btn_QuickPick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                switch (tag)
                {
                    case "All3D": Mode = FilterMode.All3D; break;
                    case "All2D": Mode = FilterMode.All2D; break;
                    case "Foundation": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_StructuralFoundation; break;
                    case "Wall": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_Walls; break;
                    case "Floor": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_Floors; break;
                    case "Beam": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_StructuralFraming; break;
                    case "Column": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_StructuralColumns; break;
                    case "Roof": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_Roofs; break;
                    case "Rebar": Mode = FilterMode.SpecificCategory; QuickCategory = BuiltInCategory.OST_Rebar; break;
                }
                ActionPick();
            }
        }

        private void UI_Btn_CustomPick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                // Verify the category exists in the document list
                var catItem = _allCategories.FirstOrDefault(c => c.Name == tag);
                if (catItem != null)
                {
                    Mode = FilterMode.SingleCategory;
                    // Cheat by selecting it then triggering action
                    UI_List_Categories.SelectedItem = catItem;
                    ActionPick();
                }
                else
                {
                    MessageBox.Show($"Category '{tag}' is not available in the current project.", "Pick Elements");
                }
            }
        }

        private void UI_Btn_AddCustom_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("Please select a category from the 'All Categories' list first to add to Custom Favorites.", "Pick Elements");
                return;
            }

            string newFav = SelectedCategory.Name;

            // Load existing
            List<string> customs = new List<string>();
            string cfgPath = GetConfigPath();
            if (File.Exists(cfgPath))
            {
                try
                {
                    string json = File.ReadAllText(cfgPath);
                    customs = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch { }
            }

            if (!customs.Contains(newFav))
            {
                customs.Add(newFav);
                try
                {
                    File.WriteAllText(cfgPath, JsonSerializer.Serialize(customs));
                    AddCustomButton(newFav);
                }
                catch { }
            }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void ActionPick()
        {
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
