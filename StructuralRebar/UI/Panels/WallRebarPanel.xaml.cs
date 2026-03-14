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
using antiGGGravity.StructuralRebar.Core.Geometry;

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

            UI_Combo_StarterType.ItemsSource = _rebarTypes;
            UI_Combo_StarterType.DisplayMemberPath = "Name";
            UI_Combo_StarterType.SelectedItem = _rebarTypes.FirstOrDefault(x => x.Name.Contains("D16")) ?? _rebarTypes.FirstOrDefault();

            UI_Combo_StarterHookEnd.ItemsSource = _hookList;
            UI_Combo_StarterHookEnd.DisplayMemberPath = "Name";
            UI_Combo_StarterHookEnd.SelectedIndex = 0;
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

                // Multi-Level
                UI_Check_MultiLevel.IsChecked = SettingsManager.GetBool(VIEW_NAME, "MultiLevel", false);
                UI_Check_MultiLevel_Changed(null, null);
                UI_Text_LapSplice.Text = SettingsManager.Get(VIEW_NAME, "LapSplice", "600");

                string lapMode = SettingsManager.Get(VIEW_NAME, "LapMode", "Auto (Code)");
                foreach (ComboBoxItem item in UI_Combo_LapMode.Items)
                {
                    if (item.Content.ToString() == lapMode)
                    {
                        UI_Combo_LapMode.SelectedItem = item;
                        break;
                    }
                }
                UI_Combo_LapMode_Changed(null, null);

                UI_Text_StarterBar.Text = SettingsManager.Get(VIEW_NAME, "StarterBar", "800");
                UI_Check_StarterEnabled.IsChecked = SettingsManager.GetBool(VIEW_NAME, "StarterEnabled", true);
                UI_Check_StarterEnabled_Click(null, null);


                SelectByName(UI_Combo_VertType, SettingsManager.Get(VIEW_NAME, "VertType"));
                SelectByName(UI_Combo_HorizType, SettingsManager.Get(VIEW_NAME, "HorizType"));

                SelectHookByName(UI_Combo_VertHookStart, SettingsManager.Get(VIEW_NAME, "VertHookStart"));
                SelectHookByName(UI_Combo_VertHookEnd, SettingsManager.Get(VIEW_NAME, "VertHookEnd"));
                SelectHookByName(UI_Combo_HorizHookStart, SettingsManager.Get(VIEW_NAME, "HorizHookStart"));
                SelectHookByName(UI_Combo_HorizHookEnd, SettingsManager.Get(VIEW_NAME, "HorizHookEnd"));

                string crankPos = SettingsManager.Get(VIEW_NAME, "CrankPos", "Lower Wall");
                foreach (ComboBoxItem item in UI_Combo_CrankPos.Items)
                {
                    if (item.Content.ToString() == crankPos)
                    {
                        UI_Combo_CrankPos.SelectedItem = item;
                        break;
                    }
                }

                SelectByName(UI_Combo_StarterType, SettingsManager.Get(VIEW_NAME, "StarterType"));
                SelectHookByName(UI_Combo_StarterHookEnd, SettingsManager.Get(VIEW_NAME, "StarterHookEnd"));

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
                
                DrawWallCrossSection();
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

                // Multi-Level
                SettingsManager.Set(VIEW_NAME, "MultiLevel", (UI_Check_MultiLevel.IsChecked == true).ToString());
                SettingsManager.Set(VIEW_NAME, "LapSplice", UI_Text_LapSplice.Text);
                SettingsManager.Set(VIEW_NAME, "LapMode", (UI_Combo_LapMode.SelectedItem as ComboBoxItem)?.Content.ToString());
                SettingsManager.Set(VIEW_NAME, "StarterBar", UI_Text_StarterBar.Text);
                SettingsManager.Set(VIEW_NAME, "StarterEnabled", (UI_Check_StarterEnabled.IsChecked == true).ToString());


                SettingsManager.Set(VIEW_NAME, "VertType", TransTypeName(UI_Combo_VertType));
                SettingsManager.Set(VIEW_NAME, "HorizType", TransTypeName(UI_Combo_HorizType));

                SettingsManager.Set(VIEW_NAME, "VertHookStart", HookName(UI_Combo_VertHookStart));
                SettingsManager.Set(VIEW_NAME, "VertHookEnd", HookName(UI_Combo_VertHookEnd));
                SettingsManager.Set(VIEW_NAME, "HorizHookStart", HookName(UI_Combo_HorizHookStart));
                SettingsManager.Set(VIEW_NAME, "HorizHookEnd", HookName(UI_Combo_HorizHookEnd));

                SettingsManager.Set(VIEW_NAME, "CrankPos", (UI_Combo_CrankPos.SelectedItem as ComboBoxItem)?.Content.ToString());
                SettingsManager.Set(VIEW_NAME, "StarterType", TransTypeName(UI_Combo_StarterType));
                SettingsManager.Set(VIEW_NAME, "StarterHookEnd", HookName(UI_Combo_StarterHookEnd));

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
                EnableLapSplice = UI_Check_MultiLevel.IsChecked == true, // Using MultiLevel checkbox to drive Stack processing
                
                // Multi-Level specific parameters mapping to DTO
                EnableZoneSpacing = false, // Walls usually don't have confinement zones like columns
                VerticalContinuousSpliceLength = IsAutoLapMode()
                    ? 0  // Auto: engine uses code-calculated lap length
                    : UnitConversion.MmToFeet(ParseDouble(UI_Text_LapSplice.Text, 600)),
                StarterDevLength = (UI_Check_StarterEnabled.IsChecked == true)
                    ? UnitConversion.MmToFeet(ParseDouble(UI_Text_StarterBar.Text, 800))
                    : 0,
                CrankPosition = (UI_Combo_CrankPos.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Lower Wall",
                EnableStarterBars = (UI_Check_StarterEnabled.IsChecked == true),
                StarterBarTypeName = (UI_Combo_StarterType.SelectedItem as RebarBarType)?.Name,
                StarterHookEndName = HookName(UI_Combo_StarterHookEnd),

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
            else if (request.WallLayerConfig == "Both Faces")
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
            else if (request.WallLayerConfig == "External Face")
            {
                hLayer.Face = RebarLayerFace.Exterior;
                hLayer.HorizontalOffset = 1;
                request.Layers.Add(hLayer);
            }
            else if (request.WallLayerConfig == "Internal Face")
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
            if (UI_Text_VertBotExt == null || UI_Check_VertBotExt == null) return;
            bool hasExt = UI_Check_VertBotExt.IsChecked == true;
            UI_Check_VertBotExt.Opacity = hasExt ? 1.0 : 0.5;
            UI_Text_VertBotExt.IsEnabled = hasExt;
            UI_Text_VertBotExt.Opacity = hasExt ? 1.0 : 0.5;
        }

        private void UI_Check_StarterEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_StarterFields == null || UI_Check_StarterEnabled == null) return;
            bool hasStarters = UI_Check_StarterEnabled.IsChecked == true;
            UI_Check_StarterEnabled.Opacity = hasStarters ? 1.0 : 0.5;
            UI_Panel_StarterFields.IsEnabled = hasStarters;
            UI_Panel_StarterFields.Opacity = hasStarters ? 1.0 : 0.5;
        }

        private void UI_Check_VertTopExt_Click(object sender, RoutedEventArgs e)
        {
            if (UI_Text_VertTopExt == null || UI_Check_VertTopExt == null) return;
            bool hasTopExt = UI_Check_VertTopExt.IsChecked == true;
            UI_Check_VertTopExt.Opacity = hasTopExt ? 1.0 : 0.5;
            UI_Text_VertTopExt.IsEnabled = hasTopExt;
            UI_Text_VertTopExt.Opacity = hasTopExt ? 1.0 : 0.5;
        }

        private double ParseDouble(string text, double defaultValue)
        {
            return double.TryParse(text, out double result) ? result : defaultValue;
        }

        private void LayerConfig_Changed(object sender, SelectionChangedEventArgs e)
        {
            DrawWallCrossSection();
        }

        private void DrawWallCrossSection()
        {
            if (UI_CrossSectionCanvas == null || UI_CrossSectionLabel == null) return;
            if (_doc == null) return; // Wait until ready

            UI_CrossSectionCanvas.Children.Clear();

            string config = (UI_Combo_LayerConfig.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Centre";
            UI_CrossSectionLabel.Text = config;

            double w = UI_CrossSectionCanvas.Width;
            double h = UI_CrossSectionCanvas.Height;

            // Wall Rectangle (Original layout)
            double wallW = 60;
            double wallH = 180;
            double cx = w / 2;
            double cy = h / 2;
            double left = cx - wallW / 2;
            double top = cy - wallH / 2;

            var wallRect = new System.Windows.Shapes.Rectangle
            {
                Width = wallW,
                Height = wallH,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xEB, 0xEF)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(wallRect, left);
            Canvas.SetTop(wallRect, top);
            UI_CrossSectionCanvas.Children.Add(wallRect);

            // Rebar params length
            double cover = 10; // visual cover
            double dotRadius = 4.5;
            
            // Highlight colors matching Column canvas style
            var rebarBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x70, 0x8B)); // Blue
            var rebarStroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x55, 0x77)); // Dark Blue

            // Helper to draw a line of vertical bars and horizontal link
            // horizOffset: relative X shift for the horizontal line so it sits outside the vertical bars
            void DrawFace(double vertX, double horizX)
            {
                // Horizontal bar (represented as a vertical line segment)
                var horizLine = new System.Windows.Shapes.Line
                {
                    X1 = horizX,
                    Y1 = top + cover,
                    X2 = horizX,
                    Y2 = top + wallH - cover,
                    Stroke = rebarBrush,
                    StrokeThickness = 1.2
                };
                UI_CrossSectionCanvas.Children.Add(horizLine);

                // Vertical bars (dots)
                int numDots = 6;
                
                // Shift first and last dots inward so their edges don't extend past the horizontal line
                double startY = top + cover + dotRadius; 
                double endY = top + wallH - cover - dotRadius;
                double spacing = (endY - startY) / (numDots - 1);

                for (int i = 0; i < numDots; i++)
                {
                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = dotRadius * 2,
                        Height = dotRadius * 2,
                        Fill = rebarBrush,
                        Stroke = rebarStroke,
                        StrokeThickness = 0.8
                    };
                    Canvas.SetLeft(dot, vertX - dotRadius);
                    Canvas.SetTop(dot, startY + (i * spacing) - dotRadius);
                    UI_CrossSectionCanvas.Children.Add(dot);
                }
            }

            double gap = 4; // visual gap between vertical and horizontal layers
            if (config == "Both Faces")
            {
                // External face (right side: horizontal outside, vertical inside)
                double rightVertX = left + wallW - cover - dotRadius - gap;
                DrawFace(rightVertX, rightVertX + dotRadius + gap);
                // Internal face (left side: horizontal outside, vertical inside)
                double leftVertX = left + cover + dotRadius + gap;
                DrawFace(leftVertX, leftVertX - dotRadius - gap);
            }
            else if (config == "Centre")
            {
                DrawFace(cx + gap, cx - gap);
            }
            else if (config == "External Face")
            {
                double rightVertX = left + wallW - cover - dotRadius - gap;
                DrawFace(rightVertX, rightVertX + dotRadius + gap);
            }
            else if (config == "Internal Face")
            {
                double leftVertX = left + cover + dotRadius + gap;
                DrawFace(leftVertX, leftVertX - dotRadius - gap);
            }
        }

        private void UI_Check_MultiLevel_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_Panel_MultiLevelFields == null || UI_Check_MultiLevel == null) return;
            bool isMultiLevel = UI_Check_MultiLevel.IsChecked == true;
            UI_Check_MultiLevel.Opacity = isMultiLevel ? 1.0 : 0.5;
            UI_Panel_MultiLevelFields.IsEnabled = isMultiLevel;
            UI_Panel_MultiLevelFields.Opacity = isMultiLevel ? 1.0 : 0.5;
        }

        private void UI_Combo_LapMode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (UI_Text_LapSplice == null) return;
            bool isAuto = IsAutoLapMode();
            UI_Text_LapSplice.IsEnabled = !isAuto;
            UI_Text_LapSplice.Opacity = !isAuto ? 1.0 : 0.5;
        }

        private bool IsAutoLapMode()
        {
            string mode = (UI_Combo_LapMode?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Auto (Code)";
            return mode.StartsWith("Auto", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Call this when the selected element changes in the main window
        /// </summary>
        public void UpdateStackInfo(Wall selectedWall)
        {
            if (selectedWall == null || UI_Check_MultiLevel.IsChecked != true)
            {
                UI_Text_StackInfo.Text = "No wall selected or Multi-Level mode disabled.";
                return;
            }

            try
            {
                var stack = MultiLevelResolver.FindWallStack(_doc, selectedWall);
                if (stack.Count <= 1)
                {
                    UI_Text_StackInfo.Text = "Only 1 wall found in stack.\nMulti-level logic will act as single-level.";
                    return;
                }

                var infoList = MultiLevelResolver.GetWallStackInfo(_doc, stack);
                
                string infoStr = $"Detected {stack.Count} stacked wall(s):\n";
                for (int i = 0; i < infoList.Count; i++)
                {
                    var info = infoList[i];
                    string marker = (stack[i].Id == selectedWall.Id) ? " ▶ " : "   ";
                    
                    double lengthMm = UnitConversion.FeetToMm(info.Length);
                    double thickMm = UnitConversion.FeetToMm(info.Thickness);
                    double heightMm = UnitConversion.FeetToMm(info.Height);
                    
                    infoStr += $"{marker}Lvl {i + 1}: {info.LevelName} | {lengthMm:F0}x{thickMm:F0} L, {heightMm:F0} H\n";
                }

                UI_Text_StackInfo.Text = infoStr.TrimEnd();
            }
            catch (Exception ex)
            {
                UI_Text_StackInfo.Text = "Error analyzing stack: " + ex.Message;
            }
        }
    }
}
