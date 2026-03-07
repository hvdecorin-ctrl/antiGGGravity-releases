using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.Rebar;

namespace antiGGGravity.Views.Rebar
{
    public partial class RebarHostMarkWindow : Window
    {
        private readonly ExternalEvent _refreshEvent;
        private readonly RebarHostMarkRefreshHandler _refreshHandler;
        private RebarQuantityResult _currentResult;

        public RebarHostMarkWindow(RebarQuantityResult initialResult, 
                                    ExternalEvent refreshEvent,
                                    RebarHostMarkRefreshHandler refreshHandler)
        {
            InitializeComponent();
            _refreshEvent = refreshEvent;
            _refreshHandler = refreshHandler;
            _refreshHandler.SetWindow(this);

            LoadResult(initialResult);
        }

        public void LoadResult(RebarQuantityResult result)
        {
            _currentResult = result;

            if (result == null || result.Diameters.Count == 0)
            {
                QtyGrid.ItemsSource = null;
                QtyGrid.Columns.Clear();
                StatusText.Text = "No rebar found in the document or selection.";
                return;
            }

            var table = new DataTable();

            table.Columns.Add("HostMark", typeof(string));

            foreach (int dia in result.Diameters)
            {
                table.Columns.Add($"{dia} mm", typeof(string));
            }

            table.Columns.Add("Weight (per Item)", typeof(string));

            foreach (var row in result.Rows)
            {
                var dr = table.NewRow();
                dr["HostMark"] = row.HostCategory; // Repurposed for Host Mark

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

            var sepRow = table.NewRow();
            sepRow["HostMark"] = "";
            table.Rows.Add(sepRow);

            var lenRow = table.NewRow();
            lenRow["HostMark"] = "Total Length (m)";
            foreach (int dia in result.Diameters)
            {
                lenRow[$"{dia} mm"] = result.TotalLengthPerDia.ContainsKey(dia)
                    ? result.TotalLengthPerDia[dia].ToString("N1")
                    : "—";
            }
            lenRow["Weight (per Item)"] = "";
            table.Rows.Add(lenRow);

            var wtRow = table.NewRow();
            wtRow["HostMark"] = "Total Weight (kg)";
            foreach (int dia in result.Diameters)
            {
                wtRow[$"{dia} mm"] = result.TotalWeightPerDia.ContainsKey(dia)
                    ? result.TotalWeightPerDia[dia].ToString("N1")
                    : "—";
            }
            wtRow["Weight (per Item)"] = result.GrandTotalWeightKg.ToString("N1");
            table.Rows.Add(wtRow);

            QtyGrid.Columns.Clear();
            QtyGrid.AutoGenerateColumns = false;

            var hostCol = new DataGridTextColumn
            {
                Header = "Host Mark \\ Dia",
                Binding = new System.Windows.Data.Binding("[HostMark]"),
                Width = new DataGridLength(150),
                FontWeight = FontWeights.SemiBold
            };
            QtyGrid.Columns.Add(hostCol);

            foreach (int dia in result.Diameters)
            {
                string colName = $"{dia} mm";
                var col = new DataGridTextColumn
                {
                    Header = colName,
                    Binding = new System.Windows.Data.Binding($"[{colName}]"),
                    Width = new DataGridLength(80),
                };
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.ElementStyle = style;
                QtyGrid.Columns.Add(col);
            }

            var totalCol = new DataGridTextColumn
            {
                Header = "Weight (per Item)",
                Binding = new System.Windows.Data.Binding("[Weight (per Item)]"),
                Width = new DataGridLength(140),
                FontWeight = FontWeights.Bold
            };
            var totalStyle = new Style(typeof(TextBlock));
            totalStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            totalCol.ElementStyle = totalStyle;
            QtyGrid.Columns.Add(totalCol);

            QtyGrid.ItemsSource = table.DefaultView;

            StatusText.Text = $"{result.Rows.Count} host marks  •  {result.Diameters.Count} bar sizes  •  Total: {result.GrandTotalWeightKg:N1} kg";
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

            sb.Append("Host Mark \\ Dia");
            foreach (int dia in _currentResult.Diameters)
                sb.Append($"\t{dia} mm");
            sb.AppendLine("\tTotal Weight (kg)");

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

            sb.AppendLine();

            sb.Append("Total Length (m)");
            foreach (int dia in _currentResult.Diameters)
            {
                double val = _currentResult.TotalLengthPerDia.ContainsKey(dia) ? _currentResult.TotalLengthPerDia[dia] : 0;
                sb.Append($"\t{val:N1}");
            }
            sb.AppendLine();

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

        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV (Comma delimited)|*.csv",
                    Title = "Export to CSV",
                    FileName = "RebarHostMarkQuantity.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusText.Text = "Exporting to CSV...";
                    var sb = new StringBuilder();

                    // Header line
                    sb.Append("Host Mark \\ Dia,");
                    foreach (int dia in _currentResult.Diameters)
                    {
                        sb.Append($"{dia} mm,");
                    }
                    sb.AppendLine("Total Weight (kg)");

                    // Data rows
                    foreach (var r in _currentResult.Rows)
                    {
                        string host = r.HostCategory.Contains(",") ? $"\"{r.HostCategory}\"" : r.HostCategory;
                        sb.Append($"{host},");
                        foreach (int dia in _currentResult.Diameters)
                        {
                            if (r.DiameterData.ContainsKey(dia))
                                sb.Append($"{Math.Round(r.DiameterData[dia].TotalLengthM, 1)},");
                            else
                                sb.Append("0,");
                        }
                        sb.AppendLine($"{Math.Round(r.RowTotalWeightKg, 1)}");
                    }

                    sb.AppendLine(); 

                    // Total Length
                    sb.Append("Total Length (m),");
                    foreach (int dia in _currentResult.Diameters)
                    {
                        double val = _currentResult.TotalLengthPerDia.ContainsKey(dia) ? _currentResult.TotalLengthPerDia[dia] : 0;
                        sb.Append($"{Math.Round(val, 1)},");
                    }
                    sb.AppendLine();

                    // Total Weight
                    sb.Append("Total Weight (kg),");
                    foreach (int dia in _currentResult.Diameters)
                    {
                        double val = _currentResult.TotalWeightPerDia.ContainsKey(dia) ? _currentResult.TotalWeightPerDia[dia] : 0;
                        sb.Append($"{Math.Round(val, 1)},");
                    }
                    sb.AppendLine($"{Math.Round(_currentResult.GrandTotalWeightKg, 1)}");

                    System.IO.File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                    
                    StatusText.Text = "Export to CSV complete.";
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{dialog.FileName}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to export CSV: " + ex.Message;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (QtyGrid.ItemsSource is DataView view)
            {
                string filterText = SearchBox.Text.Trim().Replace("'", "''");
                if (string.IsNullOrEmpty(filterText))
                {
                    view.RowFilter = "";
                }
                else
                {
                    // Filter by HostMark, but keep summary rows (which usually have empty or specific text)
                    // Or we can just filter by HostMark and accept that totals might disappear if they don't match
                    // Usually better to keep totals if possible, but RowFilter 
                    // applies to all rows in the view.
                    view.RowFilter = $"[HostMark] LIKE '%{filterText}%' OR [HostMark] = '' OR [HostMark] = 'Total Length (m)' OR [HostMark] = 'Total Weight (kg)'";
                }
                
                // Update status with visible count
                if (_currentResult != null)
                {
                    int visibleRows = view.Count;
                    // Subtracting 3 for the summary rows (separator, Total Length, Total Weight) if they exist
                    int dataRows = Math.Max(0, visibleRows - 3);
                    StatusText.Text = $"Filtered: {dataRows} of {_currentResult.Rows.Count} host marks  •  Total: {_currentResult.GrandTotalWeightKg:N1} kg";
                }
            }
        }

        private void CalcInput_Changed(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return; 

            if (double.TryParse(InputDiameter?.Text, out double diaMm) &&
                double.TryParse(InputLength?.Text, out double lengthM))
            {
                double unitWt = Math.PI / 4.0 * diaMm * diaMm * 7850.0 / 1e6;
                double totalWt = lengthM * unitWt;

                ResultUnitWeight.Text = unitWt.ToString("N3"); 
                ResultTotalWeight.Text = totalWt.ToString("N1"); 
            }
            else
            {
                ResultUnitWeight.Text = "---";
                ResultTotalWeight.Text = "---";
            }
        }
    }
}
