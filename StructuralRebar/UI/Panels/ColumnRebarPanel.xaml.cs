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
    public partial class ColumnRebarPanel : UserControl
    {
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<RebarHookType> _hookList;

        public ColumnRebarPanel(Document doc)
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

            UI_Combo_VerticalType.ItemsSource = _rebarTypes;
            UI_Combo_VerticalType.DisplayMemberPath = "Name";
            UI_Combo_VerticalType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D25")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TieType.ItemsSource = _rebarTypes;
            UI_Combo_TieType.DisplayMemberPath = "Name";
            UI_Combo_TieType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            UI_Combo_VHookBot.ItemsSource = _hookList;
            UI_Combo_VHookBot.DisplayMemberPath = "Name";
            UI_Combo_VHookBot.SelectedIndex = 0;

            UI_Combo_VHookTop.ItemsSource = _hookList;
            UI_Combo_VHookTop.DisplayMemberPath = "Name";
            UI_Combo_VHookTop.SelectedIndex = 0;

            UI_Combo_HookStart.ItemsSource = _hookList;
            UI_Combo_HookStart.DisplayMemberPath = "Name";
            UI_Combo_HookStart.SelectedIndex = 0;

            UI_Combo_HookEnd.ItemsSource = _hookList;
            UI_Combo_HookEnd.DisplayMemberPath = "Name";
            UI_Combo_HookEnd.SelectedIndex = 0;
        }

        public RebarRequest GetRequest()
        {
            double tieSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieSpacing.Text, 200));
            double tieBotOff = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieBotOffset.Text, 100));
            double tieTopOff = UnitConversion.MmToFeet(ParseDouble(UI_Text_TieTopOffset.Text, 100));

            var request = new RebarRequest
            {
                HostType = ElementHostType.Column,
                
                // Ties (Transverse)
                TransverseBarTypeName = (UI_Combo_TieType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = tieSpacing,
                TransverseStartOffset = tieBotOff, // Start = Bottom
                TransverseEndOffset = tieTopOff,   // End = Top
                TransverseHookStartName = (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name,
                TransverseHookEndName = (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name,

                // Vertical Bars
                ColumnCountX = (int)ParseDouble(UI_Text_CountX.Text, 3),
                ColumnCountY = (int)ParseDouble(UI_Text_CountY.Text, 3),
                VerticalTopExtension = UI_Check_TopExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_TopExtValue.Text, 300)) : 0,
                VerticalBottomExtension = UI_Check_BotExt.IsChecked == true ? UnitConversion.MmToFeet(ParseDouble(UI_Text_BotExtValue.Text, 300)) : 0,
                
                // Vertical Hooks
                Layers = new List<RebarLayerConfig>() // We'll store vertical settings in a simplified layer or direct request
            };

            // Simplified: vertical bars use First Layer template for type/hooks
            request.Layers.Add(new RebarLayerConfig
            {
                VerticalBarTypeName = (UI_Combo_VerticalType.SelectedItem as RebarBarType)?.Name,
                HookStartName = (UI_Combo_VHookBot.SelectedItem as RebarHookType)?.Name,
                HookEndName = (UI_Combo_VHookTop.SelectedItem as RebarHookType)?.Name,
                HookStartOutward = UI_Check_VHookBotOut.IsChecked == true,
                HookEndOutward = UI_Check_VHookTopOut.IsChecked == true
            });

            return request;
        }

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
