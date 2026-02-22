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

            UI_Combo_HorizType.ItemsSource = _rebarTypes;
            UI_Combo_HorizType.DisplayMemberPath = "Name";
            UI_Combo_HorizType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TrimmerType.ItemsSource = _rebarTypes;
            UI_Combo_TrimmerType.DisplayMemberPath = "Name";
            UI_Combo_TrimmerType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_HorizSpacing.Text = SettingsManager.Get(VIEW_NAME, "HorizSpacing", "200");
                UI_Text_Leg1.Text = SettingsManager.Get(VIEW_NAME, "Leg1", "600");
                UI_Text_Leg2.Text = SettingsManager.Get(VIEW_NAME, "Leg2", "600");
                UI_Text_BotOffset.Text = SettingsManager.Get(VIEW_NAME, "BotOffset", "50");
                UI_Text_TopOffset.Text = SettingsManager.Get(VIEW_NAME, "TopOffset", "50");

                UI_Check_Trimmers.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TrimmersEnabled", true);

                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"));
                SelectByName(UI_Combo_TrimmerType, SettingsManager.Get(VIEW_NAME, "TrimmerType"));
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "HorizSpacing", UI_Text_HorizSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "Leg1", UI_Text_Leg1.Text);
                SettingsManager.Set(VIEW_NAME, "Leg2", UI_Text_Leg2.Text);
                SettingsManager.Set(VIEW_NAME, "BotOffset", UI_Text_BotOffset.Text);
                SettingsManager.Set(VIEW_NAME, "TopOffset", UI_Text_TopOffset.Text);

                SettingsManager.Set(VIEW_NAME, "TrimmersEnabled", (UI_Check_Trimmers.IsChecked == true).ToString());

                SettingsManager.Set(VIEW_NAME, "HorizType", TransTypeName(UI_Combo_HorizType));
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
                RemoveExisting = false, // Handled by Window level

                // Primary Horizontal Settings
                VerticalBarTypeName = (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name,
                VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_HorizSpacing.Text, 200)),
                
                // Leg Lengths
                LegLength1 = UnitConversion.MmToFeet(ParseDouble(UI_Text_Leg1.Text, 600)),
                LegLength2 = UnitConversion.MmToFeet(ParseDouble(UI_Text_Leg2.Text, 600)),

                // Vertical Offsets
                TransverseStartOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotOffset.Text, 50)),
                TransverseEndOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopOffset.Text, 50)),

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
    }
}
