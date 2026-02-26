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
        private const string VIEW_NAME = "RebarSuite_StripFooting";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public StripFootingRebarPanel(Document doc)
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

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

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

        private void LoadSettings()
        {
            try
            {
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Text_TransStartOff.Text = SettingsManager.Get(VIEW_NAME, "TransStartOff", "50");
                UI_Text_TopCount.Text = SettingsManager.Get(VIEW_NAME, "TopCount", "4");
                UI_Text_BotCount.Text = SettingsManager.Get(VIEW_NAME, "BotCount", "4");

                UI_Check_TopBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopBarsEnabled", true);
                UI_Check_BotBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotBarsEnabled", true);


                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"));
                SelectByName(UI_Combo_BotType, SettingsManager.Get(VIEW_NAME, "BotType"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));

                SelectHookByName(UI_Combo_TopHook, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BotHook, SettingsManager.Get(VIEW_NAME, "BotHook"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransStartOff", UI_Text_TransStartOff.Text);
                SettingsManager.Set(VIEW_NAME, "TopCount", UI_Text_TopCount.Text);
                SettingsManager.Set(VIEW_NAME, "BotCount", UI_Text_BotCount.Text);

                SettingsManager.Set(VIEW_NAME, "TopBarsEnabled", (UI_Check_TopBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotBarsEnabled", (UI_Check_BotBars.IsChecked == true).ToString());


                SettingsManager.Set(VIEW_NAME, "TopType", TransTypeName(UI_Combo_TopType));
                SettingsManager.Set(VIEW_NAME, "BotType", TransTypeName(UI_Combo_BotType));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));

                SettingsManager.Set(VIEW_NAME, "TopHook", HookName(UI_Combo_TopHook));
                SettingsManager.Set(VIEW_NAME, "BotHook", HookName(UI_Combo_BotHook));
                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.StripFooting,
                RemoveExisting = false, // Handled by Window level
                EnableLapSplice = false, // Set by window/handler now

                // Stirrups (Transverse)
                TransverseBarTypeName = (UI_Combo_TransType.SelectedItem as RebarBarType)?.Name,
                TransverseSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransSpacing.Text, 200)),
                TransverseStartOffset = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransStartOff.Text, 50)),
                TransverseHookStartName = HookName(UI_Combo_HookStart),
                TransverseHookEndName = HookName(UI_Combo_HookEnd),

                // Longitudinal Offsets - Now handled automatically by Engine/Generator
                StockLength = 0, 
                StockLength_Backing = 0,
                
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
                    HookStartName = HookName(UI_Combo_TopHook),
                    HookEndName = HookName(UI_Combo_TopHook),
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
                    HookStartName = HookName(UI_Combo_BotHook),
                    HookEndName = HookName(UI_Combo_BotHook),
                    HookStartOutward = false,
                    HookEndOutward = false
                });
            }

            return request;
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

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }
    }
}
