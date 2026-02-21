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
        private Document _doc;
        private List<RebarBarType> _rebarTypes;

        public WallCornerUPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
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

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.WallCornerU,
                RemoveExisting = UI_Check_RemoveExisting.IsChecked == true,

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

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
