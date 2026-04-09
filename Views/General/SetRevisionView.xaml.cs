using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.Models;

namespace antiGGGravity.Views.General
{
    public partial class SetRevisionView : Window
    {
        private Document _doc;
        private ObservableCollection<RevisionViewModel> _allRevisions;
        private ObservableCollection<SheetViewModel> _allSheets;
        private ObservableCollection<SheetViewModel> _filteredSheets;

        public SetRevisionView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSelectionSources();
        }

        private void LoadData()
        {
            // Load non-issued revisions
            var revs = RevisionLogic.GetRevisions(_doc, includeIssued: false)
                .Select(r => new RevisionViewModel(r))
                .ToList();
            _allRevisions = new ObservableCollection<RevisionViewModel>(revs);
            UI_List_Revisions.ItemsSource = _allRevisions;

            // Load all sheets
            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetViewModel(s))
                .ToList();
            _allSheets = new ObservableCollection<SheetViewModel>(sheets);
            _filteredSheets = new ObservableCollection<SheetViewModel>(_allSheets);
            UI_List_Sheets.ItemsSource = _filteredSheets;
        }

        private void LoadSelectionSources()
        {
            var sources = new List<SelectionSourceViewModel>();
            sources.Add(new SelectionSourceViewModel { Type = SelectionSourceType.Manual, Name = "<Manual Selection>" });

            var sets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .OrderBy(s => s.Name);
            foreach (var set in sets)
                sources.Add(new SelectionSourceViewModel { Type = SelectionSourceType.Set, Object = set, Name = $"Set: {set.Name}" });

            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => s.Definition.CategoryId == new ElementId(BuiltInCategory.OST_Sheets) && !s.IsTemplate)
                .OrderBy(s => s.Name);
            foreach (var sch in schedules)
                sources.Add(new SelectionSourceViewModel { Type = SelectionSourceType.Schedule, Object = sch, Name = $"Schedule: {sch.Name}" });

            UI_Combo_Source.ItemsSource = sources;
            UI_Combo_Source.SelectedIndex = 0;
        }

        private void UI_Combo_Source_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_Combo_Source.SelectedItem is SelectionSourceViewModel source)
            {
                // Reset all selections first
                foreach (var s in _allSheets) s.IsSelected = false;

                switch (source.Type)
                {
                    case SelectionSourceType.Manual:
                        _filteredSheets = new ObservableCollection<SheetViewModel>(_allSheets);
                        break;

                    case SelectionSourceType.Set:
                        var set = source.Object as ViewSheetSet;
                        var setSheetIds = new HashSet<ElementId>();
                        foreach (View v in set.Views) if (v is ViewSheet) setSheetIds.Add(v.Id);
                        
                        var setSheets = _allSheets.Where(vm => setSheetIds.Contains(vm.Sheet.Id)).ToList();
                        foreach (var vm in setSheets) vm.IsSelected = true;
                        _filteredSheets = new ObservableCollection<SheetViewModel>(setSheets);
                        break;

                    case SelectionSourceType.Schedule:
                        var sch = source.Object as ViewSchedule;
                        var ordered = PrintLogic.OrderSheetsBySchedule(sch, _allSheets.Select(vm => vm.Sheet));
                        var schSheetIds = new HashSet<ElementId>(ordered.Select(s => s.Id));
                        
                        var schSheets = _allSheets.Where(vm => schSheetIds.Contains(vm.Sheet.Id)).ToList();
                        foreach (var vm in schSheets) vm.IsSelected = true;
                        _filteredSheets = new ObservableCollection<SheetViewModel>(schSheets);
                        break;
                }
                UI_List_Sheets.ItemsSource = _filteredSheets;
                UI_List_Sheets.Items.Refresh();
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            int selectedCount = _allSheets.Count(s => s.IsSelected);
            UI_Txt_Status.Text = $"Sheets selected: {selectedCount}";
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Check_AllRevs_Checked(object sender, RoutedEventArgs e) => ToggleAllRevs(true);
        private void UI_Check_AllRevs_Unchecked(object sender, RoutedEventArgs e) => ToggleAllRevs(false);
        private void ToggleAllRevs(bool isSelected) { if (_allRevisions != null) foreach (var r in _allRevisions) r.IsSelected = isSelected; UI_List_Revisions.Items.Refresh(); }

        private void UI_Check_AllSheets_Checked(object sender, RoutedEventArgs e) => ToggleAllSheets(true);
        private void UI_Check_AllSheets_Unchecked(object sender, RoutedEventArgs e) => ToggleAllSheets(false);
        private void ToggleAllSheets(bool isSelected) { if (_filteredSheets != null) foreach (var s in _filteredSheets) s.IsSelected = isSelected; UI_List_Sheets.Items.Refresh(); UpdateStatus(); }

        private void UI_Txt_SearchRevs_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_SearchRevs.Text.ToLower();
            UI_List_Revisions.ItemsSource = string.IsNullOrWhiteSpace(filter) ? _allRevisions : new ObservableCollection<RevisionViewModel>(_allRevisions.Where(r => r.Label.ToLower().Contains(filter)));
        }

        private void UI_Txt_SearchSheets_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_SearchSheets.Text.ToLower();
            _filteredSheets = string.IsNullOrWhiteSpace(filter) ? new ObservableCollection<SheetViewModel>(_allSheets) : new ObservableCollection<SheetViewModel>(_allSheets.Where(s => s.DisplayName.ToLower().Contains(filter)));
            UI_List_Sheets.ItemsSource = _filteredSheets;
            UI_List_Sheets.Items.Refresh();
        }

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            var selectedRevs = _allRevisions.Where(r => r.IsSelected).Select(r => r.Revision).ToList();
            var selectedSheets = _allSheets.Where(s => s.IsSelected).Select(s => s.Sheet).ToList();

            if (!selectedRevs.Any() || !selectedSheets.Any())
            {
                UI_Txt_Status.Text = "Please select at least one revision and one sheet.";
                UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            using (Transaction t = new Transaction(_doc, "Set Revisions on Sheets"))
            {
                t.Start();
                RevisionLogic.SetSheetRevisions(selectedSheets, selectedRevs);
                t.Commit();
            }

            MessageBox.Show($"Successfully applied {selectedRevs.Count} revisions to {selectedSheets.Count} sheets.", "Success");
            Close();
        }
    }
}
