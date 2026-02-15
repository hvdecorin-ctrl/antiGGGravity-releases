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
    public partial class WallRebarView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "WallRebar";
        public bool IsConfirmed { get; private set; } = false;

        private List<RebarBarType> _rebarTypes;
        private List<RebarHookType> _hookList;

        public WallRebarView(Document doc)
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

            UI_Combo_VertType.ItemsSource = _rebarTypes;
            UI_Combo_VertType.DisplayMemberPath = "Name";
            UI_Combo_VertType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_HorizType.ItemsSource = _rebarTypes;
            UI_Combo_HorizType.DisplayMemberPath = "Name";
            UI_Combo_HorizType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            UI_Combo_VertHookStart.ItemsSource = _hookList;
            UI_Combo_VertHookStart.DisplayMemberPath = "Name";
            UI_Combo_VertHookStart.SelectedIndex = 0;

            UI_Combo_VertHookEnd.ItemsSource = _hookList;
            UI_Combo_VertHookEnd.DisplayMemberPath = "Name";
            UI_Combo_VertHookEnd.SelectedIndex = 0;

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
                UI_Check_VertBotExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableVertBotExt", false);
                UI_Check_VertTopExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableVertTopExt", false);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                UI_Text_VertSpacing.Text = SettingsManager.Get(VIEW_NAME, "VertSpacing", "200");
                UI_Text_VertStartOffset.Text = SettingsManager.Get(VIEW_NAME, "VertStartOffset", "50");
                UI_Text_VertEndOffset.Text = SettingsManager.Get(VIEW_NAME, "VertEndOffset", "50");
                UI_Text_VertBotExt.Text = SettingsManager.Get(VIEW_NAME, "VertBotExt", "500");
                UI_Text_VertTopExt.Text = SettingsManager.Get(VIEW_NAME, "VertTopExt", "500");

                UI_Text_HorizSpacing.Text = SettingsManager.Get(VIEW_NAME, "HorizSpacing", "200");
                UI_Text_HorizTopOffset.Text = SettingsManager.Get(VIEW_NAME, "HorizTopOffset", "50");
                UI_Text_HorizBottomOffset.Text = SettingsManager.Get(VIEW_NAME, "HorizBottomOffset", "50");

                SelectByName(UI_Combo_VertType, SettingsManager.Get(VIEW_NAME, "VertType"), _rebarTypes);
                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"), _rebarTypes);

                SelectHookByName(UI_Combo_VertHookStart, SettingsManager.Get(VIEW_NAME, "VertHookStart"));
                SelectHookByName(UI_Combo_VertHookEnd, SettingsManager.Get(VIEW_NAME, "VertHookEnd"));
                SelectHookByName(UI_Combo_HorizHookStart, SettingsManager.Get(VIEW_NAME, "HorizHookStart"));
                SelectHookByName(UI_Combo_HorizHookEnd, SettingsManager.Get(VIEW_NAME, "HorizHookEnd"));

                var layerConfig = SettingsManager.Get(VIEW_NAME, "LayerConfig", "Centre");
                foreach (ComboBoxItem item in UI_Combo_LayerConfig.Items)
                    if (item.Content.ToString() == layerConfig) { UI_Combo_LayerConfig.SelectedItem = item; break; }

                UI_Check_VertBotExt_Click(null, null);
                UI_Check_VertTopExt_Click(null, null);
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "EnableVertBotExt", (UI_Check_VertBotExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "EnableVertTopExt", (UI_Check_VertTopExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "RemoveExisting", (UI_Check_RemoveExisting.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "VertSpacing", UI_Text_VertSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "VertStartOffset", UI_Text_VertStartOffset.Text);
                SettingsManager.Set(VIEW_NAME, "VertEndOffset", UI_Text_VertEndOffset.Text);
                SettingsManager.Set(VIEW_NAME, "VertBotExt", UI_Text_VertBotExt.Text);
                SettingsManager.Set(VIEW_NAME, "VertTopExt", UI_Text_VertTopExt.Text);

                SettingsManager.Set(VIEW_NAME, "HorizSpacing", UI_Text_HorizSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "HorizTopOffset", UI_Text_HorizTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "HorizBottomOffset", UI_Text_HorizBottomOffset.Text);

                SettingsManager.Set(VIEW_NAME, "VertType", (UI_Combo_VertType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HorizType", (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "VertHookStart", (UI_Combo_VertHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "VertHookEnd", (UI_Combo_VertHookEnd.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HorizHookStart", (UI_Combo_HorizHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "HorizHookEnd", (UI_Combo_HorizHookEnd.SelectedItem as RebarHookType)?.Name ?? "");

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

        private void UI_Check_VertBotExt_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Text_VertBotExt != null)
                UI_Text_VertBotExt.Visibility = UI_Check_VertBotExt.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void UI_Check_VertTopExt_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Text_VertTopExt != null)
                UI_Text_VertTopExt.Visibility = UI_Check_VertTopExt.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        // --- Properties ---
        public RebarBarType VertType => UI_Combo_VertType.SelectedItem as RebarBarType;
        public double VertSpacingMM => double.TryParse(UI_Text_VertSpacing.Text, out double d) ? d : 200;
        public double VertStartOffsetMM => double.TryParse(UI_Text_VertStartOffset.Text, out double d) ? d : 50;
        public double VertEndOffsetMM => double.TryParse(UI_Text_VertEndOffset.Text, out double d) ? d : 50;
        
        public RebarHookType VertHookStart => UI_Combo_VertHookStart.SelectedItem as RebarHookType;
        public RebarHookType VertHookEnd => UI_Combo_VertHookEnd.SelectedItem as RebarHookType;
        public bool VertHookStartOut => UI_Check_VertHookStartOut.IsChecked == true;
        public bool VertHookEndOut => UI_Check_VertHookEndOut.IsChecked == true;

        public bool EnableVertBotExt => UI_Check_VertBotExt.IsChecked == true;
        public double VertBotExtMM => double.TryParse(UI_Text_VertBotExt.Text, out double d) ? d : 500;
        
        public bool EnableVertTopExt => UI_Check_VertTopExt.IsChecked == true;
        public double VertTopExtMM => double.TryParse(UI_Text_VertTopExt.Text, out double d) ? d : 500;

        public RebarBarType HorizType => UI_Combo_HorizType.SelectedItem as RebarBarType;
        public double HorizSpacingMM => double.TryParse(UI_Text_HorizSpacing.Text, out double d) ? d : 200;
        public double HorizTopOffsetMM => double.TryParse(UI_Text_HorizTopOffset.Text, out double d) ? d : 50;
        public double HorizBottomOffsetMM => double.TryParse(UI_Text_HorizBottomOffset.Text, out double d) ? d : 50;

        public RebarHookType HorizHookStart => UI_Combo_HorizHookStart.SelectedItem as RebarHookType;
        public RebarHookType HorizHookEnd => UI_Combo_HorizHookEnd.SelectedItem as RebarHookType;
        public bool HorizHookStartOut => UI_Check_HorizHookStartOut.IsChecked == true;
        public bool HorizHookEndOut => UI_Check_HorizHookEndOut.IsChecked == true;

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
