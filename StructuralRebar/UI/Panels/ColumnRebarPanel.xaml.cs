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
    public partial class ColumnRebarPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_Column";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public ColumnRebarPanel(Document doc)
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

            UI_Combo_VerticalTypeX.ItemsSource = _rebarTypes;
            UI_Combo_VerticalTypeX.DisplayMemberPath = "Name";
            UI_Combo_VerticalTypeX.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D25")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_VerticalTypeY.ItemsSource = _rebarTypes;
            UI_Combo_VerticalTypeY.DisplayMemberPath = "Name";
            UI_Combo_VerticalTypeY.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D25")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TieType.ItemsSource = _rebarTypes;
            UI_Combo_TieType.DisplayMemberPath = "Name";
            UI_Combo_TieType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

            UI_Combo_VHookBot.ItemsSource = _hookList;
            UI_Combo_VHookBot.DisplayMemberPath = "Name";
            UI_Combo_VHookBot.SelectedIndex = 0;

            UI_Combo_VHookTop.ItemsSource = _hookList;
            UI_Combo_VHookTop.DisplayMemberPath = "Name";
            UI_Combo_VHookTop.SelectedIndex = 0;

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
                UI_Text_CountX.Text = SettingsManager.Get(VIEW_NAME, "CountX", "3");
                UI_Text_CountY.Text = SettingsManager.Get(VIEW_NAME, "CountY", "3");
                UI_Text_TieSpacing.Text = SettingsManager.Get(VIEW_NAME, "TieSpacing", "200");
                UI_Text_TieBotOffset.Text = SettingsManager.Get(VIEW_NAME, "TieBotOffset", "100");
                UI_Text_TieTopOffset.Text = SettingsManager.Get(VIEW_NAME, "TieTopOffset", "100");
                UI_Text_TopExtValue.Text = SettingsManager.Get(VIEW_NAME, "TopExtValue", "300");
                UI_Text_BotExtValue.Text = SettingsManager.Get(VIEW_NAME, "BotExtValue", "300");

                UI_Check_TopExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopExtEnabled", false);
                UI_Check_BotExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotExtEnabled", false);
                UI_Check_VHookBotOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VHookBotOut", false);
                UI_Check_VHookTopOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VHookTopOut", false);
                UI_Check_CutRebar.IsChecked = SettingsManager.GetBool(VIEW_NAME, "CutRebarEnabled", true);

                SelectByName(UI_Combo_VerticalTypeX, SettingsManager.Get(VIEW_NAME, "VerticalTypeX"));
                SelectByName(UI_Combo_VerticalTypeY, SettingsManager.Get(VIEW_NAME, "VerticalTypeY"));
                SelectByName(UI_Combo_TieType, SettingsManager.Get(VIEW_NAME, "TieType"));

                SelectHookByName(UI_Combo_VHookBot, SettingsManager.Get(VIEW_NAME, "VHookBot"));
                SelectHookByName(UI_Combo_VHookTop, SettingsManager.Get(VIEW_NAME, "VHookTop"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));

                UI_Radio_TieUnEQ.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TieDistUnEQ", false);
                UI_Radio_TieEQ.IsChecked = !(UI_Radio_TieUnEQ.IsChecked == true);
                TieDist_Changed(null, null);
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "CountX", UI_Text_CountX.Text);
                SettingsManager.Set(VIEW_NAME, "CountY", UI_Text_CountY.Text);
                SettingsManager.Set(VIEW_NAME, "TieSpacing", UI_Text_TieSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TieBotOffset", UI_Text_TieBotOffset.Text);
                SettingsManager.Set(VIEW_NAME, "TieTopOffset", UI_Text_TieTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "TopExtValue", UI_Text_TopExtValue.Text);
                SettingsManager.Set(VIEW_NAME, "BotExtValue", UI_Text_BotExtValue.Text);

                SettingsManager.Set(VIEW_NAME, "TopExtEnabled", (UI_Check_TopExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotExtEnabled", (UI_Check_BotExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "VHookBotOut", (UI_Check_VHookBotOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "VHookTopOut", (UI_Check_VHookTopOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "CutRebarEnabled", (UI_Check_CutRebar.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "VerticalTypeX", TransTypeName(UI_Combo_VerticalTypeX));
                SettingsManager.Set(VIEW_NAME, "VerticalTypeY", TransTypeName(UI_Combo_VerticalTypeY));
                SettingsManager.Set(VIEW_NAME, "TieType", TransTypeName(UI_Combo_TieType));

                SettingsManager.Set(VIEW_NAME, "VHookBot", HookName(UI_Combo_VHookBot));
                SettingsManager.Set(VIEW_NAME, "VHookTop", HookName(UI_Combo_VHookTop));
                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));

                SettingsManager.Set(VIEW_NAME, "TieDistUnEQ", (UI_Radio_TieUnEQ.IsChecked == true).ToString());

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            double tieSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieSpacing.Text, 200));
            double tieBotOff = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieBotOffset.Text, 100));
            double tieTopOff = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieTopOffset.Text, 100));

            var request = new RebarRequest
            {
                HostType = ElementHostType.Column,
                
                // Ties (Transverse)
                TransverseBarTypeName = (UI_Combo_TieType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = tieSpacing,
                TransverseStartOffset = tieBotOff, // Start = Bottom
                TransverseEndOffset = tieTopOff,   // End = Top
                TransverseHookStartName = (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name,
                TransverseHookEndName = (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name,

                // Vertical Bars
                ColumnCountX = (int)ParseDouble(UI_Text_CountX.Text, 3),
                ColumnCountY = (int)ParseDouble(UI_Text_CountY.Text, 3),
                VerticalBarTypeNameX = (UI_Combo_VerticalTypeX.SelectedItem as RebarBarType)?.Name,
                VerticalBarTypeNameY = (UI_Combo_VerticalTypeY.SelectedItem as RebarBarType)?.Name,
                VerticalTopExtension = UI_Check_TopExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_TopExtValue.Text, 300)) : 0,
                VerticalBottomExtension = UI_Check_BotExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_BotExtValue.Text, 300)) : 0,
                
                // Vertical Hooks
                Layers = new List<RebarLayerConfig>(),
                EnableZoneSpacing = (UI_Radio_TieUnEQ.IsChecked == true),
                EnableLapSplice = (UI_Check_CutRebar.IsChecked == true),
            };
 
            // Simplified: vertical bars use First Layer template for hooks
            request.Layers.Add(new RebarLayerConfig
            {
                HookStartName = (UI_Combo_VHookBot.SelectedItem as HookViewModel)?.Hook?.Name,
                HookEndName = (UI_Combo_VHookTop.SelectedItem as HookViewModel)?.Hook?.Name,
                HookStartOutward = UI_Check_VHookBotOut.IsChecked == true,
                HookEndOutward = UI_Check_VHookTopOut.IsChecked == true
            });

            return request;
        }

        public void TieDist_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            if (UI_TieZoneInfo == null) return;
            UI_TieZoneInfo.Visibility = (UI_Radio_TieUnEQ.IsChecked == true)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        public void UpdateZoneInfo(DesignCodeStandard code)
        {
            if (UI_TieZoneTitle == null) return;

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    UI_TieZoneTitle.Text = "3-Zone Layout (ACI 318):";
                    UI_TieZoneLine1.Text = "├─ Bottom Confinement: l_o length, s_o spacing";
                    UI_TieZoneLine2.Text = "├─ Mid Span:           H-2×l_o,   2×s_o";
                    UI_TieZoneLine3.Text = "└─ Top Confinement:    l_o length, s_o spacing";
                    UI_TieZoneNote.Text = "l_o = max(H/6, D, 450mm), s_o = min(D/4, 6db, 150mm)";
                    break;

                case DesignCodeStandard.AS3600:
                    UI_TieZoneTitle.Text = "3-Zone Layout (AS 3600):";
                    UI_TieZoneLine1.Text = "├─ Bottom Confinement: l_o length, s_o spacing";
                    UI_TieZoneLine2.Text = "├─ Mid Span:           H-2×l_o,   2×s_o";
                    UI_TieZoneLine3.Text = "└─ Top Confinement:    l_o length, s_o spacing";
                    UI_TieZoneNote.Text = "l_o = max(H/6, D, 450mm), s_o = min(D/2, 15db, 300mm)";
                    break;

                case DesignCodeStandard.EC2:
                    UI_TieZoneTitle.Text = "3-Zone Layout (Eurocode 2):";
                    UI_TieZoneLine1.Text = "├─ Bottom Confinement: l_o length, s_o spacing";
                    UI_TieZoneLine2.Text = "├─ Mid Span:           H-2×l_o,   relaxed";
                    UI_TieZoneLine3.Text = "└─ Top Confinement:    l_o length, s_o spacing";
                    UI_TieZoneNote.Text = "l_o = max(H/6, D, 450mm), s_o = min(D/2, 8db, 175mm)";
                    break;

                case DesignCodeStandard.NZS3101:
                    UI_TieZoneTitle.Text = "3-Zone Layout (NZS 3101):";
                    UI_TieZoneLine1.Text = "├─ Bottom Confinement: l_o length, D/4 spacing";
                    UI_TieZoneLine2.Text = "├─ Mid Span:           H-2×l_o,   D/2";
                    UI_TieZoneLine3.Text = "└─ Top Confinement:    l_o length, D/4 spacing";
                    UI_TieZoneNote.Text = "l_o = max(H/6, D, 450mm), end = min(D/4, 100mm)";
                    break;

                default:
                    UI_TieZoneTitle.Text = "3-Zone Layout (Custom):";
                    UI_TieZoneLine1.Text = "├─ Bottom Confinement: l_o length, 100mm spacing";
                    UI_TieZoneLine2.Text = "├─ Mid Span:           H-2×l_o,   200mm";
                    UI_TieZoneLine3.Text = "└─ Top Confinement:    l_o length, 100mm spacing";
                    UI_TieZoneNote.Text = "l_o = max(H/6, D, 450mm)";
                    break;
            }
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
