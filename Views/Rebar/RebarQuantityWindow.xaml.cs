using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
// Note: Do NOT import System.Windows.Data — 'Binding' conflicts with Autodesk.Revit.DB.Binding
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.Rebar;

namespace antiGGGravity.Views.Rebar
{
    public partial class RebarQuantityWindow : Window
    {
        private readonly ExternalEvent _refreshEvent;
        private readonly RebarQuantityRefreshHandler _refreshHandler;
        private RebarQuantityResult _currentResult;

        public RebarQuantityWindow(RebarQuantityResult initialResult, 
                                    ExternalEvent refreshEvent,
                                    RebarQuantityRefreshHandler refreshHandler)
        {
            InitializeComponent();
            _refreshEvent = refreshEvent;
            _refreshHandler = refreshHandler;
            _refreshHandler.SetWindow(this);

            LoadResult(initialResult);
        }

        /// <summary>
        /// Load a RebarQuantityResult into the DataGrid. Called from UI thread.
        /// </summary>
        public void LoadResult(RebarQuantityResult result)
        {
            _currentResult = result;

            if (result == null || result.Diameters.Count == 0)
            {
                QtyGrid.ItemsSource = null;
                QtyGrid.Columns.Clear();
                StatusText.Text = "No rebar found in the document.";
                return;
            }

            // Build a DataTable for display
            var table = new DataTable();

            // Column 1: Host category
            table.Columns.Add("HostCategory", typeof(string));

            // Dynamic diameter columns
            foreach (int dia in result.Diameters)
            {
                table.Columns.Add($"{dia} mm", typeof(string));
            }

            // Last column: Weight (per Item)
            table.Columns.Add("Weight (per Item)", typeof(string));

            // --- Data rows ---
            foreach (var row in result.Rows)
            {
                var dr = table.NewRow();
                dr["HostCategory"] = row.HostCategory;

                foreach (int dia in result.Diameters)
                {
                    if (row.DiameterData.ContainsKey(dia))
                        dr[$"{dia} mm"] = row.DiameterData[dia].TotalLengthM.ToString("N1");
                    else
                        dr[$"{dia} mm"] = "—";
                }

                dr["Weight (per Item)"] = row.RowTotalWeightKg.ToString("N1");
                table.Rows.Add(dr);
            }

            // --- Separator row ---
            var sepRow = table.NewRow();
            sepRow["HostCategory"] = "";
            table.Rows.Add(sepRow);

            // --- Total Length (m) row ---
            var lenRow = table.NewRow();
            lenRow["HostCategory"] = "Total Length (m)";
            foreach (int dia in result.Diameters)
            {
                lenRow[$"{dia} mm"] = result.TotalLengthPerDia.ContainsKey(dia)
                    ? result.TotalLengthPerDia[dia].ToString("N1")
                    : "—";
            }
            lenRow["Weight (per Item)"] = "";
            table.Rows.Add(lenRow);

            // --- Total Weight (kg) row ---
            var wtRow = table.NewRow();
            wtRow["HostCategory"] = "Total Weight (kg)";
            foreach (int dia in result.Diameters)
            {
                wtRow[$"{dia} mm"] = result.TotalWeightPerDia.ContainsKey(dia)
                    ? result.TotalWeightPerDia[dia].ToString("N1")
                    : "—";
            }
            wtRow["Weight (per Item)"] = result.GrandTotalWeightKg.ToString("N1");
            table.Rows.Add(wtRow);

            // --- Bind to DataGrid ---
            QtyGrid.Columns.Clear();
            QtyGrid.AutoGenerateColumns = false;

            // Build columns
            // First column — left aligned, bold
            var hostCol = new DataGridTextColumn
            {
                Header = "Host \\ Dia",
                Binding = new System.Windows.Data.Binding("[HostCategory]"),
                Width = new DataGridLength(150),
                FontWeight = FontWeights.SemiBold
            };
            QtyGrid.Columns.Add(hostCol);

            // Diameter columns — right aligned
            foreach (int dia in result.Diameters)
            {
                string colName = $"{dia} mm";
                var col = new DataGridTextColumn
                {
                    Header = colName,
                    Binding = new System.Windows.Data.Binding($"[{colName}]"),
                    Width = new DataGridLength(80),
                };
                // Right-align via element style
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.ElementStyle = style;
                QtyGrid.Columns.Add(col);
            }

            // Total weight column
            var totalCol = new DataGridTextColumn
            {
                Header = "Weight (per Item)",
                Binding = new System.Windows.Data.Binding("[Weight (per Item)]"),
                Width = new DataGridLength(140), // Slightly wider for new header
                FontWeight = FontWeights.Bold
            };
            var totalStyle = new Style(typeof(TextBlock));
            totalStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            totalCol.ElementStyle = totalStyle;
            QtyGrid.Columns.Add(totalCol);

            QtyGrid.ItemsSource = table.DefaultView;

            int rebarCount = result.Rows.Sum(r => r.DiameterData.Values.Count);
            StatusText.Text = $"{result.Rows.Count} host categories  •  {result.Diameters.Count} bar sizes  •  Total: {result.GrandTotalWeightKg:N1} kg";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Refreshing...";
            _refreshEvent?.Raise();
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null) return;

            var sb = new StringBuilder();

            // Header line
            sb.Append("Host \\ Dia");
            foreach (int dia in _currentResult.Diameters)
                sb.Append($"\t{dia} mm");
            sb.AppendLine("\tTotal Weight (kg)");

            // Data rows
            foreach (var row in _currentResult.Rows)
            {
                sb.Append(row.HostCategory);
                foreach (int dia in _currentResult.Diameters)
                {
                    if (row.DiameterData.ContainsKey(dia))
                        sb.Append($"\t{row.DiameterData[dia].TotalLengthM:N1}");
                    else
                        sb.Append("\t0.0");
                }
                sb.AppendLine($"\t{row.RowTotalWeightKg:N1}");
            }

            // Separator
            sb.AppendLine();

            // Total Length
            sb.Append("Total Length (m)");
            foreach (int dia in _currentResult.Diameters)
            {
                double val = _currentResult.TotalLengthPerDia.ContainsKey(dia) ? _currentResult.TotalLengthPerDia[dia] : 0;
                sb.Append($"\t{val:N1}");
            }
            sb.AppendLine();

            // Total Weight
            sb.Append("Total Weight (kg)");
            foreach (int dia in _currentResult.Diameters)
            {
                double val = _currentResult.TotalWeightPerDia.ContainsKey(dia) ? _currentResult.TotalWeightPerDia[dia] : 0;
                sb.Append($"\t{val:N1}");
            }
            sb.AppendLine($"\t{_currentResult.GrandTotalWeightKg:N1}");

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusText.Text = "Copied to clipboard! Paste into Excel.";
            }
            catch
            {
                StatusText.Text = "Failed to copy to clipboard.";
            }
        }

        private void CalcInput_Changed(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return; // Prevent calc during initial window load

            if (double.TryParse(InputDiameter?.Text, out double diaMm) &&
                double.TryParse(InputLength?.Text, out double lengthM))
            {
                // Unit weight formula: d² / 162.2
                // (Note: RebarQuantityService uses precise (π/4)*d²*7850/1e6, but standard field formula is d²/162.2.
                // We will use the service's logic to guarantee perfect matching, although they are ~99.9% identical).
                double unitWt = Math.PI / 4.0 * diaMm * diaMm * 7850.0 / 1e6;
                double totalWt = lengthM * unitWt;

                ResultUnitWeight.Text = unitWt.ToString("N3"); // Show 3 decimals for unit weight
                ResultTotalWeight.Text = totalWt.ToString("N1"); // Show 1 for total
            }
            else
            {
                ResultUnitWeight.Text = "---";
                ResultTotalWeight.Text = "---";
            }
        }
    }
}
