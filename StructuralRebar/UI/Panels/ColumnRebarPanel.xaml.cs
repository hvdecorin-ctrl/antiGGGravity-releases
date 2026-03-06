using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            DrawCrossSection();
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

            // Starter Bar Type
            UI_Combo_StarterType.ItemsSource = _rebarTypes;
            UI_Combo_StarterType.DisplayMemberPath = "Name";
            UI_Combo_StarterType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D20")) ?? _rebarTypes.FirstOrDefault();

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

            UI_Combo_StarterHook.ItemsSource = _hookList;
            UI_Combo_StarterHook.DisplayMemberPath = "Name";
            UI_Combo_StarterHook.SelectedIndex = 0;
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

                // Multi-level settings
                UI_Check_MultiLevel.IsChecked = SettingsManager.GetBool(VIEW_NAME, "MultiLevel", false);
                UI_Check_Starters.IsChecked = SettingsManager.GetBool(VIEW_NAME, "EnableStarters", false);
                UI_Text_StarterDevLength.Text = SettingsManager.Get(VIEW_NAME, "StarterDevLength", "0");
                SelectByName(UI_Combo_StarterType, SettingsManager.Get(VIEW_NAME, "StarterType"));
                SelectHookByName(UI_Combo_StarterHook, SettingsManager.Get(VIEW_NAME, "StarterHook"));

                // Lap Length Mode
                UI_Text_LapSplice.Text = SettingsManager.Get(VIEW_NAME, "LapSplice", "600");
                string lapMode = SettingsManager.Get(VIEW_NAME, "LapMode", "Auto (Code)");
                foreach (ComboBoxItem item in UI_Combo_LapMode.Items)
                {
                    if (item.Content.ToString() == lapMode)
                    {
                        UI_Combo_LapMode.SelectedItem = item;
                        break;
                    }
                }
                UI_Combo_LapMode_Changed(null, null);

                string splicePos = SettingsManager.Get(VIEW_NAME, "SplicePosition", "Above Slab (Code Default)");
                foreach (ComboBoxItem item in UI_Combo_SplicePos.Items)
                {
                    if (item.Content.ToString() == splicePos)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }

                string crankPos = SettingsManager.Get(VIEW_NAME, "CrankPosition", "Upper Column");
                foreach (ComboBoxItem item in UI_Combo_CrankPos.Items)
                {
                    if (item.Content.ToString() == crankPos)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }

                // Apply visibility
                MultiLevel_Changed(null, null);
                Starters_Changed(null, null);
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

                SettingsManager.Set(VIEW_NAME, "VerticalTypeX", TransTypeName(UI_Combo_VerticalTypeX));
                SettingsManager.Set(VIEW_NAME, "VerticalTypeY", TransTypeName(UI_Combo_VerticalTypeY));
                SettingsManager.Set(VIEW_NAME, "TieType", TransTypeName(UI_Combo_TieType));

                SettingsManager.Set(VIEW_NAME, "VHookBot", HookName(UI_Combo_VHookBot));
                SettingsManager.Set(VIEW_NAME, "VHookTop", HookName(UI_Combo_VHookTop));
                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));

                SettingsManager.Set(VIEW_NAME, "TieDistUnEQ", (UI_Radio_TieUnEQ.IsChecked == true).ToString());

                // Multi-level settings
                SettingsManager.Set(VIEW_NAME, "MultiLevel", (UI_Check_MultiLevel.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "EnableStarters", (UI_Check_Starters.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "CrankPosition", (UI_Combo_CrankPos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Upper Column");
                SettingsManager.Set(VIEW_NAME, "StarterDevLength", UI_Text_StarterDevLength.Text);
                SettingsManager.Set(VIEW_NAME, "StarterType", TransTypeName(UI_Combo_StarterType));
                SettingsManager.Set(VIEW_NAME, "StarterHook", HookName(UI_Combo_StarterHook));
                SettingsManager.Set(VIEW_NAME, "SplicePosition", (UI_Combo_SplicePos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Above Slab (Code Default)");
                SettingsManager.Set(VIEW_NAME, "LapMode", (UI_Combo_LapMode.SelectedItem as ComboBoxItem)?.Content.ToString());
                SettingsManager.Set(VIEW_NAME, "LapSplice", UI_Text_LapSplice.Text);

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
                TransverseHookStartName = (UI_Combo_HookStart.SelectedItem as HookViewModel)?.Hook?.Name,
                TransverseHookEndName = (UI_Combo_HookEnd.SelectedItem as HookViewModel)?.Hook?.Name,

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
                EnableLapSplice = false, // Set by window/handler now

                // Multi-Level
                MultiLevel = (UI_Check_MultiLevel.IsChecked == true),
                SplicePosition = (UI_Combo_SplicePos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Above Slab",
                CrankPosition = (UI_Combo_CrankPos.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Upper Column",
                VerticalContinuousSpliceLength = IsAutoLapMode()
                    ? 0  // Auto: engine uses code-calculated lap length
                    : UnitConversion.MmToFeet(ParseDouble(UI_Text_LapSplice.Text, 600)),

                // Starter Bars
                EnableStarterBars = (UI_Check_Starters.IsChecked == true),
                StarterBarTypeName = (UI_Combo_StarterType.SelectedItem as RebarBarType)?.Name,
                StarterHookEndName = (UI_Combo_StarterHook.SelectedItem as HookViewModel)?.Hook?.Name,
                StarterDevLength = UnitConversion.MmToFeet(ParseDouble(UI_Text_StarterDevLength.Text, 0)),
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

        // ── Event Handlers ──

        public void TieDist_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_TieZoneInfo == null) return;
            UI_TieZoneInfo.Visibility = (UI_Radio_TieUnEQ.IsChecked == true)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void MultiLevel_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_MultiLevelFields == null) return;
            UI_Panel_MultiLevelFields.Visibility = (UI_Check_MultiLevel.IsChecked == true)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void UI_Combo_LapMode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (UI_Text_LapSplice == null) return;
            UI_Text_LapSplice.Visibility = IsAutoLapMode()
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
        }

        private bool IsAutoLapMode()
        {
            string mode = (UI_Combo_LapMode?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Auto (Code)";
            return mode.StartsWith("Auto", StringComparison.OrdinalIgnoreCase);
        }

        private void Starters_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_StarterFields == null) return;
            bool show = (UI_Check_Starters.IsChecked == true);
            UI_Panel_StarterFields.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void BarCount_Changed(object sender, TextChangedEventArgs e)
        {
            DrawCrossSection();
        }

        // ── Cross-Section Preview Drawing ──

        private void DrawCrossSection()
        {
            if (UI_CrossSectionCanvas == null) return;

            UI_CrossSectionCanvas.Children.Clear();

            int nx = (int)ParseDouble(UI_Text_CountX?.Text, 3);
            int ny = (int)ParseDouble(UI_Text_CountY?.Text, 3);

            if (nx < 1) nx = 1;
            if (ny < 1) ny = 1;

            double canvasW = UI_CrossSectionCanvas.Width;
            double canvasH = UI_CrossSectionCanvas.Height;
            double margin = 20;

            // Concrete outline
            double rectW = canvasW - 2 * margin;
            double rectH = canvasH - 2 * margin;

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = rectW,
                Height = rectH,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEB, 0xEF)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(rect, margin);
            Canvas.SetTop(rect, margin);
            UI_CrossSectionCanvas.Children.Add(rect);

            // Bar positions
            double barMargin = 14;
            double barRadius = 4.5;
            double innerLeft = margin + barMargin;
            double innerTop = margin + barMargin;
            double innerRight = canvasW - margin - barMargin;
            double innerBottom = canvasH - margin - barMargin;
            double innerW = innerRight - innerLeft;
            double innerH = innerBottom - innerTop;

            // Tie rectangle (inside cover)
            var tie = new System.Windows.Shapes.Rectangle
            {
                Width = innerW + barRadius * 2 + 2,
                Height = innerH + barRadius * 2 + 2,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x70, 0x8B)),
                StrokeThickness = 1.2,
                Fill = Brushes.Transparent,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(tie, innerLeft - barRadius - 1);
            Canvas.SetTop(tie, innerTop - barRadius - 1);
            UI_CrossSectionCanvas.Children.Add(tie);

            // Generate bar positions (mirroring ColumnLayoutGenerator logic)
            var barPositions = new List<(double X, double Y)>();

            // Bottom face (y = bottom)
            for (int i = 0; i < nx; i++)
            {
                double x = nx > 1 ? innerLeft + innerW * i / (nx - 1) : (innerLeft + innerRight) / 2;
                barPositions.Add((x, innerBottom));
            }

            // Top face (y = top)
            if (ny > 1)
            {
                for (int i = 0; i < nx; i++)
                {
                    double x = nx > 1 ? innerLeft + innerW * i / (nx - 1) : (innerLeft + innerRight) / 2;
                    barPositions.Add((x, innerTop));
                }
            }

            // Left face inner bars
            int nyInner = ny - 2;
            if (nyInner > 0)
            {
                for (int j = 0; j < nyInner; j++)
                {
                    double y = innerTop + innerH * (j + 1) / (ny - 1);
                    barPositions.Add((innerLeft, y));
                }
            }

            // Right face inner bars
            if (nyInner > 0 && nx > 1)
            {
                for (int j = 0; j < nyInner; j++)
                {
                    double y = innerTop + innerH * (j + 1) / (ny - 1);
                    barPositions.Add((innerRight, y));
                }
            }

            // Draw bars
            var barBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x70, 0x8B));
            foreach (var (bx, by) in barPositions)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = barRadius * 2,
                    Height = barRadius * 2,
                    Fill = barBrush,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x55, 0x77)),
                    StrokeThickness = 0.8
                };
                Canvas.SetLeft(dot, bx - barRadius);
                Canvas.SetTop(dot, by - barRadius);
                UI_CrossSectionCanvas.Children.Add(dot);
            }

            // Update label
            if (UI_CrossSectionLabel != null)
                UI_CrossSectionLabel.Text = $"{nx}×{ny}";
        }

        // ── Design Code Info ──

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

        /// <summary>
        /// Updates the stack info text from outside (e.g., after column detection).
        /// </summary>
        public void UpdateStackInfo(string info)
        {
            if (UI_StackInfoText != null)
                UI_StackInfoText.Text = info;
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
