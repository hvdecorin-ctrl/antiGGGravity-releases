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
            UpdateCrossSection();
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

            UI_Combo_SideType.ItemsSource = _rebarTypes;
            UI_Combo_SideType.DisplayMemberPath = "Name";
            UI_Combo_SideType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

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
                UI_Check_SideRebar.IsChecked = SettingsManager.GetBool(VIEW_NAME, "SideRebarEnabled", false);
                UI_Text_SideRows.Text = SettingsManager.Get(VIEW_NAME, "SideRows", "2");

                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"));
                SelectByName(UI_Combo_BotType, SettingsManager.Get(VIEW_NAME, "BotType"));
                SelectByName(UI_Combo_TransType, SettingsManager.Get(VIEW_NAME, "TransType"));
                SelectByName(UI_Combo_SideType, SettingsManager.Get(VIEW_NAME, "SideType"));

                SelectHookByName(UI_Combo_TopHook, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BotHook, SettingsManager.Get(VIEW_NAME, "BotHook"));
                SelectHookByName(UI_Combo_HookStart, SettingsManager.Get(VIEW_NAME, "HookStart"));
                SelectHookByName(UI_Combo_HookEnd, SettingsManager.Get(VIEW_NAME, "HookEnd"));

                UI_Check_TopHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopHookOverride", false);
                UI_Text_TopHookLength.Text = SettingsManager.Get(VIEW_NAME, "TopHookLength", "300");
                UI_Check_BotHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotHookOverride", false);
                UI_Text_BotHookLength.Text = SettingsManager.Get(VIEW_NAME, "BotHookLength", "300");
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
                SettingsManager.Set(VIEW_NAME, "SideRebarEnabled", (UI_Check_SideRebar.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SideRows", UI_Text_SideRows.Text);

                SettingsManager.Set(VIEW_NAME, "TopType", TransTypeName(UI_Combo_TopType));
                SettingsManager.Set(VIEW_NAME, "BotType", TransTypeName(UI_Combo_BotType));
                SettingsManager.Set(VIEW_NAME, "TransType", TransTypeName(UI_Combo_TransType));
                SettingsManager.Set(VIEW_NAME, "SideType", TransTypeName(UI_Combo_SideType));

                SettingsManager.Set(VIEW_NAME, "TopHook", HookName(UI_Combo_TopHook));
                SettingsManager.Set(VIEW_NAME, "BotHook", HookName(UI_Combo_BotHook));
                SettingsManager.Set(VIEW_NAME, "HookStart", HookName(UI_Combo_HookStart));
                SettingsManager.Set(VIEW_NAME, "HookEnd", HookName(UI_Combo_HookEnd));

                SettingsManager.Set(VIEW_NAME, "TopHookOverride", (UI_Check_TopHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopHookLength", UI_Text_TopHookLength.Text);
                SettingsManager.Set(VIEW_NAME, "BotHookOverride", (UI_Check_BotHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotHookLength", UI_Text_BotHookLength.Text);

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

                // Side Bars
                EnableSideRebar = UI_Check_SideRebar.IsChecked == true,
                SideRebarTypeName = (UI_Combo_SideType.SelectedItem as RebarBarType)?.Name,
                SideRebarRows = (int)ParseDouble(UI_Text_SideRows.Text, 2),

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
                    HookEndOutward = false,
                    OverrideHookLength = UI_Check_TopHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopHookLength.Text, 300))
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
                    HookEndOutward = false,
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300))
                });
            }

            return request;
        }

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            UpdateCrossSection();
        }

        private void UpdateCrossSection()
        {
            if (UI_Canvas_CrossSection == null || UI_Check_BotBars == null || UI_Check_TopBars == null || UI_Check_SideRebar == null) return;
            var canvas = UI_Canvas_CrossSection;
            canvas.Children.Clear();

            double cW = canvas.Width;   // 200
            double cH = canvas.Height;  // 190
            double marginL = 10;
            double marginR = 10;
            double footW = cW - marginL - marginR;
            double footH = 140; // x2 (70 -> 140)

            // Concrete outline
            var concreteRect = new System.Windows.Shapes.Rectangle
            {
                Width = footW,
                Height = footH,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(concreteRect, marginL);
            Canvas.SetTop(concreteRect, cH / 2.0 - footH / 2.0);
            canvas.Children.Add(concreteRect);

            double cover = 15;
            double dotR = 4.5; // Match column canvas (radius 4.5 = 9px diameter)

            // Stirrup outline (dashed)
            var stirrupRect = new System.Windows.Shapes.Rectangle
            {
                Width = footW - 2 * cover,
                Height = footH - 2 * cover,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120)),
                StrokeThickness = 3, // Thicker stirrup
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(stirrupRect, marginL + cover);
            Canvas.SetTop(stirrupRect, cH / 2.0 - footH / 2.0 + cover);
            canvas.Children.Add(stirrupRect);

            double internalOff = 8; // Offset to place bars inside stirrup

            // --- BOTTOM BARS (Longitudinal) ---
            if (UI_Check_BotBars.IsChecked == true)
            {
                double botY = (cH / 2.0 + footH / 2.0) - cover - internalOff;
                int count = (int)ParseDouble(UI_Text_BotCount.Text, 4);
                DrawBarRow(canvas, marginL + cover + internalOff, marginL + footW - cover - internalOff, botY, count, dotR, Brushes.MidnightBlue);
            }

            // --- TOP BARS (Longitudinal) ---
            if (UI_Check_TopBars.IsChecked == true)
            {
                double topY = (cH / 2.0 - footH / 2.0) + cover + internalOff;
                int count = (int)ParseDouble(UI_Text_TopCount.Text, 4);
                DrawBarRow(canvas, marginL + cover + internalOff, marginL + footW - cover - internalOff, topY, count, dotR, Brushes.DarkRed);
            }

            // --- SIDE BARS ---
            if (UI_Check_SideRebar.IsChecked == true)
            {
                int rows = (int)ParseDouble(UI_Text_SideRows.Text, 2);
                double internalOffSide = 12; // Further inside (5 -> 12)
                double sTop = (cH / 2.0 - footH / 2.0) + cover + 15;
                double sBot = (cH / 2.0 + footH / 2.0) - cover - 15;
                if (sBot > sTop && rows > 0)
                {
                    double step = (sBot - sTop) / (rows + 1);
                    for (int i = 1; i <= rows; i++)
                    {
                        double y = sTop + step * i;
                        DrawDot(canvas, marginL + cover + internalOffSide, y, 3.5, Brushes.SlateGray); // Side dot 7px
                        DrawDot(canvas, marginL + footW - cover - internalOffSide, y, 3.5, Brushes.SlateGray);
                    }
                }
            }
        }

        private void DrawBarRow(Canvas canvas, double left, double right, double y, int count, double r, Brush fill)
        {
            if (count < 1) return;
            if (count == 1)
            {
                DrawDot(canvas, (left + right) / 2.0, y, r, fill);
            }
            else
            {
                double w = right - left;
                double step = w / (count - 1);
                for (int i = 0; i < count; i++)
                    DrawDot(canvas, left + step * i, y, r, fill);
            }
        }

        private void DrawDot(Canvas canvas, double cx, double cy, double r, Brush fill)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(dot, cx - r);
            Canvas.SetTop(dot, cy - r);
            canvas.Children.Add(dot);
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
