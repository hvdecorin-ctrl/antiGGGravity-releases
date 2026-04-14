using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class CircularColumnPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_CircularColumn";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public CircularColumnPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSettings();
            UpdateDisplay(null, null);
        }

        private void LoadData()
        {
            _rebarTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name)
                .ToList();

            UI_Combo_MainType.ItemsSource = _rebarTypes;
            UI_Combo_MainType.DisplayMemberPath = "Name";
            UI_Combo_MainType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D20")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TransType.ItemsSource = _rebarTypes;
            UI_Combo_TransType.DisplayMemberPath = "Name";
            UI_Combo_TransType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

            UI_Combo_MainHook.ItemsSource = _hookList;
            UI_Combo_MainHook.DisplayMemberPath = "Name";
            UI_Combo_MainHook.SelectedIndex = 0;

            UI_Combo_HookBot.ItemsSource = _hookList.ToList();
            UI_Combo_HookBot.DisplayMemberPath = "Name";
            UI_Combo_HookBot.SelectedIndex = 0;

            UI_Combo_StarterType.ItemsSource = _rebarTypes;
            UI_Combo_StarterType.DisplayMemberPath = "Name";
            UI_Combo_StarterType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D20")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_StarterHook.ItemsSource = _hookList.ToList();
            UI_Combo_StarterHook.DisplayMemberPath = "Name";
            UI_Combo_StarterHook.SelectedIndex = 0;

            UI_Combo_TransHookStart.ItemsSource = _hookList.ToList();
            UI_Combo_TransHookStart.DisplayMemberPath = "Name";
            UI_Combo_TransHookStart.SelectedIndex = 0;

            UI_Combo_TransHookEnd.ItemsSource = _hookList.ToList();
            UI_Combo_TransHookEnd.DisplayMemberPath = "Name";
            UI_Combo_TransHookEnd.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_MainCount.Text = SettingsManager.Get(VIEW_NAME, "MainCount", "8");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Combo_TransMode.SelectedIndex = SettingsManager.GetInt(VIEW_NAME, "TransMode", 0);

                SelectByName(UI_Combo_MainType, SettingsManager.Get(VIEW_NAME, "MainType"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));
                SelectHookByName(UI_Combo_MainHook, SettingsManager.Get(VIEW_NAME, "MainHook"));
                SelectHookByName(UI_Combo_HookBot, SettingsManager.Get(VIEW_NAME, "HookBot"));
                UI_Check_HookBotOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "HookBotOut", false);
                UI_Check_HookTopOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "HookTopOut", false);
                UI_Check_BotExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotExt", false);
                UI_Text_BotExtValue.Text = SettingsManager.Get(VIEW_NAME, "BotExtValue", "300");
                UI_Check_TopExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopExt", false);
                UI_Text_TopExtValue.Text = SettingsManager.Get(VIEW_NAME, "TopExtValue", "300");
                SelectByName(UI_Combo_StarterType, SettingsManager.Get(VIEW_NAME, "StarterType"));
                SelectHookByName(UI_Combo_StarterHook, SettingsManager.Get(VIEW_NAME, "StarterHook"));

                UI_Check_MultiLevel.IsChecked = SettingsManager.GetBool(VIEW_NAME, "MultiLevel", false);
                UI_Combo_SplicePos.SelectedIndex = SettingsManager.GetInt(VIEW_NAME, "SplicePos", 0);
                UI_Combo_CrankPos.SelectedIndex = SettingsManager.GetInt(VIEW_NAME, "CrankPos", 1);
                UI_Combo_LapMode.SelectedIndex = SettingsManager.GetInt(VIEW_NAME, "LapMode", 0);
                UI_Text_LapSplice.Text = SettingsManager.Get(VIEW_NAME, "LapSplice", "40");
                UI_Text_StarterDevLength.Text = SettingsManager.Get(VIEW_NAME, "StarterDevLength", "0");
                UI_Check_StarterOnly.IsChecked = SettingsManager.GetBool(VIEW_NAME, "StarterOnly", false);

                SelectHookByName(UI_Combo_TransHookStart, SettingsManager.Get(VIEW_NAME, "TransHookStart"));
                SelectHookByName(UI_Combo_TransHookEnd, SettingsManager.Get(VIEW_NAME, "TransHookEnd"));
                UI_Radio_TieUnEQ.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TieDistUnEQ", false);
                UI_Radio_TieEQ.IsChecked = !(UI_Radio_TieUnEQ.IsChecked == true);
                
                // Set halftone states
                UpdateHooksExtHalftone(null, null);
                MultiLevel_Changed(null, null);
                Starters_Changed(null, null);
                TieDist_Changed(null, null);
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "MainCount", UI_Text_MainCount.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransMode", UI_Combo_TransMode.SelectedIndex.ToString());

                SettingsManager.Set(VIEW_NAME, "MainType", TransTypeName(UI_Combo_MainType));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));
                SettingsManager.Set(VIEW_NAME, "MainHook", HookName(UI_Combo_MainHook));
                SettingsManager.Set(VIEW_NAME, "HookBot", HookName(UI_Combo_HookBot));
                SettingsManager.Set(VIEW_NAME, "HookBotOut", (UI_Check_HookBotOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "HookTopOut", (UI_Check_HookTopOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotExt", (UI_Check_BotExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotExtValue", UI_Text_BotExtValue.Text);
                SettingsManager.Set(VIEW_NAME, "TopExt", (UI_Check_TopExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopExtValue", UI_Text_TopExtValue.Text);
                SettingsManager.Set(VIEW_NAME, "StarterType", TransTypeName(UI_Combo_StarterType));
                SettingsManager.Set(VIEW_NAME, "StarterHook", HookName(UI_Combo_StarterHook));

                SettingsManager.Set(VIEW_NAME, "MultiLevel", (UI_Check_MultiLevel.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SplicePos", UI_Combo_SplicePos.SelectedIndex.ToString());
                SettingsManager.Set(VIEW_NAME, "CrankPos", UI_Combo_CrankPos.SelectedIndex.ToString());
                SettingsManager.Set(VIEW_NAME, "LapMode", UI_Combo_LapMode.SelectedIndex.ToString());
                SettingsManager.Set(VIEW_NAME, "LapSplice", UI_Text_LapSplice.Text);
                SettingsManager.Set(VIEW_NAME, "Starters", (UI_Check_Starters.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "StarterOnly", (UI_Check_StarterOnly.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "StarterDevLength", UI_Text_StarterDevLength.Text);

                SettingsManager.Set(VIEW_NAME, "TransHookStart", HookName(UI_Combo_TransHookStart));
                SettingsManager.Set(VIEW_NAME, "TransHookEnd", HookName(UI_Combo_TransHookEnd));
                SettingsManager.Set(VIEW_NAME, "TieDistUnEQ", (UI_Radio_TieUnEQ.IsChecked == true).ToString());

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            double lapMultiplier = ParseDouble(UI_Text_LapSplice.Text, 40);
            double vBarDia = (_rebarTypes.FirstOrDefault(x => x.Name == TransTypeName(UI_Combo_MainType)))?.BarModelDiameter ?? 0;
            double manuallyCalculatedLap = lapMultiplier * vBarDia;
            bool isAutoLap = UI_Combo_LapMode.SelectedIndex == 0;

            var request = new RebarRequest
            {
                HostType = ElementHostType.Column,
                IsCircularColumn = true,
                RemoveExisting = false,
                
                VerticalBarTypeName = TransTypeName(UI_Combo_MainType),
                PileBarCount = ParseInt(UI_Text_MainCount.Text, 8),
                
                TransverseBarTypeName = TransTypeName(UI_Combo_TransType),
                TransverseSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransSpacing.Text, 200)),
                EnableSpiral = UI_Combo_TransMode.SelectedIndex == 1,
                TransverseHookStartName = HookName(UI_Combo_TransHookStart),
                TransverseHookEndName = HookName(UI_Combo_TransHookEnd),
                EnableZoneSpacing = (UI_Radio_TieUnEQ.IsChecked == true),

                // Vertical extensions
                VerticalTopExtension = UI_Check_TopExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_TopExtValue.Text, 300)) : 0,
                VerticalBottomExtension = UI_Check_BotExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_BotExtValue.Text, 300)) : 0,

                // Multi-Level
                MultiLevel = (UI_Check_MultiLevel.IsChecked == true),
                SplicePosition = (UI_Combo_SplicePos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Above Slab",
                CrankPosition = (UI_Combo_CrankPos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Upper Bar",
                LapSpliceMode = isAutoLap ? "Auto" : "Manual",
                LapSpliceLength = manuallyCalculatedLap,
                VerticalContinuousSpliceLength = isAutoLap ? 0 : manuallyCalculatedLap,

                // Starters
                EnableStarterBars = (UI_Check_Starters.IsChecked == true),
                StarterOnly = (UI_Check_StarterOnly.IsChecked == true),
                StarterBarTypeName = TransTypeName(UI_Combo_StarterType),
                StarterHookEndName = HookName(UI_Combo_StarterHook),
                StarterDevLength = UnitConversion.MmToFeet(ParseDouble(UI_Text_StarterDevLength.Text, 0)),
            };

            // Vertical bar hooks -> same pattern as standard column (Layers[0])
            request.Layers = new List<RebarLayerConfig>
            {
                new RebarLayerConfig
                {
                    HookStartName = HookName(UI_Combo_HookBot),
                    HookEndName = HookName(UI_Combo_MainHook),
                    HookStartOutward = UI_Check_HookBotOut.IsChecked == true,
                    HookEndOutward = UI_Check_HookTopOut.IsChecked == true
                }
            };

            return request;
        }

        private void HookCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateHooksExtHalftone(null, null);
            UpdateDisplay(null, null);
        }

        private void UpdateHooksExtHalftone(object sender, RoutedEventArgs e)
        {
            if (UI_Combo_HookBot == null) return;

            // Bottom Hook halftone
            bool hasBotHook = (UI_Combo_HookBot.SelectedItem as HookViewModel)?.Hook != null;
            UI_Check_HookBotOut.IsEnabled = hasBotHook;
            UI_Check_HookBotOut.Opacity = hasBotHook ? 1.0 : 0.5;
            UI_Check_BotExt.IsEnabled = hasBotHook;
            UI_Check_BotExt.Opacity = hasBotHook ? 1.0 : 0.5;
            
            bool hasBotExt = hasBotHook && UI_Check_BotExt.IsChecked == true;
            UI_Text_BotExtValue.IsEnabled = hasBotExt;
            UI_Text_BotExtValue.Opacity = hasBotExt ? 1.0 : 0.5;

            // Top Hook halftone
            bool hasTopHook = (UI_Combo_MainHook.SelectedItem as HookViewModel)?.Hook != null;
            UI_Check_HookTopOut.IsEnabled = hasTopHook;
            UI_Check_HookTopOut.Opacity = hasTopHook ? 1.0 : 0.5;
            UI_Check_TopExt.IsEnabled = hasTopHook;
            UI_Check_TopExt.Opacity = hasTopHook ? 1.0 : 0.5;

            bool hasTopExt = hasTopHook && UI_Check_TopExt.IsChecked == true;
            UI_Text_TopExtValue.IsEnabled = hasTopExt;
            UI_Text_TopExtValue.Opacity = hasTopExt ? 1.0 : 0.5;
        }

        private void MultiLevel_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_MultiLevelFields == null || UI_Check_MultiLevel == null) return;
            bool isChecked = UI_Check_MultiLevel.IsChecked == true;
            UI_Panel_MultiLevelFields.IsEnabled = isChecked;
            UI_Panel_MultiLevelFields.Opacity = isChecked ? 1.0 : 0.5;
        }

        private void Starters_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_StarterFields == null || UI_Check_Starters == null || UI_Check_StarterOnly == null) return;
            bool isChecked = UI_Check_Starters.IsChecked == true;
            bool starterOnly = UI_Check_StarterOnly.IsChecked == true;

            // StarterOnly auto-enables Starters
            if (starterOnly && !isChecked)
            {
                UI_Check_Starters.IsChecked = true;
                isChecked = true;
            }

            // StarterOnly only available when Starters enabled
            UI_Check_StarterOnly.IsEnabled = isChecked;
            UI_Check_StarterOnly.Opacity = isChecked ? 1.0 : 0.5;

            UI_Panel_StarterFields.IsEnabled = isChecked;
            UI_Panel_StarterFields.Opacity = isChecked ? 1.0 : 0.5;
        }

        private void TieDist_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_TieZoneInfo == null) return;
            UI_TieZoneInfo.Visibility = (UI_Radio_TieUnEQ.IsChecked == true)
                ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
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

        private void UpdateDisplay(object sender, RoutedEventArgs e)
        {
            if (UI_Canvas_Preview == null) return;
            
            // Update labels
            if (UI_Label_Spacing != null)
                UI_Label_Spacing.Text = (UI_Combo_TransMode.SelectedIndex == 1) ? "Pitch" : "Spacing";

            var canvas = UI_Canvas_Preview;
            canvas.Children.Clear();

            double center = 100;
            double radius = 80;

            // Pile circle
            var pileCircle = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = Brushes.LightGray,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
            };
            Canvas.SetLeft(pileCircle, center - radius);
            Canvas.SetTop(pileCircle, center - radius);
            canvas.Children.Add(pileCircle);

            // Transverse (Hoop/Spiral)
            double hoopRadius = radius - 15;
            var hoop = new System.Windows.Shapes.Ellipse
            {
                Width = hoopRadius * 2,
                Height = hoopRadius * 2,
                Stroke = Brushes.SlateGray,
                StrokeThickness = 2
            };
            Canvas.SetLeft(hoop, center - hoopRadius);
            Canvas.SetTop(hoop, center - hoopRadius);
            canvas.Children.Add(hoop);

            // Main Bars
            int count = ParseInt(UI_Text_MainCount.Text, 8);
            double barRadius = hoopRadius;
            double dotR = 5;

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                double x = center + barRadius * Math.Cos(angle);
                double y = center + barRadius * Math.Sin(angle);

                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = dotR * 2,
                    Height = dotR * 2,
                    Fill = Brushes.MidnightBlue,
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(dot, x - dotR);
                Canvas.SetTop(dot, y - dotR);
                canvas.Children.Add(dot);
            }
        }

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

        private double ParseDouble(string text, double defaultValue) => double.TryParse(text, out double result) ? result : defaultValue;
        private int ParseInt(string text, int defaultValue) => int.TryParse(text, out int result) ? result : defaultValue;
    }
}
