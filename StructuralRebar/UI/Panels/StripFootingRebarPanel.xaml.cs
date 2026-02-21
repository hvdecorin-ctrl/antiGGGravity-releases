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
    public partial class StripFootingRebarPanel : UserControl
    {
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<RebarHookType> _hookList;

        public StripFootingRebarPanel(Document doc)
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

            UI_Combo_TopType.ItemsSource = _rebarTypes;
            UI_Combo_TopType.DisplayMemberPath = "Name";
            UI_Combo_TopType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_BotType.ItemsSource = _rebarTypes;
            UI_Combo_BotType.DisplayMemberPath = "Name";
            UI_Combo_BotType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TransType.ItemsSource = _rebarTypes;
            UI_Combo_TransType.DisplayMemberPath = "Name";
            UI_Combo_TransType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

            // Hook Types
            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<RebarHookType> { null };
            _hookList.AddRange(hookTypes);

            UI_Combo_TopHook.ItemsSource = _hookList;
            UI_Combo_TopHook.DisplayMemberPath = "Name";
            UI_Combo_TopHook.SelectedIndex = 0;

            UI_Combo_BotHook.ItemsSource = _hookList;
            UI_Combo_BotHook.DisplayMemberPath = "Name";
            UI_Combo_BotHook.SelectedIndex = 0;

            UI_Combo_HookStart.ItemsSource = _hookList;
            UI_Combo_HookStart.DisplayMemberPath = "Name";
            UI_Combo_HookStart.SelectedIndex = 0;

            UI_Combo_HookEnd.ItemsSource = _hookList;
            UI_Combo_HookEnd.DisplayMemberPath = "Name";
            UI_Combo_HookEnd.SelectedIndex = 0;
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.StripFooting,
                RemoveExisting = UI_Check_RemoveExisting.IsChecked == true,

                // Stirrups (Transverse)
                TransverseBarTypeName = (UI_Combo_TransType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransSpacing.Text, 200)),
                TransverseStartOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransStartOff.Text, 50)),
                TransverseHookStartName = (UI_Combo_HookStart.SelectedItem as RebarHookType)?.Name,
                TransverseHookEndName = (UI_Combo_HookEnd.SelectedItem as RebarHookType)?.Name,

                // Longitudinal Offsets
                StockLength = UnitConversion.MmToFeet(ParseDouble(UI_Text_LongStartOff.Text, 50)), // Reusing as Start
                StockLength_Backing = UnitConversion.MmToFeet(ParseDouble(UI_Text_LongEndOff.Text, 50)), // Reusing as End (temporary)
                
                Layers = new List<RebarLayerConfig>()
            };

            // Top Layer
            if (UI_Check_TopBars.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Side = RebarSide.Top,
                    VerticalBarTypeName = (UI_Combo_TopType.SelectedItem as RebarBarType)?.Name,
                    VerticalCount = (int)ParseDouble(UI_Text_TopCount.Text, 4),
                    HookStartName = (UI_Combo_TopHook.SelectedItem as RebarHookType)?.Name,
                    HookEndName = (UI_Combo_TopHook.SelectedItem as RebarHookType)?.Name,
                    HookStartOutward = false, // Not used for footings
                    HookEndOutward = false
                });
            }

            // Bottom Layer
            if (UI_Check_BotBars.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Side = RebarSide.Bottom,
                    VerticalBarTypeName = (UI_Combo_BotType.SelectedItem as RebarBarType)?.Name,
                    VerticalCount = (int)ParseDouble(UI_Text_BotCount.Text, 4),
                    HookStartName = (UI_Combo_BotHook.SelectedItem as RebarHookType)?.Name,
                    HookEndName = (UI_Combo_BotHook.SelectedItem as RebarHookType)?.Name,
                    HookStartOutward = false,
                    HookEndOutward = false
                });
            }

            return request;
        }

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
