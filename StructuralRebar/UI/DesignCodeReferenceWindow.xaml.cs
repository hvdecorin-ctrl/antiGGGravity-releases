using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    }
}
