using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Utilities;
using antiGGGravity.Commands.Overrides;

namespace antiGGGravity.Views.Overrides
{
    public partial class DimFakeView : Window
    {
        private readonly Document _doc;
        private readonly List<Dimension> _targets;
        
        public ObservableCollection<PresetItem> Presets { get; set; }
 
        public DimFakeView(Document doc, List<Dimension> targets)
        {
            InitializeComponent();
            _doc = doc;
            _targets = targets;
 
            LoadPresets();
            UI_Presets_List.ItemsSource = Presets;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UI_Value.Focus();
        }
 
        private void LoadPresets()
        {
            Presets = new ObservableCollection<PresetItem>();
            
            // Try to load from settings, or defaults
            for (int i = 0; i < 5; i++)
            {
                string storedVal = SettingsManager.Get("DimFake", $"Preset{i}_Val", GetDefaultVal(i));
                string storedBel = SettingsManager.Get("DimFake", $"Preset{i}_Bel", GetDefaultBel(i));
                Presets.Add(new PresetItem { Value = storedVal, Below = storedBel });
            }
        }
 
        private string GetDefaultVal(int i)
        {
            if (i == 0) return "600 MIN LAP";
            return "";
        }
        private string GetDefaultBel(int i)
        {
            if (i == 1) return "TYP";
            if (i == 2) return "N.T.S";
            if (i == 3) return "MIN";
            if (i == 4) return "MAX";
            return "";
        }
 
        private void SavePresets()
        {
            for (int i = 0; i < Presets.Count; i++)
            {
                SettingsManager.Set("DimFake", $"Preset{i}_Val", Presets[i].Value);
                SettingsManager.Set("DimFake", $"Preset{i}_Bel", Presets[i].Below);
            }
            SettingsManager.SaveAll();
        }
 
        private void Preset_Apply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PresetItem item)
            {
                UI_Value.Text = item.Value;
                UI_Below.Text = item.Below;
            }
        }
 
        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            SavePresets();
 
            try
            {
                using (Transaction t = new Transaction(_doc, "Override Dimensions"))
                {
                    t.Start();
                    foreach (Dimension d in _targets)
                    {
                        // Reset if blank, else apply text
                        string val = UI_Value.Text;
                        string bel = UI_Below.Text;

                        if (d.NumberOfSegments > 0)
                        {
                            foreach (DimensionSegment seg in d.Segments)
                            {
                                seg.ValueOverride = val; // Works for reset if val is empty
                                seg.Below = bel;
                            }
                        }
                        else
                        {
                            d.ValueOverride = val;
                            d.Below = bel;
                        }
                    }
                    t.Commit();
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error applying overrides: " + ex.Message);
            }
        }
 
        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            SavePresets();
            this.DialogResult = false;
            Close();
        }
    }
 
    public class PresetItem
    {
        public string Value { get; set; }
        public string Below { get; set; }
    }
}
