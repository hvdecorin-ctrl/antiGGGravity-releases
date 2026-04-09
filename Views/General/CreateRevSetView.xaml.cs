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

        public CreateRevSetView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
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
                        UpdateDefaultName();
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

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Create_Click(object sender, RoutedEventArgs e)
        {
            var selectedRevs = _allRevisions.Where(r => r.IsSelected).Select(r => r.Revision).ToList();
            string setName = UI_Txt_SetName.Text;

            if (!selectedRevs.Any())
            {
                UI_Txt_Status.Text = "Please select at least one revision.";
                UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (string.IsNullOrWhiteSpace(setName))
            {
                UI_Txt_Status.Text = "Please provide a name for the sheet set.";
                UI_Txt_Status.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            bool matchAny = UI_Radio_Any.IsChecked == true;
            var matchedSheets = RevisionLogic.GetRevisedSheets(_doc, selectedRevs, matchAny);

            if (!matchedSheets.Any())
            {
                MessageBox.Show("No sheets found matching the selected revisions.", "No Matching Sheets");
                return;
            }

            try
            {
                using (Transaction t = new Transaction(_doc, "Create Revision Sheet Set"))
                {
                    t.Start();
                    RevisionLogic.CreateRevisionSheetSet(_doc, setName, matchedSheets);
                    t.Commit();
                }

                MessageBox.Show($"Successfully created sheet set '{setName}' with {matchedSheets.Count} sheets.", "Success");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating sheet set: {ex.Message}", "Error");
            }
        }
    }
}
