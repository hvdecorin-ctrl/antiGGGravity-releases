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
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<RebarHookType> _hookList;

        public WallRebarPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
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

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

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
                RemoveExisting = UI_Check_RemoveExisting.IsChecked == true,

                // Transverse (Vertical Bars)
                TransverseBarTypeName = (UI_Combo_VertType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = vSpacing,
                TransverseStartOffset = vStart,
                TransverseEndOffset = vEnd,
                TransverseHookStartName = (UI_Combo_VertHookStart.SelectedItem as RebarHookType)?.Name,
                TransverseHookEndName = (UI_Combo_VertHookEnd.SelectedItem as RebarHookType)?.Name,
                TransverseHookStartOut = UI_Check_VertHookStartOut.IsChecked == true,
                TransverseHookEndOut = UI_Check_VertHookEndOut.IsChecked == true,
                VerticalBottomExtension = UI_Check_VertBotExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_VertBotExt.Text, 500)) : 0,
                VerticalTopExtension = UI_Check_VertTopExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_VertTopExt.Text, 500)) : 0,

                // Layers (Horizontal Bars)
                Layers = new List<RebarLayerConfig>(),
                WallLayerConfig = (UI_Combo_LayerConfig.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Centre"
            };

            string hBarType = (UI_Combo_HorizType.SelectedItem as RebarBarType)?.Name;
            string hHookStart = (UI_Combo_HorizHookStart.SelectedItem as RebarHookType)?.Name;
            string hHookEnd = (UI_Combo_HorizHookEnd.SelectedItem as RebarHookType)?.Name;
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

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
