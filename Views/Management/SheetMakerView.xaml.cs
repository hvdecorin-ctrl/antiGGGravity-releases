using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Runtime.CompilerServices;

namespace antiGGGravity.Views.Management
{
    public partial class SheetMakerView : Window
    {
        private ExternalCommandData _commandData;
        private Document _doc;
        private List<SheetItem> _allSheets;

        public SheetMakerView(ExternalCommandData commandData)
        {
            InitializeComponent();
            _commandData = commandData;
            _doc = commandData.Application.ActiveUIDocument.Document;
            
            LoadTitleBlocks();
            LoadSheets();
            UpdatePreview();
        }

        private void LoadTitleBlocks()
        {
            var titleBlocks = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();

            UI_Combo_TitleBlocks.ItemsSource = titleBlocks;
            if (titleBlocks.Any()) UI_Combo_TitleBlocks.SelectedIndex = 0;
        }

        private void LoadSheets()
        {
            _allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .Select(s => new SheetItem(s))
                .OrderBy(s => s.Number)
                .ToList();

            UI_Grid_Sheets.ItemsSource = _allSheets;
        }

        private void UI_Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_Search.Text.ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                UI_Grid_Sheets.ItemsSource = _allSheets;
                return;
            }
            var filtered = _allSheets.Where(s => 
                (s.Name != null && s.Name.ToLower().Contains(filter)) || 
                (s.Number != null && s.Number.ToLower().Contains(filter)))
                .ToList();
            UI_Grid_Sheets.ItemsSource = filtered;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Process_Click(object sender, RoutedEventArgs e)
        {
            // 1. Check for Creations
            int createCount = 0;
            if (int.TryParse(UI_Txt_Count.Text, out int count) && count > 0)
            {
                createCount = count;
            }

            // 2. Check for Updates
            var sheetsToUpdate = _allSheets.Where(s => s.IsModified || s.IsNumberModified).ToList();

            using (TransactionGroup tg = new TransactionGroup(_doc, "Sheet Maker: Process"))
            {
                tg.Start();

                // Part A: Update existing
                if (sheetsToUpdate.Any())
                {
                    using (Transaction t = new Transaction(_doc, "Update Sheets"))
                    {
                        t.Start();
                        foreach (var item in sheetsToUpdate)
                        {
                            try
                            {
                                if (item.IsNumberModified) item.Sheet.SheetNumber = item.Number;
                                if (item.IsModified) item.Sheet.Name = item.Name;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error updating sheet {item.OriginalNumber}: {ex.Message}");
                            }
                        }
                        t.Commit();
                    }
                }

                // Part B: Create new
                if (createCount > 0)
                {
                    FamilySymbol selectedTitleBlock = UI_Combo_TitleBlocks.SelectedItem as FamilySymbol;
                    ElementId titleBlockId = selectedTitleBlock?.Id ?? ElementId.InvalidElementId;
                    string baseName = UI_Txt_SheetName.Text;
                    string currentNo = UI_Txt_Rule.Text;

                    using (Transaction t = new Transaction(_doc, "Create Sheets"))
                    {
                        t.Start();
                        for (int i = 0; i < createCount; i++)
                        {
                            try
                            {
                                ViewSheet newSheet = ViewSheet.Create(_doc, titleBlockId);
                                newSheet.Name = baseName;
                                newSheet.SheetNumber = currentNo;
                                currentNo = GetNextSheetNumber(currentNo);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error creating sheet {currentNo}: {ex.Message}");
                                break;
                            }
                        }
                        t.Commit();
                    }
                }

                tg.Assimilate();
            }

            // Refresh UI
            LoadSheets();
            UI_Txt_Count.Text = "0"; // Reset count after creation
            MessageBox.Show("Sheets processed successfully.", "Sheet Maker");
        }

        private void UI_Btn_Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allSheets.Where(s => s.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select at least one sheet to delete.", "Delete Sheets");
                return;
            }

            var safeSheets = selectedItems.Where(s => IsSafeToDelete(s.Sheet)).ToList();
            int nonSafeCount = selectedItems.Count - safeSheets.Count;

            if (!safeSheets.Any())
            {
                MessageBox.Show($"All selected sheets ({selectedItems.Count}) contain model views (Plans/Sections) and cannot be deleted.", "Safety Rule");
                return;
            }

            string msg = $"Are you sure you want to delete {safeSheets.Count} sheets?";
            if (nonSafeCount > 0) msg += $"\n\nNote: {nonSafeCount} sheets were skipped because they contain model views (Plans/Sections).";

            var result = MessageBox.Show(msg, "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using (Transaction t = new Transaction(_doc, "Delete Sheets"))
            {
                t.Start();
                foreach (var item in safeSheets)
                {
                    try { _doc.Delete(item.Sheet.Id); } catch { }
                }
                t.Commit();
            }

            LoadSheets();
            MessageBox.Show($"Successfully deleted {safeSheets.Count} sheets.", "Success");
        }

        private bool IsSafeToDelete(ViewSheet sheet)
        {
            // Allow if empty or only contains Legends/Schedules
            var viewportIds = sheet.GetAllViewports();
            foreach (ElementId vpId in viewportIds)
            {
                Viewport vp = _doc.GetElement(vpId) as Viewport;
                if (vp == null) continue;
                View v = _doc.GetElement(vp.ViewId) as View;
                if (v != null && v.ViewType != ViewType.Legend)
                {
                    return false; // Found a model view (Plan, Section, etc)
                }
            }
            
            // Schedules (ScheduleSheetInstance) are allowed
            return true;
        }

        private void UI_Check_All_Click(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb == null) return;
            foreach (var item in _allSheets)
            {
                item.IsSelected = cb.IsChecked == true;
            }
        }

        private void UI_Txt_Rule_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (UI_Txt_Preview == null) return;
            
            string start = UI_Txt_Rule.Text;
            if (string.IsNullOrEmpty(start))
            {
                UI_Txt_Preview.Text = "Preview: ...";
                return;
            }

            string next1 = GetNextSheetNumber(start);
            string next2 = GetNextSheetNumber(next1);
            UI_Txt_Preview.Text = $"Preview: {start}, {next1}, {next2}...";
        }

        private string GetNextSheetNumber(string currentNo)
        {
            if (string.IsNullOrEmpty(currentNo)) return "1";
            
            var match = System.Text.RegularExpressions.Regex.Match(currentNo, @"(.*?)([0-9]+)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string numStr = match.Groups[2].Value;
                if (long.TryParse(numStr, out long val))
                {
                    return prefix + (val + 1).ToString().PadLeft(numStr.Length, '0');
                }
            }
            return currentNo + "-1";
        }
    }

    public class SheetItem : INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }
        public string OriginalNumber { get; }
        public string OriginalName { get; }

        private string _number;
        public string Number 
        { 
            get => _number;
            set { _number = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name 
        { 
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsModified => Name != OriginalName;
        public bool IsNumberModified => Number != OriginalNumber;

        public SheetItem(ViewSheet sheet)
        {
            Sheet = sheet;
            _number = sheet.SheetNumber;
            _name = sheet.Name;
            OriginalNumber = sheet.SheetNumber;
            OriginalName = sheet.Name;
            IsSelected = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
