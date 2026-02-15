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
    public partial class WallCornerRebarUView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "WallCornerRebarU";
        public bool IsConfirmed { get; private set; } = false;

        private List<RebarBarType> _rebarTypes;
        private List<RebarHookType> _hookList;

        public WallCornerRebarUView(Document doc)
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

            UI_Combo_HorizType.ItemsSource = _rebarTypes;
            UI_Combo_HorizType.DisplayMemberPath = "Name";
            UI_Combo_HorizType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TrimmerType.ItemsSource = _rebarTypes;
            UI_Combo_TrimmerType.DisplayMemberPath = "Name";
            UI_Combo_TrimmerType.SelectedItem = UI_Combo_HorizType.SelectedItem;

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            UI_Combo_HorizHookStart.ItemsSource = _hookList;
            UI_Combo_HorizHookStart.DisplayMemberPath = "Name";
            UI_Combo_HorizHookStart.SelectedIndex = 0;

            UI_Combo_HorizHookEnd.ItemsSource = _hookList;
            UI_Combo_HorizHookEnd.DisplayMemberPath = "Name";
            UI_Combo_HorizHookEnd.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Check_AddTrimmers.IsChecked = SettingsManager.GetBool(VIEW_NAME, "AddTrimmers", true);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                UI_Text_HorizSpacing.Text = SettingsManager.Get(VIEW_NAME, "HorizSpacing", "200");
                UI_Text_HorizLeg1.Text = SettingsManager.Get(VIEW_NAME, "Leg1", "700");
                UI_Text_HorizLeg2.Text = SettingsManager.Get(VIEW_NAME, "Leg2", "700");
                UI_Text_HorizTopOffset.Text = SettingsManager.Get(VIEW_NAME, "TopOffset", "50");
                UI_Text_HorizBottomOffset.Text = SettingsManager.Get(VIEW_NAME, "BottomOffset", "50");

                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"), _rebarTypes);
                SelectByName(UI_Combo_TrimmerType, SettingsManager.Get(VIEW_NAME, "TrimmerType"), _rebarTypes);

                SelectHookByName(UI_Combo_HorizHookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HorizHookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));

                var layerConfig = SettingsManager.Get(VIEW_NAME, "LayerConfig", "Centre");
                foreach (ComboBoxItem item in UI_Combo_LayerConfig.Items)
                    if (item.Content.ToString() == layerConfig) { UI_Combo_LayerConfig.SelectedItem = item; break; }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "AddTrimmers", (UI_Check_AddTrimmers.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "RemoveExisting", (UI_Check_RemoveExisting.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "HorizSpacing", UI_Text_HorizSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "Leg1", UI_Text_HorizLeg1.Text);
                SettingsManager.Set(VIEW_NAME, "Leg2", UI_Text_HorizLeg2.Text);
                SettingsManager.Set(VIEW_NAME, "TopOffset", UI_Text_HorizTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "BottomOffset", UI_Text_HorizBottomOffset.Text);

                SettingsManager.Set(VIEW_NAME, "HorizType", (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TrimmerType", (UI_Combo_TrimmerType.SelectedItem as RebarBarType)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "HookStart", (UI_Combo_HorizHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HookEnd", (UI_Combo_HorizHookEnd.SelectedItem as RebarHookType)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "LayerConfig", LayerConfig);

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

        // --- Properties ---
        public RebarBarType HorizType => UI_Combo_HorizType.SelectedItem as RebarBarType;
        public double HorizSpacingMM => double.TryParse(UI_Text_HorizSpacing.Text, out double d) ? d : 200;
        
        public double Leg1MM => double.TryParse(UI_Text_HorizLeg1.Text, out double d) ? d : 700;
        public double Leg2MM => double.TryParse(UI_Text_HorizLeg2.Text, out double d) ? d : 700;

        public double TopOffsetMM => double.TryParse(UI_Text_HorizTopOffset.Text, out double d) ? d : 50;
        public double BottomOffsetMM => double.TryParse(UI_Text_HorizBottomOffset.Text, out double d) ? d : 50;

        public RebarHookType HookStart => UI_Combo_HorizHookStart.SelectedItem as RebarHookType;
        public RebarHookType HookEnd => UI_Combo_HorizHookEnd.SelectedItem as RebarHookType;
        public bool HookStartOut => UI_Check_HorizHookStartOut.IsChecked == true;
        public bool HookEndOut => UI_Check_HorizHookEndOut.IsChecked == true;

        public bool AddTrimmers => UI_Check_AddTrimmers.IsChecked == true;
        public RebarBarType TrimmerType => UI_Combo_TrimmerType.SelectedItem as RebarBarType;

        public string LayerConfig
        {
            get
            {
                if (UI_Combo_LayerConfig.SelectedItem is ComboBoxItem item)
                    return item.Content.ToString();
                return "Centre";
            }
        }

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
    }
}
