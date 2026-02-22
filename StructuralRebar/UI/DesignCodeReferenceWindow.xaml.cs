using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

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

                // Build comparison rows: each row = { Parameter, ACI318, AS3600, EC2, NZS3101 }
                var rows = new List<Dictionary<string, string>>();

                AddRow(rows, "Tension Ld (mm)",
                    $"{results[0].TensionDevLengthMm}", $"{results[1].TensionDevLengthMm}",
                    $"{results[2].TensionDevLengthMm}", $"{results[3].TensionDevLengthMm}");

                AddRow(rows, "Compression Ld (mm)",
                    $"{results[0].CompressionDevLengthMm}", $"{results[1].CompressionDevLengthMm}",
                    $"{results[2].CompressionDevLengthMm}", $"{results[3].CompressionDevLengthMm}");

                AddRow(rows, "Dev. Multiplier (×db)",
                    $"{results[0].DevMultiplier}", $"{results[1].DevMultiplier}",
                    $"{results[2].DevMultiplier}", $"{results[3].DevMultiplier}");

                AddRow(rows, "Tension Lap (mm)",
                    $"{results[0].TensionLapMm}", $"{results[1].TensionLapMm}",
                    $"{results[2].TensionLapMm}", $"{results[3].TensionLapMm}");

                AddRow(rows, "Lap Multiplier (×db)",
                    $"{results[0].LapMultiplier}", $"{results[1].LapMultiplier}",
                    $"{results[2].LapMultiplier}", $"{results[3].LapMultiplier}");

                AddRow(rows, "Compression Lap (mm)",
                    $"{results[0].CompressionLapMm}", $"{results[1].CompressionLapMm}",
                    $"{results[2].CompressionLapMm}", $"{results[3].CompressionLapMm}");

                AddRow(rows, "Beam End Zone Length",
                    results[0].BeamEndZoneLength, results[1].BeamEndZoneLength,
                    results[2].BeamEndZoneLength, results[3].BeamEndZoneLength);

                AddRow(rows, "Beam End Zone Spacing (mm)",
                    $"{results[0].BeamEndZoneSpacingMm}", $"{results[1].BeamEndZoneSpacingMm}",
                    $"{results[2].BeamEndZoneSpacingMm}", $"{results[3].BeamEndZoneSpacingMm}");

                AddRow(rows, "Column Confine Spacing (mm)",
                    $"{results[0].ColumnConfineSpacingMm}", $"{results[1].ColumnConfineSpacingMm}",
                    $"{results[2].ColumnConfineSpacingMm}", $"{results[3].ColumnConfineSpacingMm}");

                AddRow(rows, "Column Mid Spacing (mm)",
                    $"{results[0].ColumnMidSpacingMm}", $"{results[1].ColumnMidSpacingMm}",
                    $"{results[2].ColumnMidSpacingMm}", $"{results[3].ColumnMidSpacingMm}");

                AddRow(rows, "Hook 90° Extension (mm)",
                    $"{results[0].Hook90ExtMm}", $"{results[1].Hook90ExtMm}",
                    $"{results[2].Hook90ExtMm}", $"{results[3].Hook90ExtMm}");

                AddRow(rows, "Hook 135° Extension (mm)",
                    $"{results[0].Hook135ExtMm}", $"{results[1].Hook135ExtMm}",
                    $"{results[2].Hook135ExtMm}", $"{results[3].Hook135ExtMm}");

                AddRow(rows, "Bend Radius (mm)",
                    $"{results[0].BendRadiusMm}", $"{results[1].BendRadiusMm}",
                    $"{results[2].BendRadiusMm}", $"{results[3].BendRadiusMm}");

                // Bind to datagrid using anonymous objects
                var displayRows = new List<CompRow>();
                foreach (var r in rows)
                {
                    displayRows.Add(new CompRow
                    {
                        Parameter = r["Parameter"],
                        ACI318 = r["ACI318"],
                        AS3600 = r["AS3600"],
                        EC2 = r["EC2"],
                        NZS3101 = r["NZS3101"]
                    });
                }

                UI_ComparisonGrid.ItemsSource = displayRows;
            }
            catch { }
        }

        private void AddRow(List<Dictionary<string, string>> rows, string param, string aci, string as3600, string ec2, string nzs)
        {
            rows.Add(new Dictionary<string, string>
            {
                { "Parameter", param },
                { "ACI318", aci },
                { "AS3600", as3600 },
                { "EC2", ec2 },
                { "NZS3101", nzs }
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

                var rows = new List<CalcRow>
                {
                    new CalcRow("Tension Dev. Length Ld", $"{result.TensionDevLengthMm} mm"),
                    new CalcRow("Dev. Multiplier", $"{result.DevMultiplier} × db"),
                    new CalcRow("Compression Dev. Length", $"{result.CompressionDevLengthMm} mm"),
                    new CalcRow("Tension Lap Length", $"{result.TensionLapMm} mm"),
                    new CalcRow("Lap Multiplier", $"{result.LapMultiplier} × db"),
                    new CalcRow("Compression Lap Length", $"{result.CompressionLapMm} mm"),
                    new CalcRow("Beam End Zone Spacing", $"{result.BeamEndZoneSpacingMm} mm"),
                    new CalcRow("Beam End Zone Length", result.BeamEndZoneLength),
                    new CalcRow("Column Confine Spacing", $"{result.ColumnConfineSpacingMm} mm"),
                    new CalcRow("Column Mid Spacing", $"{result.ColumnMidSpacingMm} mm"),
                    new CalcRow("Column Confine Length", result.ColumnConfineLength),
                    new CalcRow("Hook 90° Extension", $"{result.Hook90ExtMm} mm"),
                    new CalcRow("Hook 135° Extension", $"{result.Hook135ExtMm} mm"),
                    new CalcRow("Bend Radius", $"{result.BendRadiusMm} mm"),
                };

                UI_ResultGrid.ItemsSource = rows;
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
            public string Parameter { get; set; }
            public string ACI318 { get; set; }
            public string AS3600 { get; set; }
            public string EC2 { get; set; }
            public string NZS3101 { get; set; }
        }

        public class CalcRow
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public CalcRow(string name, string value) { Name = name; Value = value; }
        }
    }
}
