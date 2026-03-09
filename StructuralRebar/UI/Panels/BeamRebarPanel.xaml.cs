using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.DTO;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class BeamRebarPanel : UserControl
    {
        private readonly Document _doc;
        private const string VIEW_NAME = "RebarSuite_Beam";
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public BeamRebarPanel(Document doc)
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

            foreach (var combo in new[] { UI_Combo_T1Type, UI_Combo_T2Type, UI_Combo_T3Type, UI_Combo_B1Type, UI_Combo_B2Type, UI_Combo_B3Type, UI_Combo_TransType, UI_Combo_SideType })
            {
                combo.ItemsSource = _rebarTypes;
                combo.DisplayMemberPath = "Name";
            }

            var d16 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();
            UI_Combo_T1Type.SelectedItem = d16;
            UI_Combo_T2Type.SelectedItem = d16;
            UI_Combo_T3Type.SelectedItem = d16;
            UI_Combo_B1Type.SelectedItem = d16;
            UI_Combo_B2Type.SelectedItem = d16;
            UI_Combo_B3Type.SelectedItem = d16;

            var r10 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("R10"))
                   ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("R6"))
                   ?? _rebarTypes.FirstOrDefault();
            UI_Combo_TransType.SelectedItem = r10;

            var h12 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("H12"))
                   ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12"))
                   ?? d16;
            UI_Combo_SideType.SelectedItem = h12;

            // Hook Types
            var hooks = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hooks.Select(h => new HookViewModel(h)));

            foreach (var combo in new[] { UI_Combo_HookStart, UI_Combo_HookEnd,
                UI_Combo_TopHookStart, UI_Combo_TopHookEnd,
                UI_Combo_BotHookStart, UI_Combo_BotHookEnd })
            {
                combo.ItemsSource = _hookList;
                combo.DisplayMemberPath = "Name";
                combo.SelectedIndex = 0;
            }
        }

        private void LoadSettings()
        {
            try
            {
                UI_Check_T1.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T1Enabled", true);
                UI_Check_T2.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T2Enabled", false);
                UI_Check_T3.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T3Enabled", false);
                UI_Check_B1.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B1Enabled", true);
                UI_Check_B2.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B2Enabled", false);
                UI_Check_B3.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B3Enabled", false);

                UI_Text_T1Count.Text = SettingsManager.Get(VIEW_NAME, "T1Count", "2");
                UI_Text_T2Count.Text = SettingsManager.Get(VIEW_NAME, "T2Count", "3");
                UI_Text_T3Count.Text = SettingsManager.Get(VIEW_NAME, "T3Count", "3");
                UI_Text_B1Count.Text = SettingsManager.Get(VIEW_NAME, "B1Count", "2");
                UI_Text_B2Count.Text = SettingsManager.Get(VIEW_NAME, "B2Count", "3");
                UI_Text_B3Count.Text = SettingsManager.Get(VIEW_NAME, "B3Count", "3");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Text_TransStartOffset.Text = SettingsManager.Get(VIEW_NAME, "TransStartOffset", "50");

                SelectByName(UI_Combo_T1Type, SettingsManager.Get(VIEW_NAME, "T1Type"));
                SelectByName(UI_Combo_T2Type, SettingsManager.Get(VIEW_NAME, "T2Type"));
                SelectByName(UI_Combo_T3Type, SettingsManager.Get(VIEW_NAME, "T3Type"));
                SelectByName(UI_Combo_B1Type, SettingsManager.Get(VIEW_NAME, "B1Type"));
                SelectByName(UI_Combo_B2Type, SettingsManager.Get(VIEW_NAME, "B2Type"));
                SelectByName(UI_Combo_B3Type, SettingsManager.Get(VIEW_NAME, "B3Type"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));

                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));
                SelectHookByName(UI_Combo_TopHookStart, SettingsManager.Get(VIEW_NAME, "TopHookStart"));
                SelectHookByName(UI_Combo_TopHookEnd, SettingsManager.Get(VIEW_NAME, "TopHookEnd"));
                SelectHookByName(UI_Combo_BotHookStart, SettingsManager.Get(VIEW_NAME, "BotHookStart"));
                SelectHookByName(UI_Combo_BotHookEnd, SettingsManager.Get(VIEW_NAME, "BotHookEnd"));

                UI_Check_TopHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopHookOverride", false);
                UI_Text_TopHookLength.Text = SettingsManager.Get(VIEW_NAME, "TopHookLength", "300");
                UI_Check_BotHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotHookOverride", false);
                UI_Text_BotHookLength.Text = SettingsManager.Get(VIEW_NAME, "BotHookLength", "300");

                UI_Radio_StirrupUnEQ.IsChecked = SettingsManager.GetBool(VIEW_NAME, "StirrupDistUnEQ", false);
                UI_Radio_StirrupEQ.IsChecked = !(UI_Radio_StirrupUnEQ.IsChecked == true);

                UI_Check_SideRebar.IsChecked = SettingsManager.GetBool(VIEW_NAME, "SideRebarEnabled", false);
                UI_Check_MultiSpan.IsChecked = SettingsManager.GetBool(VIEW_NAME, "MultiSpanEnabled", false);
                UI_Text_SideRows.Text = SettingsManager.Get(VIEW_NAME, "SideRows", "2");
                SelectByName(UI_Combo_SideType, SettingsManager.Get(VIEW_NAME, "SideType"));

                UI_Text_LayerGap.Text = SettingsManager.Get(VIEW_NAME, "LayerGap", "25");

                UI_Check_T2Cont.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T2Cont", false);
                UI_Check_T3Cont.IsChecked = SettingsManager.GetBool(VIEW_NAME, "T3Cont", false);
                UI_Check_B2Cont.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B2Cont", false);
                UI_Check_B3Cont.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B3Cont", false);



                toggle_visibility(null, null);
                StirrupDist_Changed(null, null);
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "T1Enabled", (UI_Check_T1.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "T2Enabled", (UI_Check_T2.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "T3Enabled", (UI_Check_T3.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B1Enabled", (UI_Check_B1.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B2Enabled", (UI_Check_B2.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B3Enabled", (UI_Check_B3.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "T1Count", UI_Text_T1Count.Text);
                SettingsManager.Set(VIEW_NAME, "T2Count", UI_Text_T2Count.Text);
                SettingsManager.Set(VIEW_NAME, "T3Count", UI_Text_T3Count.Text);
                SettingsManager.Set(VIEW_NAME, "B1Count", UI_Text_B1Count.Text);
                SettingsManager.Set(VIEW_NAME, "B2Count", UI_Text_B2Count.Text);
                SettingsManager.Set(VIEW_NAME, "B3Count", UI_Text_B3Count.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransStartOffset", UI_Text_TransStartOffset.Text);

                SettingsManager.Set(VIEW_NAME, "T1Type", TransTypeName(UI_Combo_T1Type));
                SettingsManager.Set(VIEW_NAME, "T2Type", TransTypeName(UI_Combo_T2Type));
                SettingsManager.Set(VIEW_NAME, "T3Type", TransTypeName(UI_Combo_T3Type));
                SettingsManager.Set(VIEW_NAME, "B1Type", TransTypeName(UI_Combo_B1Type));
                SettingsManager.Set(VIEW_NAME, "B2Type", TransTypeName(UI_Combo_B2Type));
                SettingsManager.Set(VIEW_NAME, "B3Type", TransTypeName(UI_Combo_B3Type));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));

                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));
                SettingsManager.Set(VIEW_NAME, "TopHookStart", HookName(UI_Combo_TopHookStart));
                SettingsManager.Set(VIEW_NAME, "TopHookEnd", HookName(UI_Combo_TopHookEnd));
                SettingsManager.Set(VIEW_NAME, "BotHookStart", HookName(UI_Combo_BotHookStart));
                SettingsManager.Set(VIEW_NAME, "BotHookEnd", HookName(UI_Combo_BotHookEnd));

                SettingsManager.Set(VIEW_NAME, "StirrupDistUnEQ", (UI_Radio_StirrupUnEQ.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "TopHookOverride", (UI_Check_TopHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopHookLength", UI_Text_TopHookLength.Text);
                SettingsManager.Set(VIEW_NAME, "BotHookOverride", (UI_Check_BotHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotHookLength", UI_Text_BotHookLength.Text);

                SettingsManager.Set(VIEW_NAME, "SideRebarEnabled", (UI_Check_SideRebar.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "MultiSpanEnabled", (UI_Check_MultiSpan.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SideRows", UI_Text_SideRows.Text);
                SettingsManager.Set(VIEW_NAME, "SideType", TransTypeName(UI_Combo_SideType));
                SettingsManager.Set(VIEW_NAME, "LayerGap", UI_Text_LayerGap.Text);

                SettingsManager.Set(VIEW_NAME, "T2Cont", (UI_Check_T2Cont.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "T3Cont", (UI_Check_T3Cont.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B2Cont", (UI_Check_B2Cont.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B3Cont", (UI_Check_B3Cont.IsChecked == true).ToString());


                SettingsManager.SaveAll();
            }
            catch { }
        }

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            if (UI_Group_T1 == null) return;
            UI_Group_T1.Visibility = UI_Check_T1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_T2.Visibility = UI_Check_T2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_T3.Visibility = UI_Check_T3.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B1.Visibility = UI_Check_B1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B2.Visibility = UI_Check_B2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B3.Visibility = UI_Check_B3.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_SideRebar.Visibility = UI_Check_SideRebar.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UpdateCrossSection();
            UpdateSpanElevation();
        }

        public void StirrupDist_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_ZoneInfo == null) return;
            UI_ZoneInfo.Visibility = (UI_Radio_StirrupUnEQ.IsChecked == true)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        public void UpdateZoneInfo(DesignCodeStandard code)
        {
            if (UI_ZoneTitle == null) return;

            switch (code)
            {
                case DesignCodeStandard.ACI318:
                    UI_ZoneTitle.Text = "3-Zone Layout (ACI 318):";
                    UI_ZoneLine1.Text = "├─ Left End Zone:  2h length, d/4 spacing";
                    UI_ZoneLine2.Text = "├─ Mid Zone:       remainder, user spacing";
                    UI_ZoneLine3.Text = "└─ Right End Zone: 2h length, d/4 spacing";
                    UI_ZoneNote.Text = "h = beam depth, spacing = min(h/4, s/2, 150mm)";
                    break;

                case DesignCodeStandard.AS3600:
                    UI_ZoneTitle.Text = "3-Zone Layout (AS 3600):";
                    UI_ZoneLine1.Text = "├─ Left End Zone:  2D length, D/4 spacing";
                    UI_ZoneLine2.Text = "├─ Mid Zone:       remainder, user spacing";
                    UI_ZoneLine3.Text = "└─ Right End Zone: 2D length, D/4 spacing";
                    UI_ZoneNote.Text = "D = beam depth, spacing = min(D/4, s/2, 150mm)";
                    break;

                case DesignCodeStandard.EC2:
                    UI_ZoneTitle.Text = "3-Zone Layout (Eurocode 2):";
                    UI_ZoneLine1.Text = "├─ Left End Zone:  1.5h length, h/4 spacing";
                    UI_ZoneLine2.Text = "├─ Mid Zone:       remainder, user spacing";
                    UI_ZoneLine3.Text = "└─ Right End Zone: 1.5h length, h/4 spacing";
                    UI_ZoneNote.Text = "h = beam depth, spacing = min(h/4, s/2, 200mm)";
                    break;

                case DesignCodeStandard.NZS3101:
                    UI_ZoneTitle.Text = "3-Zone Layout (NZS 3101):";
                    UI_ZoneLine1.Text = "├─ Left End Zone:  2h length, d/4 spacing";
                    UI_ZoneLine2.Text = "├─ Mid Zone:       remainder, d/2 spacing";
                    UI_ZoneLine3.Text = "└─ Right End Zone: 2h length, d/4 spacing";
                    UI_ZoneNote.Text = "d = beam depth, end = min(d/4, s/2, 100mm)";
                    break;

                default:
                    UI_ZoneTitle.Text = "3-Zone Layout (Custom):";
                    UI_ZoneLine1.Text = "├─ Left End Zone:  2h length, s/2 spacing";
                    UI_ZoneLine2.Text = "├─ Mid Zone:       remainder, user spacing";
                    UI_ZoneLine3.Text = "└─ Right End Zone: 2h length, s/2 spacing";
                    UI_ZoneNote.Text = "h = beam depth, s = user spacing";
                    break;
            }
        }

        /// <summary>
        /// Builds a RebarRequest from current panel state.
        /// All mm values converted to feet here (single conversion point).
        /// </summary>
        public RebarRequest BuildRequest(bool removeExisting)
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.Beam,
                RemoveExisting = removeExisting,
                TransverseBarTypeName = (UI_Combo_TransType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransSpacing.Text, 200)),
                TransverseStartOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransStartOffset.Text, 50)),
                TransverseHookStartName = HookName(UI_Combo_HookStart),
                TransverseHookEndName = HookName(UI_Combo_HookEnd),
                EnableZoneSpacing = (UI_Radio_StirrupUnEQ.IsChecked == true),
                MultiSpan = (UI_Check_MultiSpan.IsChecked == true),
                EnableLapSplice = false, // Set by window/handler now
                LayerGap = UnitConversion.MmToFeet(ParseDouble(UI_Text_LayerGap.Text, 25)),
            };

            // Top layers
            if (UI_Check_T1.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Exterior,
                    VerticalBarTypeName = (UI_Combo_T1Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_T1Count.Text, 2),  // Count stored in spacing field
                    VerticalOffset = 1, // Positive = top
                    IsContinuous = true, // T1 always continuous
                    HookStartName = HookName(UI_Combo_TopHookStart),
                    HookEndName = HookName(UI_Combo_TopHookEnd),
                    OverrideHookLength = UI_Check_TopHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopHookLength.Text, 300)),
                });
            }
            if (UI_Check_T2.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Exterior,
                    VerticalBarTypeName = (UI_Combo_T2Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_T2Count.Text, 3),
                    VerticalOffset = 1,
                    IsContinuous = (UI_Check_T2Cont.IsChecked == true),
                    HookStartName = HookName(UI_Combo_TopHookStart),
                    HookEndName = HookName(UI_Combo_TopHookEnd),
                    OverrideHookLength = UI_Check_TopHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopHookLength.Text, 300)),
                });
            }
            if (UI_Check_T3.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Exterior,
                    VerticalBarTypeName = (UI_Combo_T3Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_T3Count.Text, 3),
                    VerticalOffset = 1,
                    IsContinuous = (UI_Check_T3Cont.IsChecked == true),
                    HookStartName = HookName(UI_Combo_TopHookStart),
                    HookEndName = HookName(UI_Combo_TopHookEnd),
                    OverrideHookLength = UI_Check_TopHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopHookLength.Text, 300)),
                });
            }

            // Bottom layers
            if (UI_Check_B1.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Interior,
                    VerticalBarTypeName = (UI_Combo_B1Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_B1Count.Text, 2),
                    VerticalOffset = -1, // Negative = bottom
                    IsContinuous = true, // B1 always continuous
                    HookStartName = HookName(UI_Combo_BotHookStart),
                    HookEndName = HookName(UI_Combo_BotHookEnd),
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300)),
                });
            }
            if (UI_Check_B2.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Interior,
                    VerticalBarTypeName = (UI_Combo_B2Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_B2Count.Text, 3),
                    VerticalOffset = -1,
                    IsContinuous = (UI_Check_B2Cont.IsChecked == true),
                    HookStartName = HookName(UI_Combo_BotHookStart),
                    HookEndName = HookName(UI_Combo_BotHookEnd),
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300)),
                });
            }
            if (UI_Check_B3.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Face = RebarLayerFace.Interior,
                    VerticalBarTypeName = (UI_Combo_B3Type.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = ParseInt(UI_Text_B3Count.Text, 3),
                    VerticalOffset = -1,
                    IsContinuous = (UI_Check_B3Cont.IsChecked == true),
                    HookStartName = HookName(UI_Combo_BotHookStart),
                    HookEndName = HookName(UI_Combo_BotHookEnd),
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300)),
                });
            }

            // Side rebar
            if (UI_Check_SideRebar.IsChecked == true)
            {
                request.EnableSideRebar = true;
                request.SideRebarTypeName = (UI_Combo_SideType.SelectedItem as RebarBarType)?.Name;
                request.SideRebarRows = ParseInt(UI_Text_SideRows.Text, 2);
            }

            return request;
        }

        // --- Cross-Section Drawing ---
        private void UpdateCrossSection()
        {
            if (UI_Canvas_CrossSection == null) return;
            var canvas = UI_Canvas_CrossSection;
            canvas.Children.Clear();

            double cW = canvas.Width;   // 200
            double cH = canvas.Height;  // 190
            double marginL = 10;
            double marginR = 55; // reserve right side for labels
            double marginTB = 10;
            double beamW = cW - marginL - marginR;
            double beamH = cH - 2 * marginTB;

            // Concrete outline
            var concreteRect = new System.Windows.Shapes.Rectangle
            {
                Width = beamW, Height = beamH,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                RadiusX = 3, RadiusY = 3
            };
            Canvas.SetLeft(concreteRect, marginL);
            Canvas.SetTop(concreteRect, marginTB);
            canvas.Children.Add(concreteRect);

            // Stirrup outline (dashed)
            double cover = 12;
            var stirrupRect = new System.Windows.Shapes.Rectangle
            {
                Width = beamW - 2 * cover, Height = beamH - 2 * cover,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = Brushes.Transparent,
                RadiusX = 2, RadiusY = 2
            };
            Canvas.SetLeft(stirrupRect, marginL + cover);
            Canvas.SetTop(stirrupRect, marginTB + cover);
            canvas.Children.Add(stirrupRect);

            double dotR = 4;
            double innerL = marginL + cover + 6;
            double innerR = marginL + beamW - cover - 6;
            double innerW = innerR - innerL;

            double layerStep = 16;

            // --- TOP BARS ---
            double topY = marginTB + cover + 10;
            if (UI_Check_T1.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, topY, ParseInt(UI_Text_T1Count.Text, 2), dotR, "T1", Brushes.DarkRed);
                topY += layerStep;
            }
            if (UI_Check_T2.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, topY, ParseInt(UI_Text_T2Count.Text, 3), dotR, "T2", Brushes.OrangeRed);
                topY += layerStep;
            }
            if (UI_Check_T3.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, topY, ParseInt(UI_Text_T3Count.Text, 3), dotR, "T3", Brushes.Gold);
                topY += layerStep;
            }

            // --- BOTTOM BARS ---
            double botY = marginTB + beamH - cover - 10;
            if (UI_Check_B1.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, botY, ParseInt(UI_Text_B1Count.Text, 2), dotR, "B1", Brushes.MidnightBlue);
                botY -= layerStep;
            }
            if (UI_Check_B2.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, botY, ParseInt(UI_Text_B2Count.Text, 3), dotR, "B2", Brushes.ForestGreen);
                botY -= layerStep;
            }
            if (UI_Check_B3.IsChecked == true)
            {
                DrawBarRow(canvas, innerL, innerR, botY, ParseInt(UI_Text_B3Count.Text, 3), dotR, "B3", Brushes.DeepSkyBlue);
                botY -= layerStep;
            }

            // --- SIDE BARS ---
            if (UI_Check_SideRebar.IsChecked == true)
            {
                int sideRows = ParseInt(UI_Text_SideRows.Text, 2);
                double sTop = marginTB + cover + 10 + (UI_Check_T1.IsChecked == true ? layerStep : 0) + (UI_Check_T2.IsChecked == true ? layerStep : 0) + (UI_Check_T3.IsChecked == true ? layerStep : 0) + 4;
                double sBot = marginTB + beamH - cover - 10 - (UI_Check_B1.IsChecked == true ? layerStep : 0) - (UI_Check_B2.IsChecked == true ? layerStep : 0) - (UI_Check_B3.IsChecked == true ? layerStep : 0) - 4;
                
                if (sBot > sTop && sideRows > 0)
                {
                    double sideStep = (sBot - sTop) / (sideRows + 1);
                    for (int r = 1; r <= sideRows; r++)
                    {
                        double y = sTop + sideStep * r;
                        DrawDot(canvas, innerL, y, dotR - 1, Brushes.SlateGray);
                        DrawDot(canvas, innerR, y, dotR - 1, Brushes.SlateGray);
                    }
                    var sLbl = new TextBlock { Text = "Side", FontSize = 12, Foreground = Brushes.SlateGray, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold };
                    Canvas.SetLeft(sLbl, innerR + 15);
                    Canvas.SetTop(sLbl, (sTop + sBot) / 2.0 - 9);
                    canvas.Children.Add(sLbl);
                }
            }
        }

        private void DrawBarRow(Canvas canvas, double left, double right, double y, int count, double r, string label, Brush fill)
        {
            if (count < 1) return;
            double w = right - left;
            if (count == 1)
            {
                DrawDot(canvas, left + w / 2.0, y, r, fill);
            }
            else
            {
                double step = w / (count - 1);
                for (int i = 0; i < count; i++)
                    DrawDot(canvas, left + step * i, y, r, fill);
            }
            // Label to the right
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(41, 98, 180)),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(lbl, right + 15);
            Canvas.SetTop(lbl, y - 9);
            canvas.Children.Add(lbl);
        }

        private void DrawDot(Canvas canvas, double cx, double cy, double r, Brush fill)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(dot, cx - r);
            Canvas.SetTop(dot, cy - r);
            canvas.Children.Add(dot);
        }

        // --- Span Elevation Drawing ---
        private void UpdateSpanElevation()
        {
            if (UI_Canvas_SpanElevation == null) return;
            var canvas = UI_Canvas_SpanElevation;
            canvas.Children.Clear();

            double cW = canvas.Width;   // 200
            double cH = canvas.Height;  // 190
            double marginL = 10;
            double marginR = 60; // Space for staggered labels on right
            double marginTB = 10;

            bool isMultiSpan = (UI_Check_MultiSpan.IsChecked == true);
            int spanCount = isMultiSpan ? 2 : 1;
            double supportW = 10;
            double supportCount = spanCount + 1;
            double totalSupportW = supportCount * supportW;
            double availW = cW - marginL - marginR - totalSupportW;
            double spanW = availW / spanCount;

            double beamTop = marginTB;
            double beamBot = cH - marginTB;
            double beamH = beamBot - beamTop;

            double cover = 12;
            double layerStep = 16;

            var supportBrush = Brushes.LightGray;
            var beamBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
            
            // Harmonious Palette
            var t1Col = Brushes.DarkRed;
            var t2Col = Brushes.OrangeRed;
            var t3Col = Brushes.Gold;
            var b1Col = Brushes.MidnightBlue;
            var b2Col = Brushes.ForestGreen;
            var b3Col = Brushes.DeepSkyBlue;

            // Draw supports and beam spans
            double x = marginL;
            var spanStarts = new List<double>();
            var spanEnds = new List<double>();

            for (int i = 0; i <= spanCount; i++)
            {
                // Support column
                var sup = new System.Windows.Shapes.Rectangle { Width = supportW, Height = beamH + 12, Fill = supportBrush, RadiusX = 1, RadiusY = 1 };
                Canvas.SetLeft(sup, x);
                Canvas.SetTop(sup, beamTop - 6);
                canvas.Children.Add(sup);

                if (i < spanCount)
                {
                    double sStart = x + supportW;
                    double sEnd = sStart + spanW;
                    spanStarts.Add(sStart);
                    spanEnds.Add(sEnd);
                    var beam = new System.Windows.Shapes.Rectangle { Width = spanW, Height = beamH, Fill = beamBrush, Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 190, 190)), StrokeThickness = 1 };
                    Canvas.SetLeft(beam, sStart);
                    Canvas.SetTop(beam, beamTop);
                    canvas.Children.Add(beam);
                }
                x += supportW + (i < spanCount ? spanW : 0);
            }

            double fullLeft = spanStarts[0];
            double fullRight = spanEnds[spanEnds.Count - 1];
            double barThick = 2.5;

            // --- TOP BARS ---
            double topY = marginTB + cover + 10;
            double ty = topY;
            var topBars = new List<(string Lbl, bool Active, bool Cont, Brush Color)>
            {
                ("T1", UI_Check_T1.IsChecked == true, true, t1Col),
                ("T2", UI_Check_T2.IsChecked == true, UI_Check_T2Cont.IsChecked == true, t2Col),
                ("T3", UI_Check_T3.IsChecked == true, UI_Check_T3Cont.IsChecked == true, t3Col),
            };
            int topIdx = 0;
            foreach (var bar in topBars)
            {
                if (!bar.Active) continue;
                if (bar.Cont)
                {
                    // Full-length continuous line
                    DrawHLine(canvas, fullLeft, fullRight, ty, barThick, bar.Color);
                    DrawBarLabel(canvas, fullRight + 14, ty - 8, bar.Lbl, bar.Color);
                }
                else
                {
                    // Additional bar near supports (hogging zones ~L/4 each side)
                    for (int s = 0; s < spanCount; s++)
                    {
                        double zoneLen = (spanEnds[s] - spanStarts[s]) * 0.30;
                        // Left support zone
                        DrawHLine(canvas, spanStarts[s], spanStarts[s] + zoneLen, ty, barThick, bar.Color);
                        // Right support zone
                        DrawHLine(canvas, spanEnds[s] - zoneLen, spanEnds[s], ty, barThick, bar.Color);
                    }
                    DrawBarLabel(canvas, fullRight + 14, ty - 8, bar.Lbl, bar.Color);
                }
                ty += layerStep;
                topIdx++;
            }

            // --- BOTTOM BARS ---
            double botY = marginTB + beamH - cover - 10;
            double by = botY;
            var botBars = new List<(string Lbl, bool Active, bool Cont, Brush Color)>
            {
                ("B1", UI_Check_B1.IsChecked == true, true, b1Col),
                ("B2", UI_Check_B2.IsChecked == true, UI_Check_B2Cont.IsChecked == true, b2Col),
                ("B3", UI_Check_B3.IsChecked == true, UI_Check_B3Cont.IsChecked == true, b3Col),
            };
            int botIdx = 0;
            foreach (var bar in botBars)
            {
                if (!bar.Active) continue;
                if (bar.Cont)
                {
                    DrawHLine(canvas, fullLeft, fullRight, by, barThick, bar.Color);
                    DrawBarLabel(canvas, fullRight + 14, by - 8, bar.Lbl, bar.Color);
                }
                else
                {
                    // Additional bar at midspan (sagging zones ~middle 60%)
                    for (int s = 0; s < spanCount; s++)
                    {
                        double sW = spanEnds[s] - spanStarts[s];
                        double zoneStart = spanStarts[s] + sW * 0.20;
                        double zoneEnd = spanEnds[s] - sW * 0.20;
                        DrawHLine(canvas, zoneStart, zoneEnd, by, barThick, bar.Color);
                    }
                    DrawBarLabel(canvas, fullRight + 14, by - 8, bar.Lbl, bar.Color);
                }
                by -= layerStep;
                botIdx++;
            }

            // --- SIDE BARS ---
            if (UI_Check_SideRebar.IsChecked == true)
            {
                var sideBrush = Brushes.DimGray;
                double midY = (beamTop + beamBot) / 2.0;
                DrawHLine(canvas, fullLeft, fullRight, midY, 1.5, sideBrush);
                DrawBarLabel(canvas, fullRight + 14, midY - 8, "S", sideBrush);
            }
        }

        private void DrawHLine(Canvas canvas, double x1, double x2, double y, double thickness, Brush stroke)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y, X2 = x2, Y2 = y,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(line);
        }

        private void DrawBarLabel(Canvas canvas, double x, double y, string text, Brush color)
        {
            var lbl = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = color,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(lbl, x);
            Canvas.SetTop(lbl, y);
            canvas.Children.Add(lbl);
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
        private static double ParseDouble(string s, double def) => double.TryParse(s, out double d) ? d : def;
        private static int ParseInt(string s, int def) => int.TryParse(s, out int i) ? i : def;
    }
}
