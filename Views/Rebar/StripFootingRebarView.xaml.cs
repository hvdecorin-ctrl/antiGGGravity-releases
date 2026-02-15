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
    public partial class StripFootingRebarView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "StripFootingRebar";
        public bool IsConfirmed { get; private set; } = false;

        private List<RebarBarType> _rebarTypes;
        private List<RebarShape> _shapes;
        private List<RebarHookType> _hookList;

        public StripFootingRebarView(Document doc)
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

            UI_Combo_TopType.ItemsSource = _rebarTypes;
            UI_Combo_TopType.DisplayMemberPath = "Name";
            UI_Combo_TopType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_BottomType.ItemsSource = _rebarTypes;
            UI_Combo_BottomType.DisplayMemberPath = "Name";
            UI_Combo_BottomType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TransType.ItemsSource = _rebarTypes;
            UI_Combo_TransType.DisplayMemberPath = "Name";
            UI_Combo_TransType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("R6") || x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

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
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);
            
            UI_Combo_TopHookStart.ItemsSource = _hookList;
            UI_Combo_TopHookStart.DisplayMemberPath = "Name";
            UI_Combo_TopHookStart.SelectedIndex = 0;

            UI_Combo_BotHookStart.ItemsSource = _hookList;
            UI_Combo_BotHookStart.DisplayMemberPath = "Name";
            UI_Combo_BotHookStart.SelectedIndex = 0;

            UI_Combo_HookStart.ItemsSource = _hookList;
            UI_Combo_HookStart.DisplayMemberPath = "Name";
            UI_Combo_HookStart.SelectedIndex = 0;

            UI_Combo_HookEnd.ItemsSource = _hookList;
            UI_Combo_HookEnd.DisplayMemberPath = "Name";
            UI_Combo_HookEnd.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Check_TopBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableTop", true);
                UI_Check_BottomBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableBottom", true);
                UI_Check_RemoveExisting.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RemoveExisting", false);

                UI_Text_TopCount.Text = SettingsManager.Get(VIEW_NAME, "TopCount", "4");
                UI_Text_BottomCount.Text = SettingsManager.Get(VIEW_NAME, "BottomCount", "4");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Text_TransStartOffset.Text = SettingsManager.Get(VIEW_NAME, "TransStartOffset", "50");
                UI_Text_LongStartOffset.Text = SettingsManager.Get(VIEW_NAME, "LongStartOffset", "50");
                UI_Text_LongEndOffset.Text = SettingsManager.Get(VIEW_NAME, "LongEndOffset", "50");

                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"), _rebarTypes);
                SelectByName(UI_Combo_BottomType, SettingsManager.Get(VIEW_NAME, "BottomType"), _rebarTypes);
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"), _rebarTypes);
                SelectByName(UI_Combo_TransShape, SettingsManager.Get(VIEW_NAME, "TransShape"), _shapes);

                SelectHookByName(UI_Combo_TopHookStart, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BotHookStart, SettingsManager.Get(VIEW_NAME, "BottomHook"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "TransHookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "TransHookEnd"));

                UI_Check_TopBars_Click(null, null);
                UI_Check_BottomBars_Click(null, null);
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

                SettingsManager.Set(VIEW_NAME, "TopCount", UI_Text_TopCount.Text);
                SettingsManager.Set(VIEW_NAME, "BottomCount", UI_Text_BottomCount.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransStartOffset", UI_Text_TransStartOffset.Text);
                SettingsManager.Set(VIEW_NAME, "LongStartOffset", UI_Text_LongStartOffset.Text);
                SettingsManager.Set(VIEW_NAME, "LongEndOffset", UI_Text_LongEndOffset.Text);

                SettingsManager.Set(VIEW_NAME, "TopType", (UI_Combo_TopType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BottomType", (UI_Combo_BottomType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TransType", (UI_Combo_TransType.SelectedItem as RebarBarType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TransShape", (UI_Combo_TransShape.SelectedItem as RebarShape)?.Name ?? "");

                SettingsManager.Set(VIEW_NAME, "TopHook", (UI_Combo_TopHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "BottomHook", (UI_Combo_BotHookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TransHookStart", (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name ?? "");
                SettingsManager.Set(VIEW_NAME, "TransHookEnd", (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name ?? "");

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

        private void UI_Check_TopBars_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Group_Top != null)
                UI_Group_Top.Visibility = UI_Check_TopBars.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void UI_Check_BottomBars_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Group_Bottom != null)
                UI_Group_Bottom.Visibility = UI_Check_BottomBars.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        // Data Accessors
        public RebarBarType TopBarType => UI_Combo_TopType.SelectedItem as RebarBarType;
        public int TopCount => int.TryParse(UI_Text_TopCount.Text, out int i) ? i : 0;
        public RebarHookType TopHook => UI_Combo_TopHookStart.SelectedItem as RebarHookType;
        public bool EnableTop => UI_Check_TopBars.IsChecked == true;

        public RebarBarType BottomBarType => UI_Combo_BottomType.SelectedItem as RebarBarType;
        public int BottomCount => int.TryParse(UI_Text_BottomCount.Text, out int i) ? i : 0;
        public RebarHookType BottomHook => UI_Combo_BotHookStart.SelectedItem as RebarHookType;
        public bool EnableBottom => UI_Check_BottomBars.IsChecked == true;

        public RebarBarType TransBarType => UI_Combo_TransType.SelectedItem as RebarBarType;
        public RebarShape TransShape => UI_Combo_TransShape.SelectedItem as RebarShape;
        public double TransSpacingMM => double.TryParse(UI_Text_TransSpacing.Text, out double d) ? d : 200;
        public RebarHookType TransHookStart => UI_Combo_HookStart.SelectedItem as RebarHookType;
        public RebarHookType TransHookEnd => UI_Combo_HookEnd.SelectedItem as RebarHookType;
        public double TransStartOffsetMM => double.TryParse(UI_Text_TransStartOffset.Text, out double d) ? d : 50;

        public double LongStartOffsetMM => double.TryParse(UI_Text_LongStartOffset.Text, out double d) ? d : 50;
        public double LongEndOffsetMM => double.TryParse(UI_Text_LongEndOffset.Text, out double d) ? d : 50;

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
    }
}
