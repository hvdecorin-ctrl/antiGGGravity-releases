using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.Rebar
{
    public partial class FootingPadRebarView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "FootingPadRebar";
        public bool IsConfirmed { get; private set; } = false;

        private List<RebarBarType> _rebarTypes;
        private List<RebarShape> _shapes;
        private List<RebarHookType> _hookList;

        public FootingPadRebarView(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSettings();
        }

        private void LoadData()
        {
            // Rebar Types
            _rebarTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name)
                .ToList();

            void SetTypes(ComboBox box, string defaultName)
            {
                box.ItemsSource = _rebarTypes;
                box.DisplayMemberPath = "Name";
                box.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains(defaultName)) ?? _rebarTypes.FirstOrDefault();
            }

            SetTypes(UI_Combo_TopType, "D16");
            SetTypes(UI_Combo_BottomType, "D16");

            // Rebar Shapes
            _shapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(x => x.Name)
                .ToList();

            void SetShape(ComboBox box) 
            {
                box.ItemsSource = _shapes;
                box.DisplayMemberPath = "Name";
                box.SelectedItem = _shapes.FirstOrDefault(x => x.Name.Contains("00") || x.Name.ToLower().Contains("straight")) ?? _shapes.FirstOrDefault();
            }

            SetShape(UI_Combo_TopShape);
            SetShape(UI_Combo_BottomShape);

            // Hooks
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            void SetHook(ComboBox box)
            {
                box.ItemsSource = _hookList;
                box.DisplayMemberPath = "Name";
                box.SelectedIndex = 0;
            }

            SetHook(UI_Combo_TopHook);
            SetHook(UI_Combo_BottomHook);

            UpdateVisibility();
        }

        private void LoadSettings()
        {
            try
            {
                UI_Check_TopBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableTop", true);
                UI_Check_BottomBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableBottom", true);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                UI_Text_TopSpacing.Text = SettingsManager.Get(VIEW_NAME, "TopSpacing", "200");
                UI_Text_BottomSpacing.Text = SettingsManager.Get(VIEW_NAME, "BottomSpacing", "200");

                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"), _rebarTypes);
                SelectByName(UI_Combo_BottomType, SettingsManager.Get(VIEW_NAME, "BottomType"), _rebarTypes);
                SelectByName(UI_Combo_TopShape, SettingsManager.Get(VIEW_NAME, "TopShape"), _shapes);
                SelectByName(UI_Combo_BottomShape, SettingsManager.Get(VIEW_NAME, "BottomShape"), _shapes);

                SelectHookByName(UI_Combo_TopHook, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BottomHook, SettingsManager.Get(VIEW_NAME, "BottomHook"));

                UpdateVisibility();
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "EnableTop", (UI_Check_TopBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "EnableBottom", (UI_Check_BottomBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "RemoveExisting", (UI_Check_RemoveExisting.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "TopSpacing", UI_Text_TopSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "BottomSpacing", UI_Text_BottomSpacing.Text);

                SettingsManager.Set(VIEW_NAME, "TopType", (UI_Combo_TopType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BottomType", (UI_Combo_BottomType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TopShape", (UI_Combo_TopShape.SelectedItem as RebarShape)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BottomShape", (UI_Combo_BottomShape.SelectedItem as RebarShape)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "TopHook", (UI_Combo_TopHook.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BottomHook", (UI_Combo_BottomHook.SelectedItem as RebarHookType)?.Name ?? "");

                SettingsManager.SaveAll();
            }
            catch { }
        }

        private void SelectByName<T>(ComboBox combo, string name, List<T> items) where T : Element
        {
            if (string.IsNullOrEmpty(name)) return;
            var match = items.FirstOrDefault(x => x.Name == name);
            if (match != null) combo.SelectedItem = match;
        }

        private void SelectHookByName(ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) { combo.SelectedIndex = 0; return; }
            var match = _hookList.FirstOrDefault(x => x?.Name == name);
            if (match != null) combo.SelectedItem = match;
            else combo.SelectedIndex = 0;
        }

        private void UpdateVisibility()
        {
            if (UI_Group_Top != null) UI_Group_Top.Visibility = UI_Check_TopBars.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            if (UI_Group_Bottom != null) UI_Group_Bottom.Visibility = UI_Check_BottomBars.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void UI_Check_TopBars_Click(object sender, RoutedEventArgs e) => UpdateVisibility();
        private void UI_Check_BottomBars_Click(object sender, RoutedEventArgs e) => UpdateVisibility();

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            IsConfirmed = true;
            Close();
        }

        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        // Accessors
        public bool EnableTop => UI_Check_TopBars.IsChecked == true;
        public RebarBarType TopType => UI_Combo_TopType.SelectedItem as RebarBarType;
        public RebarShape TopShape => UI_Combo_TopShape.SelectedItem as RebarShape;
        public RebarHookType TopHook => UI_Combo_TopHook.SelectedItem as RebarHookType;
        public double TopSpacingMM => double.TryParse(UI_Text_TopSpacing.Text, out double d) ? d : 200;

        public bool EnableBottom => UI_Check_BottomBars.IsChecked == true;
        public RebarBarType BottomType => UI_Combo_BottomType.SelectedItem as RebarBarType;
        public RebarShape BottomShape => UI_Combo_BottomShape.SelectedItem as RebarShape;
        public RebarHookType BottomHook => UI_Combo_BottomHook.SelectedItem as RebarHookType;
        public double BottomSpacingMM => double.TryParse(UI_Text_BottomSpacing.Text, out double d) ? d : 200;

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
    }
}
