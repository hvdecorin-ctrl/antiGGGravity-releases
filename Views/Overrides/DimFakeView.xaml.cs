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

namespace antiGGGravity.Views.Overrides
{
    public partial class DimFakeView : Window
    {
        private ExternalCommandData _commandData;
        private UIDocument _uidoc;
        private Document _doc;
        
        public ObservableCollection<PresetItem> Presets { get; set; }

        public DimFakeView(ExternalCommandData commandData)
        {
            InitializeComponent();
            _commandData = commandData;
            _uidoc = commandData.Application.ActiveUIDocument;
            _doc = _uidoc.Document;

            LoadPresets();
            UI_Presets_List.ItemsSource = Presets;
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

            string valOverride = UI_Value.Text;
            string belowOverride = UI_Below.Text;

            try
            {
                // Check current selection
                var selectedIds = _uidoc.Selection.GetElementIds();
                List<Dimension> dims = new List<Dimension>();

                if (selectedIds.Count > 0)
                {
                    foreach (ElementId id in selectedIds)
                    {
                        if (_doc.GetElement(id) is Dimension d) dims.Add(d);
                    }
                }
                else
                {
                    // Pick Mode
                    this.Hide(); // Hide window to pick
                    try
                    {
                        var refs = _uidoc.Selection.PickObjects(ObjectType.Element, new DimensionSelectionFilter(), "Select Dimensions");
                        foreach (var r in refs)
                        {
                            if (_doc.GetElement(r) is Dimension d) dims.Add(d);
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        this.Show();
                        return; // User cancelled pick
                    }
                    this.Show(); // Restore window
                }

                if (dims.Count > 0)
                {
                    using (Transaction t = new Transaction(_doc, "Override Dimensions"))
                    {
                        t.Start();
                        foreach (Dimension d in dims)
                        {
                            // Segments support not implemented in API directly easily for Values sometimes?
                            // Actually Dimension.ValueOverride works on the dimension.
                            // If multi-segment, we might need to iterate segments if exposed?
                            // Python used `if el.Segments.Size > 0`.
                            // In C#, Dimension class has `ValueOverride`. Does it apply to all?
                            // Revit API: Dimension.ValueOverride applies to the dimension.
                            // But for multi-segment chain, can we override individually? 
                            // `d.Segments` exists (DimensionSegment). They have `ValueOverride`.
                            
                            if (d.NumberOfSegments > 0)
                            {
                                foreach (DimensionSegment seg in d.Segments)
                                {
                                    if (!string.IsNullOrEmpty(valOverride)) seg.ValueOverride = valOverride;
                                    if (!string.IsNullOrEmpty(belowOverride)) seg.Below = belowOverride;
                                }
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(valOverride)) d.ValueOverride = valOverride;
                                if (!string.IsNullOrEmpty(belowOverride)) d.Below = belowOverride;
                            }
                        }
                        t.Commit();
                    }
                    UI_Status.Text = "Applied to " + dims.Count + " dimensions.";
                }
                else
                {
                    UI_Status.Text = "No dimensions selected.";
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            SavePresets();
            Close();
        }
    }

    public class PresetItem
    {
        public string Value { get; set; }
        public string Below { get; set; }
    }

    public class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Dimension;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
