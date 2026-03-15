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
    public partial class PadShapeRebarPanel : UserControl
    {
        private const string VIEW_NAME = "RebarSuite_PadShape";
        private Document _doc;
        private List<RebarBarType> _rebarTypes;
        private List<HookViewModel> _hookList;

        public PadShapeRebarPanel(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadData();
            LoadSettings();
            UpdateCrossSection();
        }

        private void LoadData()
        {
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

            UI_Combo_SideType.ItemsSource = _rebarTypes;
            UI_Combo_SideType.DisplayMemberPath = "Name";
            UI_Combo_SideType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

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
        }

        private void LoadSettings()
        {
            try
            {
                UI_Text_TopSpacing.Text = SettingsManager.Get(VIEW_NAME, "TopSpacing", "200");
                UI_Text_BotSpacing.Text = SettingsManager.Get(VIEW_NAME, "BotSpacing", "200");

                UI_Check_TopBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopBarsEnabled", false);
                UI_Check_BotBars.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotBarsEnabled", false);
                UI_Check_SideRebar.IsChecked = SettingsManager.GetBool(VIEW_NAME, "SideRebarEnabled", false);
                UI_Text_SideSpacing.Text = SettingsManager.Get(VIEW_NAME, "SideSpacing", "200");

                SelectByName(UI_Combo_TopType, SettingsManager.Get(VIEW_NAME, "TopType"));
                SelectByName(UI_Combo_BotType, SettingsManager.Get(VIEW_NAME, "BotType"));
                SelectByName(UI_Combo_SideType, SettingsManager.Get(VIEW_NAME, "SideType"));

                SelectHookByName(UI_Combo_TopHook, SettingsManager.Get(VIEW_NAME, "TopHook"));
                SelectHookByName(UI_Combo_BotHook, SettingsManager.Get(VIEW_NAME, "BotHook"));

                UI_Check_TopHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "TopHookOverride", false);
                UI_Text_TopHookLength.Text = SettingsManager.Get(VIEW_NAME, "TopHookLength", "300");
                UI_Check_BotHookOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "BotHookOverride", false);
                UI_Text_BotHookLength.Text = SettingsManager.Get(VIEW_NAME, "BotHookLength", "300");

                UI_Check_SideLegOverride.IsChecked = SettingsManager.GetBool(VIEW_NAME, "SideLegOverride", false);
                UI_Text_SideLegLength.Text = SettingsManager.Get(VIEW_NAME, "SideLegLength", "300");
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                SettingsManager.Set(VIEW_NAME, "TopSpacing", UI_Text_TopSpacing.Text);
                SettingsManager.Set(VIEW_NAME, "BotSpacing", UI_Text_BotSpacing.Text);

                SettingsManager.Set(VIEW_NAME, "TopBarsEnabled", (UI_Check_TopBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotBarsEnabled", (UI_Check_BotBars.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SideRebarEnabled", (UI_Check_SideRebar.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SideSpacing", UI_Text_SideSpacing.Text);

                SettingsManager.Set(VIEW_NAME, "TopType", TransTypeName(UI_Combo_TopType));
                SettingsManager.Set(VIEW_NAME, "BotType", TransTypeName(UI_Combo_BotType));
                SettingsManager.Set(VIEW_NAME, "SideType", TransTypeName(UI_Combo_SideType));

                SettingsManager.Set(VIEW_NAME, "TopHook", HookName(UI_Combo_TopHook));
                SettingsManager.Set(VIEW_NAME, "BotHook", HookName(UI_Combo_BotHook));

                SettingsManager.Set(VIEW_NAME, "TopHookOverride", (UI_Check_TopHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "TopHookLength", UI_Text_TopHookLength.Text);
                SettingsManager.Set(VIEW_NAME, "BotHookOverride", (UI_Check_BotHookOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "BotHookLength", UI_Text_BotHookLength.Text);

                SettingsManager.Set(VIEW_NAME, "SideLegOverride", (UI_Check_SideLegOverride.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "SideLegLength", UI_Text_SideLegLength.Text);

                SettingsManager.SaveAll();
            }
            catch { }
        }

        public RebarRequest GetRequest()
        {
            var request = new RebarRequest
            {
                HostType = ElementHostType.PadShape,
                RemoveExisting = false,
                
                EnableSideRebar = UI_Check_SideRebar.IsChecked == true,
                SideRebarTypeName = TransTypeName(UI_Combo_SideType),
                SideRebarSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_SideSpacing.Text, 200)),
                EnableSideRebarOverrideLeg = UI_Check_SideLegOverride.IsChecked == true,
                SideRebarLegLength = UnitConversion.MmToFeet(ParseDouble(UI_Text_SideLegLength.Text, 300)),

                Layers = new List<RebarLayerConfig>()
            };

            // Top Layer
            if (UI_Check_TopBars.IsChecked == true)
            {
                request.Layers.Add(new RebarLayerConfig
                {
                    Side = RebarSide.Top,
                    VerticalBarTypeName = (UI_Combo_TopType.SelectedItem as RebarBarType)?.Name,
                    VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_TopSpacing.Text, 200)),
                    HookStartName = HookName(UI_Combo_TopHook),
                    HookEndName = HookName(UI_Combo_TopHook),
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
                    VerticalSpacing = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotSpacing.Text, 200)),
                    HookStartName = HookName(UI_Combo_BotHook),
                    HookEndName = HookName(UI_Combo_BotHook),
                    OverrideHookLength = UI_Check_BotHookOverride.IsChecked == true,
                    HookLengthOverride = UnitConversion.MmToFeet(ParseDouble(UI_Text_BotHookLength.Text, 300))
                });
            }

            return request;
        }

        public void toggle_visibility(object sender, RoutedEventArgs e)
        {
            if (UI_Check_TopBars == null || UI_Check_BotBars == null || UI_Check_SideRebar == null) return;

            bool topEnabled = UI_Check_TopBars.IsChecked == true;
            UI_Check_TopBars.Opacity = topEnabled ? 1.0 : 0.5;
            if (UI_Group_TopBars != null)
            {
                UI_Group_TopBars.IsEnabled = topEnabled;
                UI_Group_TopBars.Opacity = topEnabled ? 1.0 : 0.5;
            }

            bool botEnabled = UI_Check_BotBars.IsChecked == true;
            UI_Check_BotBars.Opacity = botEnabled ? 1.0 : 0.5;
            if (UI_Group_BotBars != null)
            {
                UI_Group_BotBars.IsEnabled = botEnabled;
                UI_Group_BotBars.Opacity = botEnabled ? 1.0 : 0.5;
            }

            bool sideEnabled = UI_Check_SideRebar.IsChecked == true;
            UI_Check_SideRebar.Opacity = sideEnabled ? 1.0 : 0.5;
            if (UI_Group_SideRebar != null)
            {
                UI_Group_SideRebar.IsEnabled = sideEnabled;
                UI_Group_SideRebar.Opacity = sideEnabled ? 1.0 : 0.5;
            }

            UpdateCrossSection();
        }

        private void UpdateCrossSection()
        {
            if (UI_Canvas_CrossSection == null) return;
            var canvas = UI_Canvas_CrossSection;
            canvas.Children.Clear();

            double cW = canvas.Width;   // 200
            double cH = canvas.Height;  // 190
            double marginL = 10;
            double marginR = 10;
            double footW = cW - marginL - marginR;
            double footH = 120;

            var concreteRect = new System.Windows.Shapes.Rectangle
            {
                Width = footW,
                Height = footH,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                RadiusX = 3, RadiusY = 3
            };
            Canvas.SetLeft(concreteRect, marginL);
            Canvas.SetTop(concreteRect, cH / 2.0 - footH / 2.0);
            canvas.Children.Add(concreteRect);

            double cover = 15;
            double dotR = 4.5;

            if (UI_Check_BotBars.IsChecked == true)
            {
                double botY = (cH / 2.0 + footH / 2.0) - cover;
                DrawHLineWithHooks(canvas, marginL + cover, marginL + footW - cover, botY + 2, 3, Brushes.MidnightBlue, true);
                DrawBarRow(canvas, marginL + cover + 10, marginL + footW - cover - 10, botY - 5, 5, dotR, Brushes.MidnightBlue);
            }

            if (UI_Check_TopBars.IsChecked == true)
            {
                double topY = (cH / 2.0 - footH / 2.0) + cover;
                DrawHLineWithHooks(canvas, marginL + cover, marginL + footW - cover, topY - 2, 3, Brushes.DarkRed, false);
                DrawBarRow(canvas, marginL + cover + 10, marginL + footW - cover - 10, topY + 5, 5, dotR, Brushes.DarkRed);
            }

            if (UI_Check_SideRebar.IsChecked == true)
            {
                double sideX_L = marginL + cover + 8;
                double sideX_R = marginL + footW - cover - 8;
                DrawDot(canvas, sideX_L, cH / 2.0, 3.5, Brushes.SlateGray);
                DrawDot(canvas, sideX_R, cH / 2.0, 3.5, Brushes.SlateGray);
            }
        }

        private void DrawHLine(Canvas canvas, double x1, double x2, double y, double thickness, Brush stroke)
        {
            var line = new System.Windows.Shapes.Line { X1 = x1, Y1 = y, X2 = x2, Y2 = y, Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            canvas.Children.Add(line);
        }

        private void DrawHLineWithHooks(Canvas canvas, double x1, double x2, double y, double thickness, Brush stroke, bool hookUp)
        {
            DrawHLine(canvas, x1, x2, y, thickness, stroke);
            double hookLen = 30;
            double vy1 = y;
            double vy2 = hookUp ? y - hookLen : y + hookLen;
            DrawVLine(canvas, x1, vy1, vy2, thickness, stroke);
            DrawVLine(canvas, x2, vy1, vy2, thickness, stroke);
        }

        private void DrawVLine(Canvas canvas, double x, double y1, double y2, double thickness, Brush stroke)
        {
            var line = new System.Windows.Shapes.Line { X1 = x, Y1 = y1, X2 = x, Y2 = y2, Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            canvas.Children.Add(line);
        }

        private void DrawBarRow(Canvas canvas, double left, double right, double y, int count, double r, Brush fill)
        {
            double w = right - left;
            double step = w / (count - 1);
            for (int i = 0; i < count; i++) DrawDot(canvas, left + step * i, y, r, fill);
        }

        private void DrawDot(Canvas canvas, double cx, double cy, double r, Brush fill)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = r * 2, Height = r * 2, Fill = fill, Stroke = Brushes.White, StrokeThickness = 0.5 };
            Canvas.SetLeft(dot, cx - r); Canvas.SetTop(dot, cy - r);
            canvas.Children.Add(dot);
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
    }
}
