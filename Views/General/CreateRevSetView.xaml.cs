using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using antiGGGravity.Models;

namespace antiGGGravity.Views.General
{
    public partial class CreateRevSetView : Window
    {
        private Document _doc;
        private ObservableCollection<RevisionViewModel> _allRevisions;
        private ObservableCollection<SheetViewModel> _matchedSheets;

        public CreateRevSetView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            _matchedSheets = new ObservableCollection<SheetViewModel>();
            UI_List_Sheets.ItemsSource = _matchedSheets;
            LoadData();
        }

        private void LoadData()
        {
            var revs = RevisionLogic.GetRevisions(_doc, includeIssued: true)
                .Select(r => new RevisionViewModel(r))
                .ToList();
            
            foreach (var rev in revs)
            {
                rev.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(RevisionViewModel.IsSelected))
                    {
                        UpdateDefaultName();
                        UpdateSheetList();
                    }
                };
            }

            _allRevisions = new ObservableCollection<RevisionViewModel>(revs);
            UI_List_Revisions.ItemsSource = _allRevisions;
        }

        private void UpdateDefaultName()
        {
            var selected = _allRevisions.Where(r => r.IsSelected).ToList();
            if (selected.Any())
            {
                UI_Txt_SetName.Text = string.Join(" & ", selected.Select(r => r.Label));
            }
            else
            {
                UI_Txt_SetName.Text = "";
            }
        }

        private void UpdateSheetList()
        {
            var selectedRevs = _allRevisions.Where(r => r.IsSelected).Select(r => r.Revision).ToList();
            
            if (!selectedRevs.Any())
            {
                _matchedSheets.Clear();
                UI_Txt_Status.Text = "Select revisions to see matching sheets.";
                return;
            }

            // Default to Match ANY as requested (logic refined by checkboxes)
            var sheets = RevisionLogic.GetRevisedSheets(_doc, selectedRevs, true)
                .Select(s => new SheetViewModel(s) { IsSelected = true })
                .ToList();

            _matchedSheets.Clear();
            foreach (var s in sheets) _matchedSheets.Add(s);
            
            UI_Txt_Status.Text = $"{_matchedSheets.Count} sheets matching selected revisions.";
            UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Create_Click(object sender, RoutedEventArgs e)
        {
            var finalSheets = _matchedSheets.Where(s => s.IsSelected).Select(s => s.Sheet).ToList();
            string setName = UI_Txt_SetName.Text;

            if (!finalSheets.Any())
            {
                UI_Txt_Status.Text = "Please ensure at least one sheet is selected.";
                UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (string.IsNullOrWhiteSpace(setName))
            {
                UI_Txt_Status.Text = "Please provide a name for the sheet set.";
                UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            try
            {
                using (Transaction t = new Transaction(_doc, "Create Revision Sheet Set"))
                {
                    t.Start();
                    RevisionLogic.CreateRevisionSheetSet(_doc, setName, finalSheets);
                    t.Commit();
                }

                MessageBox.Show($"Successfully created sheet set '{setName}' with {finalSheets.Count} sheets.", "Success");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating sheet set: {ex.Message}", "Error");
            }
        }
    }
}
