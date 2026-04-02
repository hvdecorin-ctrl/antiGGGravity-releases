using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.General;

namespace antiGGGravity.Views.General
{
    public partial class JoinUnjoinView : Window
    {
        // Structural default items (red section) — now Observable to support dynamic promotion
        private ObservableCollection<CheckedListItem> _leftDefaults;
        private ObservableCollection<CheckedListItem> _rightDefaults;

        // Project category items (green section) — master + filtered
        private List<CheckedListItem> _allLeftProject;
        private List<CheckedListItem> _allRightProject;

        public ObservableCollection<CheckedListItem> LeftProjectFiltered { get; set; }
        public ObservableCollection<CheckedListItem> RightProjectFiltered { get; set; }

        public bool IsJoinOperation => UI_Radio_Join.IsChecked == true;

        // External Event for modeless operation
        private ExternalEvent _externalEvent;
        private JoinUnjoinHandler _handler;

        // Structural/Core priority list matching original Python code
        private static readonly HashSet<string> DefaultCategoryNames = new HashSet<string>
        {
            "Walls",
            "Floors",
            "Structural Framing",
            "Structural Columns",
            "Structural Foundations",
            "Generic Models",
            "Roofs"
        };

        public JoinUnjoinView(ExternalCommandData commandData)
        {
            InitializeComponent();

            // Create external event handler for modeless operation
            _handler = new JoinUnjoinHandler(this);
            _externalEvent = ExternalEvent.Create(_handler);

            var doc = commandData.Application.ActiveUIDocument.Document;
            var categories = doc.Settings.Categories.Cast<Category>()
                .Where(c => c.CategoryType == Autodesk.Revit.DB.CategoryType.Model).ToList();

            var sortedCats = categories.OrderBy(c => c.Name).ToList();

            // Split into defaults and project categories
            var defaults = sortedCats.Where(c => DefaultCategoryNames.Contains(c.Name)).ToList();
            var project = sortedCats.Where(c => !DefaultCategoryNames.Contains(c.Name)).ToList();

            // LEFT defaults (red) — unchecked by default
            _leftDefaults = new ObservableCollection<CheckedListItem>(defaults.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = true, IsChecked = false
            }));

            // RIGHT defaults (red) — unchecked by default
            _rightDefaults = new ObservableCollection<CheckedListItem>(defaults.Select(c => new CheckedListItem
            {
                Name = c.Name, Category = c, IsDefault = true, IsChecked = false
            }));

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

        // --- Category Management (Double-click to Promote/Demote) ---

        private void UI_ProjectLeft_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                if (sender is FrameworkElement fe && fe.DataContext is CheckedListItem item)
                {
                    _allLeftProject.Remove(item);
                    LeftProjectFiltered.Remove(item);
                    item.IsDefault = true;
                    _leftDefaults.Add(item);
                }
            }
        }

        private void UI_ProjectRight_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                if (sender is FrameworkElement fe && fe.DataContext is CheckedListItem item)
                {
                    _allRightProject.Remove(item);
                    RightProjectFiltered.Remove(item);
                    item.IsDefault = true;
                    _rightDefaults.Add(item);
                }
            }
        }

        private void UI_DefaultLeft_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                if (sender is FrameworkElement fe && fe.DataContext is CheckedListItem item)
                {
                    _leftDefaults.Remove(item);
                    item.IsDefault = false;
                    InsertSorted(_allLeftProject, item);
                    ApplyFilter(UI_Search_Left.Text, _allLeftProject, LeftProjectFiltered);
                }
            }
        }

        private void UI_DefaultRight_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                if (sender is FrameworkElement fe && fe.DataContext is CheckedListItem item)
                {
                    _rightDefaults.Remove(item);
                    item.IsDefault = false;
                    InsertSorted(_allRightProject, item);
                    ApplyFilter(UI_Search_Right.Text, _allRightProject, RightProjectFiltered);
                }
            }
        }

        private void InsertSorted(List<CheckedListItem> list, CheckedListItem item)
        {
            int index = list.BinarySearch(item, Comparer<CheckedListItem>.Create((a, b) => string.Compare(a.Name, b.Name)));
            if (index < 0) index = ~index;
            list.Insert(index, item);
        }

        // --- Select All (affects only the Pre-defined/Structural Default list) ---
        private void UI_Check_LeftAll_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && _leftDefaults != null)
            {
                bool isChecked = cb.IsChecked == true;
                foreach (var item in _leftDefaults)
                {
                    item.IsChecked = isChecked;
                }
            }
        }

        private void UI_Check_RightAll_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && _rightDefaults != null)
            {
                bool isChecked = cb.IsChecked == true;
                foreach (var item in _rightDefaults)
                {
                    item.IsChecked = isChecked;
                }
            }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(SessionLog);
            }
            catch {}
        }

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            bool hasLeft = _leftDefaults.Any(c => c.IsChecked);
            bool hasRight = _rightDefaults.Any(c => c.IsChecked);

            if (!hasLeft || !hasRight)
            {
                MessageBox.Show("Please select at least one category from each panel header (Pre-defined list).", "Join/Unjoin Advance");
                return;
            }

            UI_Status_Text.Text = "⏳ Processing...";
            _externalEvent.Raise();
        }

        // Expose ONLY the Pre-defined categories for the handler
        public IReadOnlyList<CheckedListItem> AllLeftItems => _leftDefaults.ToList();

        public IReadOnlyList<CheckedListItem> AllRightItems => _rightDefaults.ToList();

        public string SessionLog { get; set; } = "No operations performed yet.";
    }
}
