using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class FootingPadRebarPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_FootingPad";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public FootingPadRebarPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSettings();
        }

        private void LoadData()
        {
            _rebarTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name)
                .ToList();

            UI_Combo_TopType.ItemsSource = _rebarTypes;
            UI_Combo_TopType.DisplayMemberPath = "Name";
            UI_Combo_TopType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_BotType.ItemsSource = _rebarTypes;
            UI_Combo_BotType.DisplayMemberPath = "Name";
            UI_Combo_BotType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

            UI_Combo_TopHook.ItemsSource = _hookList;
            UI_Combo_TopHook.DisplayMemberPath = "Name";
            UI_Combo_TopHook.SelectedIndex = 0;

            UI_Combo_BotHook.ItemsSource = _hookList;
            UI_Combo_BotHook.DisplayMemberPath = "Name";
            UI_Combo_BotHook.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_TopSpacing.Text = SettingsManager.Get(VIEW_NAME, "TopSpacing", "200");
                UI_Text_BotSpacing.Text = SettingsManager.Get(VIEW_NAME, "BotSpacing", "200");

                UI_Check_TopBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopBarsEnabled", true);
                UI_Check_BotBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotBarsEnabled", true);

                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"));
                SelectByName(UI_Combo_BotType, SettingsManager.Get(VIEW_NAME, "BotType"));

                SelectHookByName(UI_Combo_TopHook, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BotHook, SettingsManager.Get(VIEW_NAME, "BotHook"));

                UI_Check_TopHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopHookOverride", false);
                UI_Text_TopHookLength.Text = SettingsManager.Get(VIEW_NAME, "TopHookLength", "300");
                UI_Check_BotHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotHookOverride", false);
                UI_Text_BotHookLength.Text = SettingsManager.Get(VIEW_NAME, "BotHookLength", "300");
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "TopSpacing", UI_Text_TopSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "BotSpacing", UI_Text_BotSpacing.Text);

                SettingsManager.Set(VIEW_NAME, "TopBarsEnabled", (UI_Check_TopBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotBarsEnabled", (UI_Check_BotBars.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "TopType", TransTypeName(UI_Combo_TopType));
                SettingsManager.Set(VIEW_NAME, "BotType", TransTypeName(UI_Combo_BotType));

                SettingsManager.Set(VIEW_NAME, "TopHook", HookName(UI_Combo_TopHook));
                SettingsManager.Set(VIEW_NAME, "BotHook", HookName(UI_Combo_BotHook));

                SettingsManager.Set(VIEW_NAME, "TopHookOverride", (UI_Check_TopHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopHookLength", UI_Text_TopHookLength.Text);
                SettingsManager.Set(VIEW_NAME, "BotHookOverride", (UI_Check_BotHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotHookLength", UI_Text_BotHookLength.Text);

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.FootingPad,
                RemoveExisting = false, // Handled by Window level
                Layers = new List<RebarLayerConfig>()
            };

            // Top Layer
            if (UI_Check_TopBars.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Side = RebarSide.Top,
                    VerticalBarTypeName = (UI_Combo_TopType.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopSpacing.Text, 200)),
                    HookStartName = HookName(UI_Combo_TopHook),
                    HookEndName = HookName(UI_Combo_TopHook),
                    OverrideHookLength = UI_Check_TopHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopHookLength.Text, 300))
                });
            }

            // Bottom Layer
            if (UI_Check_BotBars.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Side = RebarSide.Bottom,
                    VerticalBarTypeName = (UI_Combo_BotType.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotSpacing.Text, 200)),
                    HookStartName = HookName(UI_Combo_BotHook),
                    HookEndName = HookName(UI_Combo_BotHook),
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300))
                });
            }

            return request;
        }

        // --- Helpers ---
        private void SelectByName(ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var match = _rebarTypes.FirstOrDefault(x => x.Name == name);
            if (match != null) combo.SelectedItem = match;
        }

        private void SelectHookByName(ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) { combo.SelectedIndex = 0; return; }
            var match = _hookList.FirstOrDefault(x => x?.Hook?.Name == name);
            if (match != null) combo.SelectedItem = match;
            else combo.SelectedIndex = 0;
        }

        private static string TransTypeName(ComboBox combo) => (combo.SelectedItem as RebarBarType)?.Name ?? "";
        private static string HookName(ComboBox combo) => (combo.SelectedItem as HookViewModel)?.Hook?.Name ?? "";

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
