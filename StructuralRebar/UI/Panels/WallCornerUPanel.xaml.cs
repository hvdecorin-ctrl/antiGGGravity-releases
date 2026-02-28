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
    public partial class WallCornerUPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_WallCornerU";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;

        public WallCornerUPanel(Document doc)
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

            SetupCombo(UI_Combo_HorizType, "D12");
            SetupCombo(UI_Combo_WallEndType, "D12");
            SetupCombo(UI_Combo_TopEndType, "D12");
            SetupCombo(UI_Combo_BotEndType, "D12");
            SetupCombo(UI_Combo_TrimmerType, "D16");
        }

        private void SetupCombo(ComboBox combo, string defaultFilter)
        {
            combo.ItemsSource = _rebarTypes;
            combo.DisplayMemberPath = "Name";
            combo.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains(defaultFilter)) ?? _rebarTypes.FirstOrDefault();
        }

        private void LoadSettings()
        {
            try
            {
                // Intersect
                UI_Check_Intersect.IsChecked = SettingsManager.GetBool(VIEW_NAME, "IntersectEnabled", false);
                UI_Text_HorizSpacing.Text = SettingsManager.Get(VIEW_NAME, "HorizSpacing", "200");
                UI_Text_Leg1.Text = SettingsManager.Get(VIEW_NAME, "Leg1", "600");
                UI_Text_Leg2.Text = SettingsManager.Get(VIEW_NAME, "Leg2", "600");
                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"));

                // Wall End
                UI_Check_WallEnd.IsChecked = SettingsManager.GetBool(VIEW_NAME, "WallEndEnabled", false);
                UI_Text_WallEndSpacing.Text = SettingsManager.Get(VIEW_NAME, "WallEndSpacing", "200");
                UI_Text_WallEndLeg1.Text = SettingsManager.Get(VIEW_NAME, "WallEndLeg1", "600");
                UI_Text_WallEndLeg2.Text = SettingsManager.Get(VIEW_NAME, "WallEndLeg2", "600");
                SelectByName(UI_Combo_WallEndType, SettingsManager.Get(VIEW_NAME, "WallEndType"));

                // Distribution Offset for Intersect/End
                UI_Text_IntEndTopOffset.Text = SettingsManager.Get(VIEW_NAME, "IntEndTopOffset", "50");
                UI_Text_IntEndBotOffset.Text = SettingsManager.Get(VIEW_NAME, "IntEndBotOffset", "50");

                // Top End
                UI_Check_TopEnd.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopEndEnabled", false);
                UI_Text_TopEndSpacing.Text = SettingsManager.Get(VIEW_NAME, "TopEndSpacing", "200");
                UI_Text_TopEndLeg1.Text = SettingsManager.Get(VIEW_NAME, "TopEndLeg1", "600");
                UI_Text_TopEndLeg2.Text = SettingsManager.Get(VIEW_NAME, "TopEndLeg2", "600");
                SelectByName(UI_Combo_TopEndType, SettingsManager.Get(VIEW_NAME, "TopEndType"));
                SelectLayerCombo(UI_Combo_TopEndLayer, SettingsManager.Get(VIEW_NAME, "TopEndLayer", "Vert External"));

                // Bottom End
                UI_Check_BotEnd.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotEndEnabled", false);
                UI_Text_BotEndSpacing.Text = SettingsManager.Get(VIEW_NAME, "BotEndSpacing", "200");
                UI_Text_BotEndLeg1.Text = SettingsManager.Get(VIEW_NAME, "BotEndLeg1", "600");
                UI_Text_BotEndLeg2.Text = SettingsManager.Get(VIEW_NAME, "BotEndLeg2", "600");
                SelectByName(UI_Combo_BotEndType, SettingsManager.Get(VIEW_NAME, "BotEndType"));
                SelectLayerCombo(UI_Combo_BotEndLayer, SettingsManager.Get(VIEW_NAME, "BotEndLayer", "Vert External"));

                // Distribution Offset for Top/Bottom
                UI_Text_TopOffset.Text = SettingsManager.Get(VIEW_NAME, "TopOffset", "50");
                UI_Text_BotOffset.Text = SettingsManager.Get(VIEW_NAME, "BotOffset", "50");

                // Trimmers
                UI_Check_Trimmers.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TrimmersEnabled", true);
                SelectByName(UI_Combo_TrimmerType, SettingsManager.Get(VIEW_NAME, "TrimmerType"));

                toggle_visibility(null, null);
            }
            catch { }
        }

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            SetGroupVisibility(UI_Group_Intersect, UI_Check_Intersect);
            SetGroupVisibility(UI_Group_WallEnd, UI_Check_WallEnd);
            SetGroupVisibility(UI_Group_TopEnd, UI_Check_TopEnd);
            SetGroupVisibility(UI_Group_BotEnd, UI_Check_BotEnd);
            SetGroupVisibility(UI_Group_Trimmers, UI_Check_Trimmers);
        }

        private void SetGroupVisibility(StackPanel group, CheckBox check)
        {
            if (group == null || check == null) return;
            group.Visibility = check.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public void SaveSettings()
        {
            try
            {
                // Intersect
                SettingsManager.Set(VIEW_NAME, "IntersectEnabled", (UI_Check_Intersect.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "HorizSpacing", UI_Text_HorizSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "Leg1", UI_Text_Leg1.Text);
                SettingsManager.Set(VIEW_NAME, "Leg2", UI_Text_Leg2.Text);
                SettingsManager.Set(VIEW_NAME, "HorizType", TransTypeName(UI_Combo_HorizType));

                // Wall End
                SettingsManager.Set(VIEW_NAME, "WallEndEnabled", (UI_Check_WallEnd.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "WallEndSpacing", UI_Text_WallEndSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "WallEndLeg1", UI_Text_WallEndLeg1.Text);
                SettingsManager.Set(VIEW_NAME, "WallEndLeg2", UI_Text_WallEndLeg2.Text);
                SettingsManager.Set(VIEW_NAME, "WallEndType", TransTypeName(UI_Combo_WallEndType));

                // Distribution Offset for Intersect/End
                SettingsManager.Set(VIEW_NAME, "IntEndTopOffset", UI_Text_IntEndTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "IntEndBotOffset", UI_Text_IntEndBotOffset.Text);

                // Top End
                SettingsManager.Set(VIEW_NAME, "TopEndEnabled", (UI_Check_TopEnd.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopEndSpacing", UI_Text_TopEndSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TopEndLeg1", UI_Text_TopEndLeg1.Text);
                SettingsManager.Set(VIEW_NAME, "TopEndLeg2", UI_Text_TopEndLeg2.Text);
                SettingsManager.Set(VIEW_NAME, "TopEndType", TransTypeName(UI_Combo_TopEndType));
                SettingsManager.Set(VIEW_NAME, "TopEndLayer", GetLayerComboValue(UI_Combo_TopEndLayer));

                // Bottom End
                SettingsManager.Set(VIEW_NAME, "BotEndEnabled", (UI_Check_BotEnd.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotEndSpacing", UI_Text_BotEndSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "BotEndLeg1", UI_Text_BotEndLeg1.Text);
                SettingsManager.Set(VIEW_NAME, "BotEndLeg2", UI_Text_BotEndLeg2.Text);
                SettingsManager.Set(VIEW_NAME, "BotEndType", TransTypeName(UI_Combo_BotEndType));
                SettingsManager.Set(VIEW_NAME, "BotEndLayer", GetLayerComboValue(UI_Combo_BotEndLayer));

                // Distribution Offset for Top/Bottom
                SettingsManager.Set(VIEW_NAME, "TopOffset", UI_Text_TopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "BotOffset", UI_Text_BotOffset.Text);

                // Trimmers
                SettingsManager.Set(VIEW_NAME, "TrimmersEnabled", (UI_Check_Trimmers.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TrimmerType", TransTypeName(UI_Combo_TrimmerType));

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.WallCornerU,
                RemoveExisting = false,

                // Intersect settings (primary)
                AddIntersectUBars = UI_Check_Intersect.IsChecked == true,
                VerticalBarTypeName = (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name,
                VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_HorizSpacing.Text, 200)),
                LegLength1 = UnitConversion.MmToFeet(ParseDouble(UI_Text_Leg1.Text, 600)),
                LegLength2 = UnitConversion.MmToFeet(ParseDouble(UI_Text_Leg2.Text, 600)),

                // Wall End
                AddWallEndUBars = UI_Check_WallEnd.IsChecked == true,
                WallEndBarTypeName = (UI_Combo_WallEndType.SelectedItem as RebarBarType)?.Name,
                WallEndSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_WallEndSpacing.Text, 200)),
                WallEndLeg1 = UnitConversion.MmToFeet(ParseDouble(UI_Text_WallEndLeg1.Text, 600)),
                WallEndLeg2 = UnitConversion.MmToFeet(ParseDouble(UI_Text_WallEndLeg2.Text, 600)),

                // Distribution Offset for Intersect/End (vertical array)
                TransverseStartOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_IntEndBotOffset.Text, 50)),
                TransverseEndOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_IntEndTopOffset.Text, 50)),

                // Top End
                AddTopEndUBars = UI_Check_TopEnd.IsChecked == true,
                TopEndBarTypeName = (UI_Combo_TopEndType.SelectedItem as RebarBarType)?.Name,
                TopEndSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopEndSpacing.Text, 200)),
                TopEndLeg1 = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopEndLeg1.Text, 600)),
                TopEndLeg2 = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopEndLeg2.Text, 600)),
                TopEndLayer = GetLayerComboValue(UI_Combo_TopEndLayer),

                // Bottom End
                AddBotEndUBars = UI_Check_BotEnd.IsChecked == true,
                BotEndBarTypeName = (UI_Combo_BotEndType.SelectedItem as RebarBarType)?.Name,
                BotEndSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotEndSpacing.Text, 200)),
                BotEndLeg1 = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotEndLeg1.Text, 600)),
                BotEndLeg2 = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotEndLeg2.Text, 600)),
                BotEndLayer = GetLayerComboValue(UI_Combo_BotEndLayer),

                // Distribution Offset for Top/Bottom (position from wall edge)
                TopBotTopOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopOffset.Text, 50)),
                TopBotBotOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotOffset.Text, 50)),

                // Trimmers
                AddTrimmers = UI_Check_Trimmers.IsChecked == true,
                TrimmerBarTypeName = (UI_Combo_TrimmerType.SelectedItem as RebarBarType)?.Name
            };

            return request;
        }

        // --- Helpers ---
        private void SelectByName(ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var match = _rebarTypes.FirstOrDefault(x => x.Name == name);
            if (match != null) combo.SelectedItem = match;
        }

        private static string TransTypeName(ComboBox combo) => (combo.SelectedItem as RebarBarType)?.Name ?? "";

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }

        private void SelectLayerCombo(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content.ToString() == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private static string GetLayerComboValue(ComboBox combo)
        {
            return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Vert External";
        }
    }
}
