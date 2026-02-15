using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.Management
{
    public partial class DuplicateSheetsView : Window
    {
        private ExternalCommandData _commandData;
        private Document _doc;
        private List<DuplicateSheetItem> _allSheets;

        public DuplicateSheetsView(ExternalCommandData commandData)
        {
            InitializeComponent();
            _commandData = commandData;
            _doc = commandData.Application.ActiveUIDocument.Document;
            LoadSheets();
        }

        private void LoadSheets()
        {
            _allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .Select(s => new DuplicateSheetItem(s))
                .OrderBy(s => s.Number)
                .ToList();

            UI_List_Sheets.ItemsSource = _allSheets;
        }

        private void UI_Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_Search.Text.ToLower();
            var filtered = _allSheets.Where(s => s.Name.ToLower().Contains(filter)).ToList();
            UI_List_Sheets.ItemsSource = filtered;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var selectedSheets = _allSheets.Where(s => s.IsSelected).ToList();
            if (!selectedSheets.Any())
            {
                MessageBox.Show("Please select at least one sheet to duplicate.", "Duplicate Sheets");
                return;
            }

            // Get Options
            ViewDuplicateOption viewOption = ViewDuplicateOption.Duplicate;
            if (UI_Radio_Detailing.IsChecked == true) viewOption = ViewDuplicateOption.WithDetailing;
            if (UI_Radio_Dependent.IsChecked == true) viewOption = ViewDuplicateOption.AsDependent;

            bool copyLegends = UI_Check_Legends.IsChecked == true;
            bool copySchedules = UI_Check_Schedules.IsChecked == true;
            string suffix = UI_Txt_Suffix.Text;

            using (Transaction t = new Transaction(_doc, "Duplicate Sheets"))
            {
                t.Start();
                int count = 0;
                foreach (var item in selectedSheets)
                {
                    try
                    {
                        DuplicateSheet(item.Sheet, viewOption, copyLegends, copySchedules, suffix);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        // Log error?
                    }
                }
                t.Commit();
                MessageBox.Show($"Successfully duplicated {count} sheets.", "Success");
                Close();
            }
        }

        private void DuplicateSheet(ViewSheet sourceSheet, ViewDuplicateOption viewOption, bool copyLegends, bool copySchedules, string suffix)
        {
            // 1. Create New Sheet
            FamilyInstance titleBlock = new FilteredElementCollector(_doc, sourceSheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault();

            ElementId titleBlockTypeId = titleBlock != null ? titleBlock.GetTypeId() : ElementId.InvalidElementId;
            ViewSheet newSheet;
            
            if (titleBlockTypeId != ElementId.InvalidElementId)
            {
                newSheet = ViewSheet.Create(_doc, titleBlockTypeId);
            }
            else
            {
                newSheet = ViewSheet.Create(_doc, ElementId.InvalidElementId);
            }

            // 2. Set Name/Number
            try 
            {
                newSheet.Name = sourceSheet.Name + suffix;
                newSheet.SheetNumber = sourceSheet.SheetNumber + "-SC"; 
            }
            catch {}

            // 3. Duplicate Views & Place
            var viewports = sourceSheet.GetAllViewports();
            foreach (ElementId vpId in viewports)
            {
                Viewport vp = _doc.GetElement(vpId) as Viewport;
                if (vp == null) continue;

                View view = _doc.GetElement(vp.ViewId) as View;
                if (view == null) continue;

                if (view.ViewType == ViewType.Legend)
                {
                    if (copyLegends)
                    {
                        Viewport.Create(_doc, newSheet.Id, view.Id, vp.GetBoxCenter());
                    }
                    continue;
                }

                if (view.ViewType == ViewType.Schedule) continue;

                ElementId newViewId = ElementId.InvalidElementId;
                try
                {
                    newViewId = view.Duplicate(viewOption);
                }
                catch
                {
                    continue;
                }

                if (newViewId != ElementId.InvalidElementId)
                {
                    Viewport.Create(_doc, newSheet.Id, newViewId, vp.GetBoxCenter());
                }
            }
            
            // 4. Handle Schedules
            if (copySchedules)
            {
                var scheduleInstances = new FilteredElementCollector(_doc, sourceSheet.Id)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>();

                foreach(var ssi in scheduleInstances)
                {
                    if (!ssi.IsTitleblockRevisionSchedule)
                    {
                        ScheduleSheetInstance.Create(_doc, newSheet.Id, ssi.ScheduleId, ssi.Point);
                    }
                }
            }
        }
    }

    public class DuplicateSheetItem : INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }
        public string Name { get; }
        public string Number { get; }
        public bool IsSelected { get; set; }

        public DuplicateSheetItem(ViewSheet sheet)
        {
            Sheet = sheet;
            Name = $"{sheet.SheetNumber} - {sheet.Name}";
            Number = sheet.SheetNumber;
            IsSelected = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
