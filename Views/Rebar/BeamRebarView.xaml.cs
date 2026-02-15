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
    public partial class BeamRebarView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "BeamRebar";
        public bool IsConfirmed { get; private set; } = false;

        // Collections for lookup
        private List<RebarBarType> _rebarTypes;
        private List<RebarShape> _shapes;
        private List<RebarHookType> _hookList;

        public BeamRebarView(Document doc)
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

            UI_Combo_T1Type.ItemsSource = _rebarTypes;
            UI_Combo_T2Type.ItemsSource = _rebarTypes;
            UI_Combo_B2Type.ItemsSource = _rebarTypes;
            UI_Combo_B1Type.ItemsSource = _rebarTypes;
            UI_Combo_TransType.ItemsSource = _rebarTypes;

            UI_Combo_T1Type.DisplayMemberPath = "Name";
            UI_Combo_T2Type.DisplayMemberPath = "Name";
            UI_Combo_B2Type.DisplayMemberPath = "Name";
            UI_Combo_B1Type.DisplayMemberPath = "Name";
            UI_Combo_TransType.DisplayMemberPath = "Name";

            var d16 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();
            UI_Combo_T1Type.SelectedItem = d16;
            UI_Combo_T2Type.SelectedItem = d16;
            UI_Combo_B2Type.SelectedItem = d16;
            UI_Combo_B1Type.SelectedItem = d16;

            var r10 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("R10")) ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("R6")) ?? _rebarTypes.FirstOrDefault();
            UI_Combo_TransType.SelectedItem = r10;

            // Rebar Shapes
            _shapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(x => x.Name)
                .ToList();

            UI_Combo_TransShape.ItemsSource = _shapes;
            UI_Combo_TransShape.DisplayMemberPath = "Name";
            UI_Combo_TransShape.SelectedItem = _shapes.FirstOrDefault(x => x.Name.Contains("00") || x.Name.ToLower().Contains("stirrup")) ?? _shapes.FirstOrDefault();

            // Hook Types
            var hooks = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hooks);

            // Longitudinal Hooks
            UI_Combo_TopHookStart.ItemsSource = _hookList;
            UI_Combo_TopHookEnd.ItemsSource = _hookList;
            UI_Combo_BotHookStart.ItemsSource = _hookList;
            UI_Combo_BotHookEnd.ItemsSource = _hookList;
            
            UI_Combo_TopHookStart.DisplayMemberPath = "Name";
            UI_Combo_TopHookEnd.DisplayMemberPath = "Name";
            UI_Combo_BotHookStart.DisplayMemberPath = "Name";
            UI_Combo_BotHookEnd.DisplayMemberPath = "Name";

            // Stirrup Hooks
            UI_Combo_HookStart.ItemsSource = _hookList;
            UI_Combo_HookEnd.ItemsSource = _hookList;
            UI_Combo_HookStart.DisplayMemberPath = "Name";
            UI_Combo_HookEnd.DisplayMemberPath = "Name";

            UI_Combo_TopHookStart.SelectedIndex = 0;
            UI_Combo_TopHookEnd.SelectedIndex = 0;
            UI_Combo_BotHookStart.SelectedIndex = 0;
            UI_Combo_BotHookEnd.SelectedIndex = 0;
            UI_Combo_HookStart.SelectedIndex = 0;
            UI_Combo_HookEnd.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                // Checkboxes
                UI_Check_T1.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T1Enabled", true);
                UI_Check_T2.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T2Enabled", false);
                UI_Check_B2.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B2Enabled", false);
                UI_Check_B1.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B1Enabled", true);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                // Text Fields
                UI_Text_T1Count.Text = SettingsManager.Get(VIEW_NAME, "T1Count", "2");
                UI_Text_T2Count.Text = SettingsManager.Get(VIEW_NAME, "T2Count", "3");
                UI_Text_B2Count.Text = SettingsManager.Get(VIEW_NAME, "B2Count", "3");
                UI_Text_B1Count.Text = SettingsManager.Get(VIEW_NAME, "B1Count", "2");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Text_TransStartOffset.Text = SettingsManager.Get(VIEW_NAME, "TransStartOffset", "50");

                // Rebar Type Combos
                SelectByName(UI_Combo_T1Type, SettingsManager.Get(VIEW_NAME, "T1Type"), _rebarTypes);
                SelectByName(UI_Combo_T2Type, SettingsManager.Get(VIEW_NAME, "T2Type"), _rebarTypes);
                SelectByName(UI_Combo_B2Type, SettingsManager.Get(VIEW_NAME, "B2Type"), _rebarTypes);
                SelectByName(UI_Combo_B1Type, SettingsManager.Get(VIEW_NAME, "B1Type"), _rebarTypes);
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"), _rebarTypes);

                // Shape Combo
                SelectByName(UI_Combo_TransShape, SettingsManager.Get(VIEW_NAME, "TransShape"), _shapes);

                // Hook Combos
                SelectHookByName(UI_Combo_TopHookStart, SettingsManager.Get(VIEW_NAME, "TopHookStart"));
                SelectHookByName(UI_Combo_TopHookEnd, SettingsManager.Get(VIEW_NAME, "TopHookEnd"));
                SelectHookByName(UI_Combo_BotHookStart, SettingsManager.Get(VIEW_NAME, "BotHookStart"));
                SelectHookByName(UI_Combo_BotHookEnd, SettingsManager.Get(VIEW_NAME, "BotHookEnd"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));

                // Update visibility
                toggle_visibility(null, null);
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                // Checkboxes
                SettingsManager.Set(VIEW_NAME, "T1Enabled", (UI_Check_T1.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "T2Enabled", (UI_Check_T2.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B2Enabled", (UI_Check_B2.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B1Enabled", (UI_Check_B1.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "RemoveExisting", (UI_Check_RemoveExisting.IsChecked == true).ToString());

                // Text Fields
                SettingsManager.Set(VIEW_NAME, "T1Count", UI_Text_T1Count.Text);
                SettingsManager.Set(VIEW_NAME, "T2Count", UI_Text_T2Count.Text);
                SettingsManager.Set(VIEW_NAME, "B2Count", UI_Text_B2Count.Text);
                SettingsManager.Set(VIEW_NAME, "B1Count", UI_Text_B1Count.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransStartOffset", UI_Text_TransStartOffset.Text);

                // Rebar Type Combos
                SettingsManager.Set(VIEW_NAME, "T1Type", (UI_Combo_T1Type.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "T2Type", (UI_Combo_T2Type.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "B2Type", (UI_Combo_B2Type.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "B1Type", (UI_Combo_B1Type.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TransType", (UI_Combo_TransType.SelectedItem as RebarBarType)?.Name ?? "");

                // Shape Combo
                SettingsManager.Set(VIEW_NAME, "TransShape", (UI_Combo_TransShape.SelectedItem as RebarShape)?.Name ?? "");

                // Hook Combos
                SettingsManager.Set(VIEW_NAME, "TopHookStart", (UI_Combo_TopHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TopHookEnd", (UI_Combo_TopHookEnd.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BotHookStart", (UI_Combo_BotHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BotHookEnd", (UI_Combo_BotHookEnd.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HookStart", (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HookEnd", (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name ?? "");

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

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            if (UI_Group_T1 == null) return;
            UI_Group_T1.Visibility = UI_Check_T1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_T2.Visibility = UI_Check_T2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B2.Visibility = UI_Check_B2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B1.Visibility = UI_Check_B1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
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

        // Top Layers
        public bool T1Enabled => UI_Check_T1.IsChecked == true;
        public RebarBarType T1Type => UI_Combo_T1Type.SelectedItem as RebarBarType;
        public int T1Count => int.TryParse(UI_Text_T1Count.Text, out int c) ? c : 2;

        public bool T2Enabled => UI_Check_T2.IsChecked == true;
        public RebarBarType T2Type => UI_Combo_T2Type.SelectedItem as RebarBarType;
        public int T2Count => int.TryParse(UI_Text_T2Count.Text, out int c) ? c : 3;

        public RebarHookType TopHookStart => UI_Combo_TopHookStart.SelectedItem as RebarHookType;
        public RebarHookType TopHookEnd => UI_Combo_TopHookEnd.SelectedItem as RebarHookType;

        // Bottom Layers
        public bool B2Enabled => UI_Check_B2.IsChecked == true;
        public RebarBarType B2Type => UI_Combo_B2Type.SelectedItem as RebarBarType;
        public int B2Count => int.TryParse(UI_Text_B2Count.Text, out int c) ? c : 3;

        public bool B1Enabled => UI_Check_B1.IsChecked == true;
        public RebarBarType B1Type => UI_Combo_B1Type.SelectedItem as RebarBarType;
        public int B1Count => int.TryParse(UI_Text_B1Count.Text, out int c) ? c : 2;

        public RebarHookType BotHookStart => UI_Combo_BotHookStart.SelectedItem as RebarHookType;
        public RebarHookType BotHookEnd => UI_Combo_BotHookEnd.SelectedItem as RebarHookType;

        // Stirrups
        public RebarShape TransShape => UI_Combo_TransShape.SelectedItem as RebarShape;
        public RebarBarType TransType => UI_Combo_TransType.SelectedItem as RebarBarType;
        public double TransSpacingMM => double.TryParse(UI_Text_TransSpacing.Text, out double d) ? d : 200;
        public RebarHookType HookStart => UI_Combo_HookStart.SelectedItem as RebarHookType;
        public RebarHookType HookEnd => UI_Combo_HookEnd.SelectedItem as RebarHookType;
        public double StartOffsetMM => double.TryParse(UI_Text_TransStartOffset.Text, out double d) ? d : 50;

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
    }
}
