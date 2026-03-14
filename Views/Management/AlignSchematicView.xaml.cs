using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using antiGGGravity.Utilities;

namespace antiGGGravity.Views.Management
{
    public partial class AlignSchematicView : Window
    {
        private ExternalCommandData _commandData;
        private Document _doc;
        private List<AlignSheetItem> _allSheets;

        public AlignSchematicView(ExternalCommandData commandData)
        {
            // Merge shared resources before initializing component to prevent parsing delay
            this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);

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
                .Select(s => new AlignSheetItem(s))
                .OrderBy(s => s.Number)
                .ToList();

            UI_List_Main.ItemsSource = _allSheets;
            UI_List_Others.ItemsSource = _allSheets;

            if (_allSheets.Any())
            {
                UI_List_Main.SelectedIndex = 0;
            }
        }

        private void UI_Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_Search.Text.ToLower();
            var filtered = _allSheets.Where(s => s.Name.ToLower().Contains(filter)).ToList();
            UI_List_Main.ItemsSource = filtered;
        }

        private void UI_List_Main_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatus();
        }

        private void UI_Btn_All_Click(object sender, RoutedEventArgs e)
        {
            var main = UI_List_Main.SelectedItem as AlignSheetItem;
            foreach (var item in UI_List_Others.Items.Cast<AlignSheetItem>())
            {
                if (item != main) item.IsSelected = true;
            }
            UpdateStatus();
        }

        private void UI_Btn_None_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in UI_List_Others.Items.Cast<AlignSheetItem>())
            {
                item.IsSelected = false;
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var main = UI_List_Main.SelectedItem as AlignSheetItem;
            int count = _allSheets.Count(s => s.IsSelected && s != main);
            
            if (main == null)
            {
                UI_Status_Text.Text = "Select a master sheet.";
            }
            else
            {
                UI_Status_Text.Text = $"Master: {main.Number} -> Aligning {count} sheets.";
            }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Align_Click(object sender, RoutedEventArgs e)
        {
            var mainItem = UI_List_Main.SelectedItem as AlignSheetItem;
            if (mainItem == null)
            {
                MessageBox.Show("Please select a Master Sheet.");
                return;
            }

            var targetItems = _allSheets.Where(s => s.IsSelected && s != mainItem).ToList();
            if (!targetItems.Any())
            {
                MessageBox.Show("Please select at least one Target Sheet.");
                return;
            }

            bool alignTitle = UI_Check_Titleblock.IsChecked == true;
            bool alignLegend = UI_Check_Legends.IsChecked == true;

            using (Transaction t = new Transaction(_doc, "Align Schematic Views"))
            {
                t.Start();
                try
                {
                    SheetData mainData = new SheetData(mainItem.Sheet, _doc);
                    if (mainData.Viewports.Count == 0)
                    {
                        MessageBox.Show($"Master sheet {mainItem.Number} has no viewports to align to.");
                        t.RollBack();
                        return;
                    }

                    foreach (var target in targetItems)
                    {
                        SheetData targetData = new SheetData(target.Sheet, _doc);
                        
                        // Align all matching viewports by type and name
                        // legends are handled specifically by the UI checkbox
                        targetData.AlignToMaster(mainData, true, alignLegend);
                        
                        if (alignTitle) targetData.AlignTitleBlock();
                    }

                    t.Commit();
                    MessageBox.Show("Alignment Complete.", "Success");
                    Close();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }
    }

    public class AlignSheetItem : INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }
        public string Name { get; }
        public string Number { get; }
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public AlignSheetItem(ViewSheet sheet)
        {
            Sheet = sheet;
            Name = $"{sheet.SheetNumber} - {sheet.Name}";
            Number = sheet.SheetNumber;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SheetData
    {
        public ViewSheet Sheet { get; }
        // Key is View Name, Value is the Viewport
        public Dictionary<string, Viewport> Viewports { get; } = new Dictionary<string, Viewport>();
        public FamilyInstance TitleBlock { get; private set; }
        private Document _doc;

        public SheetData(ViewSheet sheet, Document doc)
        {
            Sheet = sheet;
            _doc = doc;
            ParseContent();
        }

        private void ParseContent()
        {
            TitleBlock = new FilteredElementCollector(_doc, Sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault();

            foreach (ElementId vpId in Sheet.GetAllViewports())
            {
                Viewport vp = _doc.GetElement(vpId) as Viewport;
                if (vp == null) continue;

                View view = _doc.GetElement(vp.ViewId) as View;
                if (view == null) continue;

                // Index by view name for direct matching
                if (!Viewports.ContainsKey(view.Name))
                {
                    Viewports.Add(view.Name, vp);
                }
            }
        }

        public void AlignToMaster(SheetData master, bool alignOtherViewports, bool alignLegends)
        {
            var targetPlans = Viewports.Values.Where(vp => IsPlanView(_doc.GetElement(vp.ViewId) as View)).ToList();
            var masterPlans = master.Viewports.Values.Where(vp => IsPlanView(_doc.GetElement(vp.ViewId) as View)).ToList();

            // Special Case: If both sheets have exactly one plan, align them even if names differ
            if (alignOtherViewports && targetPlans.Count == 1 && masterPlans.Count == 1)
            {
                targetPlans[0].SetBoxCenter(masterPlans[0].GetBoxCenter());
            }

            // Iterate through ALL viewports on THIS sheet and try to find a match on MASTER
            foreach (var entry in Viewports)
            {
                string viewName = entry.Key;
                Viewport targetVP = entry.Value;
                View targetView = _doc.GetElement(targetVP.ViewId) as View;

                if (targetView == null) continue;

                // Determine if we should align this specific viewport type
                bool isLegend = targetView.ViewType == ViewType.Legend;
                if (isLegend && !alignLegends) continue;
                if (!isLegend && !alignOtherViewports) continue;

                // Skip if already handled by the single-plan special case
                if (targetPlans.Count == 1 && masterPlans.Count == 1 && targetPlans[0].Id == targetVP.Id) continue;

                // Try to find matching view on master sheet
                if (master.Viewports.TryGetValue(viewName, out Viewport masterVP))
                {
                    View masterView = _doc.GetElement(masterVP.ViewId) as View;
                    
                    // CRITICAL: Only match if ViewType is the same
                    if (masterView != null && masterView.ViewType == targetView.ViewType)
                    {
                        targetVP.SetBoxCenter(masterVP.GetBoxCenter());
                    }
                }
            }
        }

        private bool IsPlanView(View view)
        {
            if (view == null) return false;
            ViewType vt = view.ViewType;
            return vt == ViewType.FloorPlan || 
                   vt == ViewType.CeilingPlan || 
                   vt == ViewType.EngineeringPlan ||
                   vt == ViewType.AreaPlan;
        }

        public void AlignTitleBlock()
        {
             if (TitleBlock != null)
             {
                 XYZ current = TitleBlock.Location is LocationPoint lp ? lp.Point : null;
                 if (current != null && !current.IsAlmostEqualTo(XYZ.Zero))
                 {
                     XYZ translation = XYZ.Zero - current;
                     ElementTransformUtils.MoveElement(_doc, TitleBlock.Id, translation);
                 }
             }
        }
    }
}
