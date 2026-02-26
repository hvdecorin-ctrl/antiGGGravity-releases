using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

            foreach (var combo in new[] { UI_Combo_T1Type, UI_Combo_T2Type, UI_Combo_B1Type, UI_Combo_B2Type, UI_Combo_TransType })
            {
                combo.ItemsSource = _rebarTypes;
                combo.DisplayMemberPath = "Name";
            }

            var d16 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();
            UI_Combo_T1Type.SelectedItem = d16;
            UI_Combo_T2Type.SelectedItem = d16;
            UI_Combo_B1Type.SelectedItem = d16;
            UI_Combo_B2Type.SelectedItem = d16;

            var r10 = _rebarTypes.FirstOrDefault(x => x.Name.Contains("R10"))
                   ?? _rebarTypes.FirstOrDefault(x => x.Name.Contains("R6"))
                   ?? _rebarTypes.FirstOrDefault();
            UI_Combo_TransType.SelectedItem = r10;

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
                UI_Check_B1.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B1Enabled", true);
                UI_Check_B2.IsChecked = SettingsManager.GetBool(VIEW_NAME, "B2Enabled", false);

                UI_Text_T1Count.Text = SettingsManager.Get(VIEW_NAME, "T1Count", "2");
                UI_Text_T2Count.Text = SettingsManager.Get(VIEW_NAME, "T2Count", "3");
                UI_Text_B1Count.Text = SettingsManager.Get(VIEW_NAME, "B1Count", "2");
                UI_Text_B2Count.Text = SettingsManager.Get(VIEW_NAME, "B2Count", "3");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Text_TransStartOffset.Text = SettingsManager.Get(VIEW_NAME, "TransStartOffset", "50");

                SelectByName(UI_Combo_T1Type, SettingsManager.Get(VIEW_NAME, "T1Type"));
                SelectByName(UI_Combo_T2Type, SettingsManager.Get(VIEW_NAME, "T2Type"));
                SelectByName(UI_Combo_B1Type, SettingsManager.Get(VIEW_NAME, "B1Type"));
                SelectByName(UI_Combo_B2Type, SettingsManager.Get(VIEW_NAME, "B2Type"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));

                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));
                SelectHookByName(UI_Combo_TopHookStart, SettingsManager.Get(VIEW_NAME, "TopHookStart"));
                SelectHookByName(UI_Combo_TopHookEnd, SettingsManager.Get(VIEW_NAME, "TopHookEnd"));
                SelectHookByName(UI_Combo_BotHookStart, SettingsManager.Get(VIEW_NAME, "BotHookStart"));
                SelectHookByName(UI_Combo_BotHookEnd, SettingsManager.Get(VIEW_NAME, "BotHookEnd"));

                UI_Radio_StirrupUnEQ.IsChecked = SettingsManager.GetBool(VIEW_NAME, "StirrupDistUnEQ", false);
                UI_Radio_StirrupEQ.IsChecked = !(UI_Radio_StirrupUnEQ.IsChecked == true);



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
                SettingsManager.Set(VIEW_NAME, "B1Enabled", (UI_Check_B1.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "B2Enabled", (UI_Check_B2.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "T1Count", UI_Text_T1Count.Text);
                SettingsManager.Set(VIEW_NAME, "T2Count", UI_Text_T2Count.Text);
                SettingsManager.Set(VIEW_NAME, "B1Count", UI_Text_B1Count.Text);
                SettingsManager.Set(VIEW_NAME, "B2Count", UI_Text_B2Count.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransStartOffset", UI_Text_TransStartOffset.Text);

                SettingsManager.Set(VIEW_NAME, "T1Type", TransTypeName(UI_Combo_T1Type));
                SettingsManager.Set(VIEW_NAME, "T2Type", TransTypeName(UI_Combo_T2Type));
                SettingsManager.Set(VIEW_NAME, "B1Type", TransTypeName(UI_Combo_B1Type));
                SettingsManager.Set(VIEW_NAME, "B2Type", TransTypeName(UI_Combo_B2Type));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));

                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));
                SettingsManager.Set(VIEW_NAME, "TopHookStart", HookName(UI_Combo_TopHookStart));
                SettingsManager.Set(VIEW_NAME, "TopHookEnd", HookName(UI_Combo_TopHookEnd));
                SettingsManager.Set(VIEW_NAME, "BotHookStart", HookName(UI_Combo_BotHookStart));
                SettingsManager.Set(VIEW_NAME, "BotHookEnd", HookName(UI_Combo_BotHookEnd));

                SettingsManager.Set(VIEW_NAME, "StirrupDistUnEQ", (UI_Radio_StirrupUnEQ.IsChecked == true).ToString());


                SettingsManager.SaveAll();
            }
            catch { }
        }

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            if (UI_Group_T1 == null) return;
            UI_Group_T1.Visibility = UI_Check_T1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_T2.Visibility = UI_Check_T2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B1.Visibility = UI_Check_B1.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            UI_Group_B2.Visibility = UI_Check_B2.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
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
                EnableLapSplice = false, // Set by window/handler now
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
                    HookStartName = HookName(UI_Combo_TopHookStart),
                    HookEndName = HookName(UI_Combo_TopHookEnd),
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
                    HookStartName = HookName(UI_Combo_TopHookStart),
                    HookEndName = HookName(UI_Combo_TopHookEnd),
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
                    HookStartName = HookName(UI_Combo_BotHookStart),
                    HookEndName = HookName(UI_Combo_BotHookEnd),
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
                    HookStartName = HookName(UI_Combo_BotHookStart),
                    HookEndName = HookName(UI_Combo_BotHookEnd),
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
        private static double ParseDouble(string s, double def) => double.TryParse(s, out double d) ? d : def;
        private static int ParseInt(string s, int def) => int.TryParse(s, out int i) ? i : def;
    }
}
