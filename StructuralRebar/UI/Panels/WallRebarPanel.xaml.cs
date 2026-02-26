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
    public partial class WallRebarPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_Wall";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public WallRebarPanel(Document doc)
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

            UI_Combo_VertType.ItemsSource = _rebarTypes;
            UI_Combo_VertType.DisplayMemberPath = "Name";
            UI_Combo_VertType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_HorizType.ItemsSource = _rebarTypes;
            UI_Combo_HorizType.DisplayMemberPath = "Name";
            UI_Combo_HorizType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D12")) ?? _rebarTypes.FirstOrDefault();

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

            UI_Combo_VertHookStart.ItemsSource = _hookList;
            UI_Combo_VertHookStart.DisplayMemberPath = "Name";
            UI_Combo_VertHookStart.SelectedIndex = 0;

            UI_Combo_VertHookEnd.ItemsSource = _hookList;
            UI_Combo_VertHookEnd.DisplayMemberPath = "Name";
            UI_Combo_VertHookEnd.SelectedIndex = 0;

            UI_Combo_HorizHookStart.ItemsSource = _hookList;
            UI_Combo_HorizHookStart.DisplayMemberPath = "Name";
            UI_Combo_HorizHookStart.SelectedIndex = 0;

            UI_Combo_HorizHookEnd.ItemsSource = _hookList;
            UI_Combo_HorizHookEnd.DisplayMemberPath = "Name";
            UI_Combo_HorizHookEnd.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_VertSpacing.Text = SettingsManager.Get(VIEW_NAME, "VertSpacing", "200");
                UI_Text_VertStartOffset.Text = SettingsManager.Get(VIEW_NAME, "VertStartOffset", "50");
                UI_Text_VertEndOffset.Text = SettingsManager.Get(VIEW_NAME, "VertEndOffset", "50");
                UI_Text_VertTopExt.Text = SettingsManager.Get(VIEW_NAME, "VertTopExt", "500");
                UI_Text_VertBotExt.Text = SettingsManager.Get(VIEW_NAME, "VertBotExt", "500");

                UI_Text_HorizSpacing.Text = SettingsManager.Get(VIEW_NAME, "HorizSpacing", "200");
                UI_Text_HorizTopOffset.Text = SettingsManager.Get(VIEW_NAME, "HorizTopOffset", "50");
                UI_Text_HorizBottomOffset.Text = SettingsManager.Get(VIEW_NAME, "HorizBottomOffset", "50");

                UI_Check_VertTopExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VertTopExtEnabled", false);
                UI_Check_VertBotExt.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VertBotExtEnabled", false);
                UI_Check_VertHookStartOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VertHookStartOut", false);
                UI_Check_VertHookEndOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "VertHookEndOut", false);
                UI_Check_HorizHookStartOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "HorizHookStartOut", false);
                UI_Check_HorizHookEndOut.IsChecked = SettingsManager.GetBool(VIEW_NAME, "HorizHookEndOut", false);


                SelectByName(UI_Combo_VertType, SettingsManager.Get(VIEW_NAME, "VertType"));
                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"));

                SelectHookByName(UI_Combo_VertHookStart, SettingsManager.Get(VIEW_NAME, "VertHookStart"));
                SelectHookByName(UI_Combo_VertHookEnd, SettingsManager.Get(VIEW_NAME, "VertHookEnd"));
                SelectHookByName(UI_Combo_HorizHookStart, SettingsManager.Get(VIEW_NAME, "HorizHookStart"));
                SelectHookByName(UI_Combo_HorizHookEnd, SettingsManager.Get(VIEW_NAME, "HorizHookEnd"));

                string config = SettingsManager.Get(VIEW_NAME, "LayerConfig", "Centre");
                foreach (ComboBoxItem item in UI_Combo_LayerConfig.Items)
                {
                    if (item.Content.ToString() == config)
                    {
                        UI_Combo_LayerConfig.SelectedItem = item;
                        break;
                    }
                }

                UI_Check_VertBotExt_Click(null, null);
                UI_Check_VertTopExt_Click(null, null);
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "VertSpacing", UI_Text_VertSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "VertStartOffset", UI_Text_VertStartOffset.Text);
                SettingsManager.Set(VIEW_NAME, "VertEndOffset", UI_Text_VertEndOffset.Text);
                SettingsManager.Set(VIEW_NAME, "VertTopExt", UI_Text_VertTopExt.Text);
                SettingsManager.Set(VIEW_NAME, "VertBotExt", UI_Text_VertBotExt.Text);

                SettingsManager.Set(VIEW_NAME, "HorizSpacing", UI_Text_HorizSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "HorizTopOffset", UI_Text_HorizTopOffset.Text);
                SettingsManager.Set(VIEW_NAME, "HorizBottomOffset", UI_Text_HorizBottomOffset.Text);

                SettingsManager.Set(VIEW_NAME, "VertTopExtEnabled", (UI_Check_VertTopExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "VertBotExtEnabled", (UI_Check_VertBotExt.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "VertHookStartOut", (UI_Check_VertHookStartOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "VertHookEndOut", (UI_Check_VertHookEndOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "HorizHookStartOut", (UI_Check_HorizHookStartOut.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "HorizHookEndOut", (UI_Check_HorizHookEndOut.IsChecked == true).ToString());


                SettingsManager.Set(VIEW_NAME, "VertType", TransTypeName(UI_Combo_VertType));
                SettingsManager.Set(VIEW_NAME, "HorizType", TransTypeName(UI_Combo_HorizType));

                SettingsManager.Set(VIEW_NAME, "VertHookStart", HookName(UI_Combo_VertHookStart));
                SettingsManager.Set(VIEW_NAME, "VertHookEnd", HookName(UI_Combo_VertHookEnd));
                SettingsManager.Set(VIEW_NAME, "HorizHookStart", HookName(UI_Combo_HorizHookStart));
                SettingsManager.Set(VIEW_NAME, "HorizHookEnd", HookName(UI_Combo_HorizHookEnd));

                SettingsManager.Set(VIEW_NAME, "LayerConfig", (UI_Combo_LayerConfig.SelectedItem as ComboBoxItem)?.Content.ToString());

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            // Convert MM to feet
            double vSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_VertSpacing.Text, 200));
            double vStart = UnitConversion.MmToFeet(ParseDouble(UI_Text_VertStartOffset.Text, 50));
            double vEnd = UnitConversion.MmToFeet(ParseDouble(UI_Text_VertEndOffset.Text, 50));
            
            double hSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_HorizSpacing.Text, 200));
            double hTop = UnitConversion.MmToFeet(ParseDouble(UI_Text_HorizTopOffset.Text, 50));
            double hBot = UnitConversion.MmToFeet(ParseDouble(UI_Text_HorizBottomOffset.Text, 50));

            var request = new RebarRequest
            {
                HostType = ElementHostType.Wall,
                RemoveExisting = false, // Handled by Window level now
                EnableLapSplice = false, // Set by window/handler now

                // Transverse (Vertical Bars)
                TransverseBarTypeName = (UI_Combo_VertType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = vSpacing,
                TransverseStartOffset = vStart,
                TransverseEndOffset = vEnd,
                TransverseHookStartName = HookName(UI_Combo_VertHookStart),
                TransverseHookEndName = HookName(UI_Combo_VertHookEnd),
                TransverseHookStartOut = UI_Check_VertHookStartOut.IsChecked == true,
                TransverseHookEndOut = UI_Check_VertHookEndOut.IsChecked == true,
                VerticalBottomExtension = UI_Check_VertBotExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_VertBotExt.Text, 500)) : 0,
                VerticalTopExtension = UI_Check_VertTopExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_VertTopExt.Text, 500)) : 0,

                // Layers (Horizontal Bars)
                Layers = new List<RebarLayerConfig>(),
                WallLayerConfig = (UI_Combo_LayerConfig.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Centre"
            };

            string hBarType = (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name;
            string hHookStart = HookName(UI_Combo_HorizHookStart);
            string hHookEnd = HookName(UI_Combo_HorizHookEnd);
            bool hHookStartOut = UI_Check_HorizHookStartOut.IsChecked == true;
            bool hHookEndOut = UI_Check_HorizHookEndOut.IsChecked == true;

            // Add layer template for horizontal bars
            var hLayer = new RebarLayerConfig
            {
                HorizontalBarTypeName = hBarType,
                HorizontalSpacing = hSpacing,
                TopOffset = hTop,
                BottomOffset = hBot,
                HookStartName = hHookStart,
                HookEndName = hHookEnd,
                HookStartOutward = hHookStartOut,
                HookEndOutward = hHookEndOut
            };

            if (request.WallLayerConfig == "Centre")
            {
                hLayer.Face = RebarLayerFace.Interior;
                hLayer.HorizontalOffset = 0;
                request.Layers.Add(hLayer);
            }
            else if (request.WallLayerConfig == "Both faces")
            {
                var extLayer = Clone(hLayer);
                extLayer.Face = RebarLayerFace.Exterior;
                extLayer.HorizontalOffset = 1; // Flag for Engine: Exterior
                request.Layers.Add(extLayer);

                var intLayer = Clone(hLayer);
                intLayer.Face = RebarLayerFace.Interior;
                intLayer.HorizontalOffset = -1; // Flag for Engine: Interior
                request.Layers.Add(intLayer);
            }
            else if (request.WallLayerConfig == "External face")
            {
                hLayer.Face = RebarLayerFace.Exterior;
                hLayer.HorizontalOffset = 1;
                request.Layers.Add(hLayer);
            }
            else if (request.WallLayerConfig == "Internal face")
            {
                hLayer.Face = RebarLayerFace.Interior;
                hLayer.HorizontalOffset = -1;
                request.Layers.Add(hLayer);
            }
            
            return request;
        }

        private RebarLayerConfig Clone(RebarLayerConfig source)
        {
            return new RebarLayerConfig
            {
                HorizontalBarTypeName = source.HorizontalBarTypeName,
                HorizontalSpacing = source.HorizontalSpacing,
                TopOffset = source.TopOffset,
                BottomOffset = source.BottomOffset,
                HookStartName = source.HookStartName,
                HookEndName = source.HookEndName,
                HookStartOutward = source.HookStartOutward,
                HookEndOutward = source.HookEndOutward
            };
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
        private void UI_Check_VertBotExt_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Text_VertBotExt != null)
                UI_Text_VertBotExt.Visibility = UI_Check_VertBotExt.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void UI_Check_VertTopExt_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Text_VertTopExt != null)
                UI_Text_VertTopExt.Visibility = UI_Check_VertTopExt.IsChecked == true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
