using System;
using System.Windows.Controls;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class CustomDesignPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_CustomDesign";

        public CustomDesignPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                UI_Text_CustomLapTension.Text = SettingsManager.Get(VIEW_NAME, "LapTension", "50");
                UI_Text_CustomLapCompression.Text = SettingsManager.Get(VIEW_NAME, "LapCompression", "30");
                UI_Text_CustomZoneLenFactor.Text = SettingsManager.Get(VIEW_NAME, "ZoneLenFactor", "1.0");
                UI_Text_CustomZoneSpacing.Text = SettingsManager.Get(VIEW_NAME, "ZoneSpacing", "6.0");
                UI_Text_CustomStarterDev.Text = SettingsManager.Get(VIEW_NAME, "StarterDev", "40");
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "LapTension", UI_Text_CustomLapTension.Text);
                SettingsManager.Set(VIEW_NAME, "LapCompression", UI_Text_CustomLapCompression.Text);
                SettingsManager.Set(VIEW_NAME, "ZoneLenFactor", UI_Text_CustomZoneLenFactor.Text);
                SettingsManager.Set(VIEW_NAME, "ZoneSpacing", UI_Text_CustomZoneSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "StarterDev", UI_Text_CustomStarterDev.Text);
                
                SettingsManager.SaveAll();
            }
            catch { }
        }

        private double ParseDouble(string val, double fallback)
        {
            return double.TryParse(val, out double result) ? result : fallback;
        }
    }
}
