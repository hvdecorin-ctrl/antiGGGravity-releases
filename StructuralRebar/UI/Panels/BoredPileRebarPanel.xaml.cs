using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.Utilities;
using antiGGGravity.StructuralRebar.DTO;
using antiGGGravity.StructuralRebar.Constants;

namespace antiGGGravity.StructuralRebar.UI.Panels
{
    public partial class BoredPileRebarPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_BoredPile";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public BoredPileRebarPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSettings();
            UpdateDisplay(null, null);
        }

        private void LoadData()
        {
            _rebarTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => x.Name)
                .ToList();

            UI_Combo_MainType.ItemsSource = _rebarTypes;
            UI_Combo_MainType.DisplayMemberPath = "Name";
            UI_Combo_MainType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D20")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_TransType.ItemsSource = _rebarTypes;
            UI_Combo_TransType.DisplayMemberPath = "Name";
            UI_Combo_TransType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D10")) ?? _rebarTypes.FirstOrDefault();

            var hookTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .OrderBy(x => x.Name)
                .ToList();

            _hookList = new List<HookViewModel> { new HookViewModel(null) };
            _hookList.AddRange(hookTypes.Select(h => new HookViewModel(h)));

            UI_Combo_MainHook.ItemsSource = _hookList;
            UI_Combo_MainHook.DisplayMemberPath = "Name";
            UI_Combo_MainHook.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_MainCount.Text = SettingsManager.Get(VIEW_NAME, "MainCount", "8");
                UI_Text_TransSpacing.Text = SettingsManager.Get(VIEW_NAME, "TransSpacing", "200");
                UI_Combo_TransMode.SelectedIndex = SettingsManager.GetInt(VIEW_NAME, "TransMode", 0);

                SelectByName(UI_Combo_MainType, SettingsManager.Get(VIEW_NAME, "MainType"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));
                SelectHookByName(UI_Combo_MainHook, SettingsManager.Get(VIEW_NAME, "MainHook"));
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "MainCount", UI_Text_MainCount.Text);
                SettingsManager.Set(VIEW_NAME, "TransSpacing", UI_Text_TransSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "TransMode", UI_Combo_TransMode.SelectedIndex.ToString());

                SettingsManager.Set(VIEW_NAME, "MainType", TransTypeName(UI_Combo_MainType));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));
                SettingsManager.Set(VIEW_NAME, "MainHook", HookName(UI_Combo_MainHook));

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.BoredPile,
                RemoveExisting = false,
                
                VerticalBarTypeName = TransTypeName(UI_Combo_MainType),
                PileBarCount = ParseInt(UI_Text_MainCount.Text, 8),
                
                TransverseBarTypeName = TransTypeName(UI_Combo_TransType),
                TransverseSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TransSpacing.Text, 200)),
                EnableSpiral = UI_Combo_TransMode.SelectedIndex == 1,
                
                TransverseHookStartName = HookName(UI_Combo_MainHook), // Using for top reinforcement if needed, or mapping specifically
            };

            return request;
        }

        private void UpdateDisplay(object sender, RoutedEventArgs e)
        {
            if (UI_Canvas_Preview == null) return;
            
            // Update labels
            if (UI_Label_Spacing != null)
                UI_Label_Spacing.Text = (UI_Combo_TransMode.SelectedIndex == 1) ? "Pitch (mm)" : "Spacing (mm)";

            var canvas = UI_Canvas_Preview;
            canvas.Children.Clear();

            double center = 100;
            double radius = 80;

            // Pile circle
            var pileCircle = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = Brushes.LightGray,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
            };
            Canvas.SetLeft(pileCircle, center - radius);
            Canvas.SetTop(pileCircle, center - radius);
            canvas.Children.Add(pileCircle);

            // Transverse (Hoop/Spiral)
            double hoopRadius = radius - 15;
            var hoop = new System.Windows.Shapes.Ellipse
            {
                Width = hoopRadius * 2,
                Height = hoopRadius * 2,
                Stroke = Brushes.SlateGray,
                StrokeThickness = 2
            };
            Canvas.SetLeft(hoop, center - hoopRadius);
            Canvas.SetTop(hoop, center - hoopRadius);
            canvas.Children.Add(hoop);

            // Main Bars
            int count = ParseInt(UI_Text_MainCount.Text, 8);
            double barRadius = hoopRadius;
            double dotR = 5;

            for (int i = 0; i < count; i++)
            {
                double angle = (2 * Math.PI / count) * i;
                double x = center + barRadius * Math.Cos(angle);
                double y = center + barRadius * Math.Sin(angle);

                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = dotR * 2,
                    Height = dotR * 2,
                    Fill = Brushes.MidnightBlue,
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(dot, x - dotR);
                Canvas.SetTop(dot, y - dotR);
                canvas.Children.Add(dot);
            }
        }

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

        private double ParseDouble(string text, double defaultValue) => double.TryParse(text, out double result) ? result : defaultValue;
        private int ParseInt(string text, int defaultValue) => int.TryParse(text, out int result) ? result : defaultValue;
    }
}
