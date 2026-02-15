using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.Model
{
    public partial class RoofFramingView : Window
    {
        private Document _doc;
        private const string VIEW_NAME = "RoofFraming";
        public bool IsConfirmed { get; private set; } = false;

        public FamilySymbol SelectedRafterType => UI_Combo_RafterType.SelectedItem as FamilySymbol;
        public FamilySymbol SelectedPurlinType => UI_Combo_PurlinType.SelectedItem as FamilySymbol;
        public FamilySymbol SelectedEdgeType => UI_Combo_EdgeType.SelectedItem as FamilySymbol;

        public double RafterSpacing => GetDouble(UI_Text_RafterSpacing.Text, 600);
        public double PurlinSpacing => GetDouble(UI_Text_PurlinSpacing.Text, 400);
        public double RafterOffset => GetDouble(UI_Text_RafterOffset.Text, 0);
        public double PurlinOffset => GetDouble(UI_Text_PurlinOffset.Text, 0);
        public double FinishingOffsetValue => GetDouble(UI_Text_FinishingOffset.Text, 50);

        public bool GenRafters => UI_Check_Rafters.IsChecked == true;
        public bool GenPurlins => UI_Check_Purlins.IsChecked == true;
        public bool GenEdgeRafters => UI_Check_EdgeRafters.IsChecked == true;
        public bool RafterUnderPurlin => UI_Check_RafterUnder.IsChecked == true;
        public bool RotateSlope => UI_Check_RotateSlope.IsChecked == true;
        public bool ApplyFinishingOffset => UI_Check_FinishingOffset.IsChecked == true;

        public RoofFramingView(Document doc, Dictionary<string, FamilySymbol> framingTypes)
        {
            InitializeComponent();
            _doc = doc;

            var typesList = framingTypes.Values.OrderBy(x => x.Family.Name).ThenBy(x => x.Name).ToList();
            
            UI_Combo_RafterType.ItemsSource = typesList;
            UI_Combo_RafterType.DisplayMemberPath = "Name"; // Simplified for now, can be improved to "Family : Type"
            
            UI_Combo_PurlinType.ItemsSource = typesList;
            UI_Combo_PurlinType.DisplayMemberPath = "Name";

            UI_Combo_EdgeType.ItemsSource = typesList;
            UI_Combo_EdgeType.DisplayMemberPath = "Name";

            LoadSettings();
        }

        private void LoadSettings()
        {
            UI_Text_RafterSpacing.Text = SettingsManager.Get(VIEW_NAME, "RafterSpacing", "600");
            UI_Text_PurlinSpacing.Text = SettingsManager.Get(VIEW_NAME, "PurlinSpacing", "400");
            UI_Text_RafterOffset.Text = SettingsManager.Get(VIEW_NAME, "RafterOffset", "0");
            UI_Text_PurlinOffset.Text = SettingsManager.Get(VIEW_NAME, "PurlinOffset", "0");
            UI_Text_FinishingOffset.Text = SettingsManager.Get(VIEW_NAME, "FinishingOffset", "50");

            UI_Check_Rafters.IsChecked = SettingsManager.GetBool(VIEW_NAME, "GenRafters", true);
            UI_Check_Purlins.IsChecked = SettingsManager.GetBool(VIEW_NAME, "GenPurlins", true);
            UI_Check_EdgeRafters.IsChecked = SettingsManager.GetBool(VIEW_NAME, "GenEdgeRafters", true);
            UI_Check_RafterUnder.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RafterUnder", true);
            UI_Check_RotateSlope.IsChecked = SettingsManager.GetBool(VIEW_NAME, "RotateSlope", true);
            UI_Check_FinishingOffset.IsChecked = SettingsManager.GetBool(VIEW_NAME, "ApplyFinishingOffset", false);

            SetComboBySettings(UI_Combo_RafterType, "RafterType");
            SetComboBySettings(UI_Combo_PurlinType, "PurlinType");
            SetComboBySettings(UI_Combo_EdgeType, "EdgeType");
        }

        private void SetComboBySettings(System.Windows.Controls.ComboBox combo, string key)
        {
            string savedName = SettingsManager.Get(VIEW_NAME, key, "");
            if (!string.IsNullOrEmpty(savedName))
            {
                foreach (FamilySymbol item in combo.ItemsSource)
                {
                    if (item.Name == savedName)
                    {
                        combo.SelectedItem = item;
                        break;
                    }
                }
            }
            if (combo.SelectedItem == null && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void SaveSettings()
        {
            SettingsManager.Set(VIEW_NAME, "RafterSpacing", UI_Text_RafterSpacing.Text);
            SettingsManager.Set(VIEW_NAME, "PurlinSpacing", UI_Text_PurlinSpacing.Text);
            SettingsManager.Set(VIEW_NAME, "RafterOffset", UI_Text_RafterOffset.Text);
            SettingsManager.Set(VIEW_NAME, "PurlinOffset", UI_Text_PurlinOffset.Text);
            SettingsManager.Set(VIEW_NAME, "FinishingOffset", UI_Text_FinishingOffset.Text);

            SettingsManager.Set(VIEW_NAME, "GenRafters", (UI_Check_Rafters.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "GenPurlins", (UI_Check_Purlins.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "GenEdgeRafters", (UI_Check_EdgeRafters.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "RafterUnder", (UI_Check_RafterUnder.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "RotateSlope", (UI_Check_RotateSlope.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "ApplyFinishingOffset", (UI_Check_FinishingOffset.IsChecked == true).ToString());

            if (SelectedRafterType != null) SettingsManager.Set(VIEW_NAME, "RafterType", SelectedRafterType.Name);
            if (SelectedPurlinType != null) SettingsManager.Set(VIEW_NAME, "PurlinType", SelectedPurlinType.Name);
            if (SelectedEdgeType != null) SettingsManager.Set(VIEW_NAME, "EdgeType", SelectedEdgeType.Name);

            SettingsManager.SaveAll();
        }

        private double GetDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }

        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRafterType == null || SelectedPurlinType == null || SelectedEdgeType == null)
            {
                MessageBox.Show("Please select framing types for all categories.", "Missing Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveSettings();
            IsConfirmed = true;
            Close();
        }
    }
}
