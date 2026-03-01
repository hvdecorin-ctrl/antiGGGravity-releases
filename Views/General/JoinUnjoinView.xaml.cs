using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class JoinUnjoinView : Window
    {
        // Structural default items (red section) — shared instances per side
        private List<CheckedListItem> _leftDefaults;
        private List<CheckedListItem> _rightDefaults;

        // Project category items (green section) — master + filtered
        private List<CheckedListItem> _allLeftProject;
        private List<CheckedListItem> _allRightProject;

        public ObservableCollection<CheckedListItem> LeftProjectFiltered { get; set; }
        public ObservableCollection<CheckedListItem> RightProjectFiltered { get; set; }

        public bool IsJoinOperation => UI_Radio_Join.IsChecked == true;

        // Structural/Core priority list matching original Python code
        private static readonly HashSet<string> DefaultCategoryNames = new HashSet<string>
        {
            "Walls",
            "Floors",
            "Structural Framing",
            "Columns",
            "Structural Foundations",
            "Generic Models",
            "Roofs"
        };

        public JoinUnjoinView(IEnumerable<Category> categories)
        {
            InitializeComponent();

            var sortedCats = categories.OrderBy(c => c.Name).ToList();

            // Split into defaults and project categories
            var defaults = sortedCats.Where(c => DefaultCategoryNames.Contains(c.Name)).ToList();
            var project = sortedCats.Where(c => !DefaultCategoryNames.Contains(c.Name)).ToList();

            // LEFT defaults (red) — pre-checked
            _leftDefaults = defaults.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = true, IsChecked = true
            }).ToList();

            // RIGHT defaults (red) — pre-checked
            _rightDefaults = defaults.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = true, IsChecked = true
            }).ToList();

            // LEFT project (green) — unchecked
            _allLeftProject = project.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = false, IsChecked = false
            }).ToList();

            // RIGHT project (green) — unchecked
            _allRightProject = project.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = false, IsChecked = false
            }).ToList();

            // Bind defaults to ItemsControls
            UI_Defaults_Left.ItemsSource = _leftDefaults;
            UI_Defaults_Right.ItemsSource = _rightDefaults;

            // Bind filtered project categories to ListBoxes
            LeftProjectFiltered = new ObservableCollection<CheckedListItem>(_allLeftProject);
            RightProjectFiltered = new ObservableCollection<CheckedListItem>(_allRightProject);
            UI_List_Left.ItemsSource = LeftProjectFiltered;
            UI_List_Right.ItemsSource = RightProjectFiltered;
        }

        // --- Search Filtering (project categories only) ---
        private void UI_Search_Left_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(UI_Search_Left.Text, _allLeftProject, LeftProjectFiltered);
        }

        private void UI_Search_Right_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(UI_Search_Right.Text, _allRightProject, RightProjectFiltered);
        }

        private void ApplyFilter(string query, List<CheckedListItem> source, ObservableCollection<CheckedListItem> target)
        {
            var filtered = string.IsNullOrWhiteSpace(query)
                ? source
                : source.Where(i => i.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            target.Clear();
            foreach (var item in filtered)
                target.Add(item);
        }

        // --- Select All / None (affects both defaults + project) ---
        private void UI_Btn_LeftAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _leftDefaults) item.IsChecked = true;
            foreach (var item in _allLeftProject) item.IsChecked = true;
        }
        private void UI_Btn_LeftNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _leftDefaults) item.IsChecked = false;
            foreach (var item in _allLeftProject) item.IsChecked = false;
        }
        private void UI_Btn_RightAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _rightDefaults) item.IsChecked = true;
            foreach (var item in _allRightProject) item.IsChecked = true;
        }
        private void UI_Btn_RightNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _rightDefaults) item.IsChecked = false;
            foreach (var item in _allRightProject) item.IsChecked = false;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            bool hasLeft = _leftDefaults.Any(c => c.IsChecked) || _allLeftProject.Any(c => c.IsChecked);
            bool hasRight = _rightDefaults.Any(c => c.IsChecked) || _allRightProject.Any(c => c.IsChecked);

            if (!hasLeft || !hasRight)
            {
                MessageBox.Show("Please select at least one category from each panel.", "Join/Unjoin Advance");
                return;
            }
            DialogResult = true;
            Close();
        }

        // Expose ALL checked items (defaults + project) for the command
        public IReadOnlyList<CheckedListItem> AllLeftItems =>
            _leftDefaults.Concat(_allLeftProject).ToList();

        public IReadOnlyList<CheckedListItem> AllRightItems =>
            _rightDefaults.Concat(_allRightProject).ToList();
    }
}
