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
    public partial class ColumnRebarView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "ColumnRebar";
        public bool IsConfirmed { get; private set; } = false;

        private List<RebarBarType> _rebarTypes;
        private List<RebarShape> _shapes;
        private List<RebarHookType> _hookList;

        public ColumnRebarView(Document doc)
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

            UI_Combo_VerticalType.ItemsSource = _rebarTypes;
            UI_Combo_VerticalType.DisplayMemberPath = "Name";
            UI_Combo_VerticalType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D20")) ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TieType.ItemsSource = _rebarTypes;
            UI_Combo_TieType.DisplayMemberPath = "Name";
            UI_Combo_TieType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("R10")) ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("R6")) ?? _rebarTypes.FirstOrDefault();

            // Rebar Shapes
            _shapes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(x => x.Name)
                .ToList();

            UI_Combo_VerticalShape.ItemsSource = _shapes;
            UI_Combo_VerticalShape.DisplayMemberPath = "Name";
            UI_Combo_VerticalShape.SelectedItem = _shapes.FirstOrDefault(x => x.Name.Contains("00") || x.Name.ToLower().Contains("straight")) ?? _shapes.FirstOrDefault();

            UI_Combo_TieShape.ItemsSource = _shapes;
            UI_Combo_TieShape.DisplayMemberPath = "Name";
            UI_Combo_TieShape.SelectedItem = _shapes.FirstOrDefault(x => x.Name.Contains("MT_1") || x.Name.ToLower().Contains("stirrup") || x.Name.Contains("51")) ?? _shapes.FirstOrDefault();

            // Hooks
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            void SetHook(ComboBox box) {
                box.ItemsSource = _hookList;
                box.DisplayMemberPath = "Name";
                box.SelectedIndex = 0;
            }

            SetHook(UI_Combo_VHookTop);
            SetHook(UI_Combo_VHookBot);
            SetHook(UI_Combo_HookStart);
            SetHook(UI_Combo_HookEnd);
        }

        private void LoadSettings()
        {
            try
            {
                UI_Check_TopExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableTopExt", false);
                UI_Check_BotExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableBotExt", false);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                UI_Text_CountX.Text = SettingsManager.Get(VIEW_NAME, "CountX", "2");
                UI_Text_CountY.Text = SettingsManager.Get(VIEW_NAME, "CountY", "2");
                UI_Text_TieSpacing.Text = SettingsManager.Get(VIEW_NAME, "TieSpacing", "200");
                UI_Text_TieBotOffset.Text = SettingsManager.Get(VIEW_NAME, "TieBotOffset", "50");
                UI_Text_TieTopOffset.Text = SettingsManager.Get(VIEW_NAME, "TieTopOffset", "50");
                UI_Text_TopExtValue.Text = SettingsManager.Get(VIEW_NAME, "TopExtValue", "0");
                UI_Text_BotExtValue.Text = SettingsManager.Get(VIEW_NAME, "BotExtValue", "0");

                SelectByName(UI_Combo_VerticalType, SettingsManager.Get(VIEW_NAME, "VerticalType"), _rebarTypes);
                SelectByName(UI_Combo_TieType, SettingsManager.Get(VIEW_NAME, "TieType"), _rebarTypes);
                SelectByName(UI_Combo_VerticalShape, SettingsManager.Get(VIEW_NAME, "VerticalShape"), _shapes);
                SelectByName(UI_Combo_TieShape, SettingsManager.Get(VIEW_NAME, "TieShape"), _shapes);

                SelectHookByName(UI_Combo_VHookTop, SettingsManager.Get(VIEW_NAME, "VHookTop"));
                SelectHookByName(UI_Combo_VHookBot, SettingsManager.Get(VIEW_NAME, "VHookBot"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "TieHookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "TieHookEnd"));
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "EnableTopExt", (UI_Check_TopExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "EnableBotExt", (UI_Check_BotExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "RemoveExisting", (UI_Check_RemoveExisting.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "CountX", UI_Text_CountX.Text);
                SettingsManager.Set(VIEW_NAME, "CountY", UI_Text_CountY.Text);
                SettingsManager.Set(VIEW_NAME, "TieSpacing", UI_Text_TieSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TieBotOffset", UI_Text_TieBotOffset.Text);
                SettingsManager.Set(VIEW_NAME, "TieTopOffset", UI_Text_TieTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "TopExtValue", UI_Text_TopExtValue.Text);
                SettingsManager.Set(VIEW_NAME, "BotExtValue", UI_Text_BotExtValue.Text);

                SettingsManager.Set(VIEW_NAME, "VerticalType", (UI_Combo_VerticalType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TieType", (UI_Combo_TieType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "VerticalShape", (UI_Combo_VerticalShape.SelectedItem as RebarShape)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TieShape", (UI_Combo_TieShape.SelectedItem as RebarShape)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "VHookTop", (UI_Combo_VHookTop.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "VHookBot", (UI_Combo_VHookBot.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TieHookStart", (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TieHookEnd", (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name ?? "");

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

        // Accessors
        public RebarBarType VerticalType => UI_Combo_VerticalType.SelectedItem as RebarBarType;
        public RebarShape VerticalShape => UI_Combo_VerticalShape.SelectedItem as RebarShape;
        
        public RebarHookType VHookTop => UI_Combo_VHookTop.SelectedItem as RebarHookType;
        public bool VHookTopOut => UI_Check_VHookTopOut.IsChecked == true;
        
        public RebarHookType VHookBot => UI_Combo_VHookBot.SelectedItem as RebarHookType;
        public bool VHookBotOut => UI_Check_VHookBotOut.IsChecked == true;

        public int CountX => int.TryParse(UI_Text_CountX.Text, out int i) ? i : 2;
        public int CountY => int.TryParse(UI_Text_CountY.Text, out int i) ? i : 2;

        public bool EnableTopExt => UI_Check_TopExt.IsChecked == true;
        public double TopExtMM => double.TryParse(UI_Text_TopExtValue.Text, out double d) ? d : 0;
        
        public bool EnableBotExt => UI_Check_BotExt.IsChecked == true;
        public double BotExtMM => double.TryParse(UI_Text_BotExtValue.Text, out double d) ? d : 0;

        public RebarBarType TieType => UI_Combo_TieType.SelectedItem as RebarBarType;
        public RebarShape TieShape => UI_Combo_TieShape.SelectedItem as RebarShape;
        
        public RebarHookType TieHookStart => UI_Combo_HookStart.SelectedItem as RebarHookType;
        public RebarHookType TieHookEnd => UI_Combo_HookEnd.SelectedItem as RebarHookType;

        public double TieSpacingMM => double.TryParse(UI_Text_TieSpacing.Text, out double d) ? d : 200;
        public double TieBotOffsetMM => double.TryParse(UI_Text_TieBotOffset.Text, out double d) ? d : 50;
        public double TieTopOffsetMM => double.TryParse(UI_Text_TieTopOffset.Text, out double d) ? d : 50;

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
    }
}
