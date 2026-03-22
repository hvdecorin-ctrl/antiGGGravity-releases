using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Calculators;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class DesignCodeReferenceWindow : Window
    {
        public DesignCodeReferenceWindow()
        {
            InitializeComponent();
            RefreshComparison();
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            UI_CustomPanel?.SaveSettings(); // Save global custom parameters
            Close();
        }

        // --- Comparison Tab ---
        private void CompParam_Changed(object sender, EventArgs e)
        {
            if (!IsLoaded) return;
            RefreshComparison();
        }

        private void RefreshComparison()
        {
            try
            {
                var grade = ParseGrade(UI_CompGrade);
                var steel = ParseSteel(UI_CompSteel);
                double barDia = ParseComboDouble(UI_CompBarDia, 16);
                double beamH = ParseDouble(UI_CompBeamDepth?.Text, 600);
                double colD = ParseDouble(UI_CompColDim?.Text, 400);

                var results = DesignCodeCalculator.CalculateAll(grade, steel, barDia, beamH, colD);

                // Build comparison rows: each row = { Category, Parameter, ACI318, AS3600, EC2, NZS3101 }
                var rows = new List<CompRow>();

                string z1 = "Zone 1: Anchorage Length and Lap Length";
                string z2 = "Zone 2: Stirrup Un-equal Distribution";
                string z3 = "Zone 3: Rebar Bending";

                // Zone 1
                AddCompRow(rows, z1, "Tension Ld (mm)",
                    $"{results[0].TensionDevLengthMm}", $"{results[1].TensionDevLengthMm}",
                    $"{results[2].TensionDevLengthMm}", $"{results[3].TensionDevLengthMm}");

                AddCompRow(rows, z1, "Compression Ld (mm)",
                    $"{results[0].CompressionDevLengthMm}", $"{results[1].CompressionDevLengthMm}",
                    $"{results[2].CompressionDevLengthMm}", $"{results[3].CompressionDevLengthMm}");

                AddCompRow(rows, z1, "Dev. Multiplier (×db)",
                    $"{results[0].DevMultiplier}", $"{results[1].DevMultiplier}",
                    $"{results[2].DevMultiplier}", $"{results[3].DevMultiplier}");

                AddCompRow(rows, z1, "Tension Lap (mm)",
                    $"{results[0].TensionLapMm}", $"{results[1].TensionLapMm}",
                    $"{results[2].TensionLapMm}", $"{results[3].TensionLapMm}");

                AddCompRow(rows, z1, "Lap Multiplier (×db)",
                    $"{results[0].LapMultiplier}", $"{results[1].LapMultiplier}",
                    $"{results[2].LapMultiplier}", $"{results[3].LapMultiplier}");

                AddCompRow(rows, z1, "Compression Lap (mm)",
                    $"{results[0].CompressionLapMm}", $"{results[1].CompressionLapMm}",
                    $"{results[2].CompressionLapMm}", $"{results[3].CompressionLapMm}");

                // Zone 2
                AddCompRow(rows, z2, "Beam End Zone Length",
                    results[0].BeamEndZoneLength, results[1].BeamEndZoneLength,
                    results[2].BeamEndZoneLength, results[3].BeamEndZoneLength);

                AddCompRow(rows, z2, "Beam End Zone Spacing (mm)",
                    $"{results[0].BeamEndZoneSpacingMm}", $"{results[1].BeamEndZoneSpacingMm}",
                    $"{results[2].BeamEndZoneSpacingMm}", $"{results[3].BeamEndZoneSpacingMm}");

                AddCompRow(rows, z2, "Column Confine Spacing (mm)",
                    $"{results[0].ColumnConfineSpacingMm}", $"{results[1].ColumnConfineSpacingMm}",
                    $"{results[2].ColumnConfineSpacingMm}", $"{results[3].ColumnConfineSpacingMm}");

                AddCompRow(rows, z2, "Column Mid Spacing (mm)",
                    $"{results[0].ColumnMidSpacingMm}", $"{results[1].ColumnMidSpacingMm}",
                    $"{results[2].ColumnMidSpacingMm}", $"{results[3].ColumnMidSpacingMm}");

                // Zone 3
                AddCompRow(rows, z3, "Hook 90° Extension (mm)",
                    $"{results[0].Hook90ExtMm}", $"{results[1].Hook90ExtMm}",
                    $"{results[2].Hook90ExtMm}", $"{results[3].Hook90ExtMm}");

                AddCompRow(rows, z3, "Hook 135° Extension (mm)",
                    $"{results[0].Hook135ExtMm}", $"{results[1].Hook135ExtMm}",
                    $"{results[2].Hook135ExtMm}", $"{results[3].Hook135ExtMm}");

                AddCompRow(rows, z3, "Bend Radius (mm)",
                    $"{results[0].BendRadiusMm}", $"{results[1].BendRadiusMm}",
                    $"{results[2].BendRadiusMm}", $"{results[3].BendRadiusMm}");

                // Bind to datagrid with grouping
                var view = CollectionViewSource.GetDefaultView(rows);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
                UI_ComparisonGrid.ItemsSource = view;
            }
            catch { }
        }

        private void AddCompRow(List<CompRow> rows, string category, string param, string aci, string as3600, string ec2, string nzs)
        {
            rows.Add(new CompRow
            {
                Category = category,
                Parameter = param,
                ACI318 = aci,
                AS3600 = as3600,
                EC2 = ec2,
                NZS3101 = nzs
            });
        }

        // --- Calculator Tab ---
        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var code = ParseCode(UI_CalcCode);
                var grade = ParseGrade(UI_CalcGrade);
                var steel = ParseSteel(UI_CalcSteel);
                double barDia = ParseComboDouble(UI_CalcBarDia, 20);
                double beamH = ParseDouble(UI_CalcBeamDepth.Text, 600);
                double colD = ParseDouble(UI_CalcColDim.Text, 400);

                var result = DesignCodeCalculator.Calculate(code, grade, steel, barDia, beamH, colD);

                UI_ResultTitle.Text = $"RESULTS — {result.CodeName}";

                string z1 = "Zone 1: Anchorage Length and Lap Length";
                string z2 = "Zone 2: Stirrup Un-equal Distribution";
                string z3 = "Zone 3: Rebar Bending";

                var rows = new List<CalcRow>
                {
                    new CalcRow(z1, "Tension Dev. Length Ld", $"{result.TensionDevLengthMm} mm"),
                    new CalcRow(z1, "Dev. Multiplier", $"{result.DevMultiplier} × db"),
                    new CalcRow(z1, "Compression Dev. Length", $"{result.CompressionDevLengthMm} mm"),
                    new CalcRow(z1, "Tension Lap Length", $"{result.TensionLapMm} mm"),
                    new CalcRow(z1, "Lap Multiplier", $"{result.LapMultiplier} × db"),
                    new CalcRow(z1, "Compression Lap Length", $"{result.CompressionLapMm} mm"),

                    new CalcRow(z2, "Beam End Zone Spacing", $"{result.BeamEndZoneSpacingMm} mm"),
                    new CalcRow(z2, "Beam End Zone Length", result.BeamEndZoneLength),
                    new CalcRow(z2, "Column Confine Spacing", $"{result.ColumnConfineSpacingMm} mm"),
                    new CalcRow(z2, "Column Mid Spacing", $"{result.ColumnMidSpacingMm} mm"),
                    new CalcRow(z2, "Column Confine Length", result.ColumnConfineLength),

                    new CalcRow(z3, "Hook 90° Extension", $"{result.Hook90ExtMm} mm"),
                    new CalcRow(z3, "Hook 135° Extension", $"{result.Hook135ExtMm} mm"),
                    new CalcRow(z3, "Bend Radius", $"{result.BendRadiusMm} mm"),
                };

                var view = CollectionViewSource.GetDefaultView(rows);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
                UI_ResultGrid.ItemsSource = view;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Calculation Error");
            }
        }

        // --- Parsing Helpers ---
        private DesignCodeStandard ParseCode(ComboBox combo)
        {
            string text = (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (text == "ACI 318") return DesignCodeStandard.ACI318;
            if (text == "AS 3600") return DesignCodeStandard.AS3600;
            if (text == "Eurocode 2") return DesignCodeStandard.EC2;
            if (text == "NZS 3101") return DesignCodeStandard.NZS3101;
            return DesignCodeStandard.Custom;
        }

        private ConcreteGrade ParseGrade(ComboBox combo)
        {
            string text = (combo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "C30";
            return text switch
            {
                "C25" => ConcreteGrade.C25,
                "C30" => ConcreteGrade.C30,
                "C35" => ConcreteGrade.C35,
                "C40" => ConcreteGrade.C40,
                "C50" => ConcreteGrade.C50,
                _ => ConcreteGrade.C30
            };
        }

        private SteelGrade ParseSteel(ComboBox combo)
        {
            string text = (combo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "500E";
            return text == "300E" ? SteelGrade.Grade300E : SteelGrade.Grade500E;
        }

        private double ParseComboDouble(ComboBox combo, double fallback)
        {
            string text = (combo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            return double.TryParse(text, out double v) ? v : fallback;
        }

        private double ParseDouble(string text, double fallback)
        {
            return double.TryParse(text, out double v) ? v : fallback;
        }

        // --- Data Classes ---
        public class CompRow
        {
            public string Category { get; set; }
            public string Parameter { get; set; }
            public string ACI318 { get; set; }
            public string AS3600 { get; set; }
            public string EC2 { get; set; }
            public string NZS3101 { get; set; }
        }

        public class CalcRow
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public CalcRow(string category, string name, string value) { Category = category; Name = name; Value = value; }
        }

        // ── Additional Bar Diagram ──
        private void AdditionalBarDiagram_Loaded(object sender, RoutedEventArgs e)
        {
            var c = UI_AdditionalBarCanvas;
            if (c == null) return;
            c.Children.Clear();

            double W = c.ActualWidth > 10 ? c.ActualWidth : 780;
            double H = c.ActualHeight > 10 ? c.ActualHeight : 340;

            // Layout constants
            double margin = 50;
            double colW = 20;        // column width
            double beamH = 40;       // beam depth visual
            double beamTop = 110;    // Y of beam top face
            double beamBot = beamTop + beamH;

            // 3 columns at left, center, right
            double xLeft = margin;
            double xMid = W / 2.0;
            double xRight = W - margin;
            double spanL1 = xMid - xLeft;   // Span 1 length
            double spanL2 = xRight - xMid;  // Span 2 length

            // Colors
            var beamFill = new SolidColorBrush(Color.FromRgb(230, 235, 240));
            var beamStroke = new SolidColorBrush(Color.FromRgb(100, 120, 140));
            var colFill = new SolidColorBrush(Color.FromRgb(180, 190, 200));
            var t1Color = new SolidColorBrush(Color.FromRgb(44, 95, 138));    // dark blue
            var t2Color = new SolidColorBrush(Color.FromRgb(196, 50, 50));    // red
            var b1Color = new SolidColorBrush(Color.FromRgb(34, 139, 34));    // green
            var b2Color = new SolidColorBrush(Color.FromRgb(200, 120, 20));   // orange
            var dimColor = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            var zoneColor = new SolidColorBrush(Color.FromArgb(30, 196, 50, 50));
            var zoneBotColor = new SolidColorBrush(Color.FromArgb(30, 200, 120, 20));

            // ── DRAW COLUMNS (supports) ──
            double colTop = beamTop - 25;
            double colBot = beamBot + 50;
            DrawRect(c, xLeft - colW / 2, colTop, colW, colBot - colTop, colFill, beamStroke, 1);
            DrawRect(c, xMid - colW / 2, colTop, colW, colBot - colTop, colFill, beamStroke, 1);
            DrawRect(c, xRight - colW / 2, colTop, colW, colBot - colTop, colFill, beamStroke, 1);

            // ── DRAW BEAM OUTLINE ──
            DrawRect(c, xLeft - colW / 2, beamTop, xRight - xLeft + colW, beamH, beamFill, beamStroke, 1.5);

            // ── ZONE SHADING ──
            // T2 hogging zones (L/3 each side of middle support)
            double t2ZoneLeft = xMid - spanL1 / 3.0;
            double t2ZoneRight = xMid + spanL2 / 3.0;
            DrawRect(c, t2ZoneLeft, beamTop - 2, t2ZoneRight - t2ZoneLeft, beamH / 2 + 2, zoneColor, null, 0);

            // B2 sagging zones (0.1L offset from supports)
            double b2S1Left = xLeft + spanL1 * 0.1;
            double b2S1Right = xMid - spanL1 * 0.1;
            double b2S2Left = xMid + spanL2 * 0.1;
            double b2S2Right = xRight - spanL2 * 0.1;
            DrawRect(c, b2S1Left, beamBot - beamH / 2, b2S1Right - b2S1Left, beamH / 2 + 2, zoneBotColor, null, 0);
            DrawRect(c, b2S2Left, beamBot - beamH / 2, b2S2Right - b2S2Left, beamH / 2 + 2, zoneBotColor, null, 0);

            // ── REBAR BARS ──
            double barThick = 3.0;
            double topY1 = beamTop + 8;    // T1
            double topY2 = beamTop + 16;   // T2
            double botY1 = beamBot - 8;    // B1
            double botY2 = beamBot - 16;   // B2

            // T1 — continuous top bar (full length)
            DrawBar(c, xLeft, topY1, xRight, topY1, t1Color, barThick);

            // T2 — hogging bars over supports
            // Over left support: extends from beam start to L/3 into span 1
            double t2End1 = xLeft + spanL1 / 3.0;
            DrawBar(c, xLeft, topY2, t2End1, topY2, t2Color, barThick);
            // Over middle support: extends L/3 from each adjacent span
            double t2Start2 = xMid - spanL1 / 3.0;
            double t2End2 = xMid + spanL2 / 3.0;
            DrawBar(c, t2Start2, topY2, t2End2, topY2, t2Color, barThick);
            // Over right support: extends L/3 into span 2 to beam end
            double t2Start3 = xRight - spanL2 / 3.0;
            DrawBar(c, t2Start3, topY2, xRight, topY2, t2Color, barThick);

            // B1 — continuous bottom bar (full length)
            DrawBar(c, xLeft, botY1, xRight, botY1, b1Color, barThick);

            // B2 — sagging bars in each span (0.1L offset)
            DrawBar(c, b2S1Left, botY2, b2S1Right, botY2, b2Color, barThick);
            DrawBar(c, b2S2Left, botY2, b2S2Right, botY2, b2Color, barThick);

            // ── DIMENSION LINES ──
            double dimTopY = beamTop - 40;
            double dimBotY = beamBot + 30;

            // Span labels
            DrawDimension(c, xLeft, xMid, dimTopY - 20, "Span 1 (L₁)", dimColor, 12, true);
            DrawDimension(c, xMid, xRight, dimTopY - 20, "Span 2 (L₂)", dimColor, 12, true);

            // T2 dimension: L/3
            DrawDimension(c, t2Start2, xMid, dimTopY, "L₁/3", t2Color, 10, false);
            DrawDimension(c, xMid, t2End2, dimTopY, "L₂/3", t2Color, 10, false);

            // B2 dimension: 0.1L
            DrawDimension(c, xLeft, b2S1Left, dimBotY, "0.1L₁", b2Color, 10, false);
            DrawDimension(c, b2S1Right, xMid, dimBotY, "0.1L₁", b2Color, 10, false);
            DrawDimension(c, xMid, b2S2Left, dimBotY + 18, "0.1L₂", b2Color, 10, false);
            DrawDimension(c, b2S2Right, xRight, dimBotY + 18, "0.1L₂", b2Color, 10, false);

            // ── LEGEND ──
            double legX = margin;
            double legY = H - 55;
            DrawLegendItem(c, legX, legY, "T1 — Continuous Top", t1Color);
            DrawLegendItem(c, legX + 170, legY, "T2 — Hogging (over supports, L/3)", t2Color);
            DrawLegendItem(c, legX, legY + 22, "B1 — Continuous Bottom", b1Color);
            DrawLegendItem(c, legX + 170, legY + 22, "B2 — Sagging (mid-span, 0.1L offset)", b2Color);

            // Column labels
            DrawLabel(c, xLeft, colBot + 4, "Col", dimColor, 9, true);
            DrawLabel(c, xMid, colBot + 4, "Col", dimColor, 9, true);
            DrawLabel(c, xRight, colBot + 4, "Col", dimColor, 9, true);
        }

        // ── Drawing helpers ──

        private void DrawRect(Canvas c, double x, double y, double w, double h, Brush fill, Brush stroke, double strokeW)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = fill };
            if (stroke != null) { r.Stroke = stroke; r.StrokeThickness = strokeW; }
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }

        private void DrawBar(Canvas c, double x1, double y1, double x2, double y2, Brush color, double thickness)
        {
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = color, StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            c.Children.Add(line);
        }

        private void DrawDimension(Canvas c, double x1, double x2, double y, string text, Brush color, double fontSize, bool bold)
        {
            // Horizontal line with end ticks
            DrawBar(c, x1, y, x2, y, color, 1);
            DrawBar(c, x1, y - 4, x1, y + 4, color, 1);
            DrawBar(c, x2, y - 4, x2, y + 4, color, 1);

            var tb = new TextBlock
            {
                Text = text, FontSize = fontSize, Foreground = color,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tw = tb.DesiredSize.Width;
            Canvas.SetLeft(tb, (x1 + x2) / 2 - tw / 2);
            Canvas.SetTop(tb, y - fontSize - 4);
            c.Children.Add(tb);
        }

        private void DrawLegendItem(Canvas c, double x, double y, string text, Brush color)
        {
            // Color swatch
            DrawBar(c, x, y + 6, x + 20, y + 6, color, 3);
            var tb = new TextBlock { Text = text, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)) };
            Canvas.SetLeft(tb, x + 25);
            Canvas.SetTop(tb, y - 1);
            c.Children.Add(tb);
        }

        private void DrawLabel(Canvas c, double x, double y, string text, Brush color, double fontSize, bool center)
        {
            var tb = new TextBlock { Text = text, FontSize = fontSize, Foreground = color, FontWeight = FontWeights.SemiBold };
            if (center)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
            }
            else
            {
                Canvas.SetLeft(tb, x);
            }
            Canvas.SetTop(tb, y);
            c.Children.Add(tb);
        }
    }
}
