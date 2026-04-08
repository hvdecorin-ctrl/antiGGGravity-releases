using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;
using Microsoft.Win32;

namespace antiGGGravity.Views.General
{
    public partial class PrintView : Window, INotifyPropertyChanged
    {
        private Document _doc;
        private ObservableCollection<PrintSheetViewModel> _allSheets;
        private ObservableCollection<PrintSetViewModel> _printSets;
        private ObservableCollection<PrintSheetViewModel> _filteredSheets;
        private bool _canToggleSelection = true;

        public bool CanToggleSelection
        {
            get => _canToggleSelection;
            set
            {
                _canToggleSelection = value;
                OnPropertyChanged(nameof(CanToggleSelection));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public PrintView(Document doc)
        {
            // Load resources
            this.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/antiGGGravity;component/Resources/Pre_BrandStyles.xaml", UriKind.RelativeOrAbsolute) });
            
            InitializeComponent();
            _doc = doc;
            DataContext = this;

            LoadData();
        }

        private void LoadData()
        {
            // 1. Load Sheets
            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new PrintSheetViewModel(s))
                .ToList();

            _allSheets = new ObservableCollection<PrintSheetViewModel>(sheets);
            UI_List_Sheets.ItemsSource = _allSheets;

            // 2. Load Selection Sources (Sets + Schedules + Manual)
            LoadSelectionSources();

            // 7. Default Path & Load Settings
            LoadSettings();
        }

        private void LoadSelectionSources()
        {
            var sources = new List<PrintSourceViewModel>();

            // 1. Manual Option
            sources.Add(new PrintSourceViewModel { Type = SourceType.Manual, Name = "<Manual Selection>" });

            // 2. Print Sets
            var sets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .OrderBy(s => s.Name);
            
            foreach (var set in sets)
            {
                sources.Add(new PrintSourceViewModel { Type = SourceType.Set, Object = set, Name = $"Set: {set.Name}" });
            }

            // 3. Schedules
            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => s.Definition.CategoryId == new ElementId(BuiltInCategory.OST_Sheets) && !s.IsTemplate)
                .OrderBy(s => s.Name);

            foreach (var sch in schedules)
            {
                sources.Add(new PrintSourceViewModel { Type = SourceType.Schedule, Object = sch, Name = $"Schedule: {sch.Name}" });
            }

            UI_Combo_Source.ItemsSource = sources;
            UI_Combo_Source.SelectedIndex = 0;

            // Load Printers (Moved here since previous block was replaced)
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                UI_Combo_Printers.Items.Add(printer);
            }
            UI_Combo_Printers.SelectedItem = _doc.PrintManager.PrinterName;

            // Load Print Setups
            var setups = new FilteredElementCollector(_doc)
                .OfClass(typeof(PrintSetting))
                .Cast<PrintSetting>()
                .OrderBy(s => s.Name)
                .ToList();
            UI_Combo_Setups.ItemsSource = setups;
            UI_Combo_Setups.SelectedItem = _doc.PrintManager.PrintSetup.CurrentPrintSetting;

            // Load CAD Setups
            var cadSetups = new FilteredElementCollector(_doc)
                .OfClass(typeof(Autodesk.Revit.DB.ExportDWGSettings))
                .Cast<Autodesk.Revit.DB.ExportDWGSettings>()
                .OrderBy(s => s.Name)
                .ToList();
            UI_Combo_CadSetups.ItemsSource = cadSetups;
            if (cadSetups.Count > 0) UI_Combo_CadSetups.SelectedIndex = 0;
        }

        private void UI_Combo_Source_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_Combo_Source.SelectedItem is PrintSourceViewModel source)
            {
                switch (source.Type)
                {
                    case SourceType.Manual:
                        CanToggleSelection = true;
                        _filteredSheets = new ObservableCollection<PrintSheetViewModel>(_allSheets);
                        UI_Txt_Status.Text = "Mode: Manual Selection (All Sheets)";
                        break;

                    case SourceType.Set:
                        CanToggleSelection = false;
                        var set = source.Object as ViewSheetSet;
                        var setSheetIds = new HashSet<ElementId>();
                        foreach (View v in set.Views) if (v is ViewSheet) setSheetIds.Add(v.Id);

                        var setSheets = _allSheets.Where(vm => setSheetIds.Contains(vm.Sheet.Id)).ToList();
                        foreach (var vm in setSheets) vm.IsSelected = true;

                        _filteredSheets = new ObservableCollection<PrintSheetViewModel>(setSheets);
                        UI_Txt_Status.Text = $"Mode: Print Set - {set.Name} ({setSheets.Count} sheets)";
                        break;

                    case SourceType.Schedule:
                        CanToggleSelection = false; // Locked for schedules as well
                        var sch = source.Object as ViewSchedule;
                        var ordered = PrintLogic.OrderSheetsBySchedule(sch, _allSheets.Select(vm => vm.Sheet));
                        
                        // Preserving selection matches for the schedule
                        var schSheets = ordered.Select(s => new PrintSheetViewModel(s) { IsSelected = true }).ToList();

                        _filteredSheets = new ObservableCollection<PrintSheetViewModel>(schSheets);
                        UI_Txt_Status.Text = $"Mode: Schedule Sort - {sch.Name} ({schSheets.Count} sheets)";
                        break;
                }
                UI_List_Sheets.ItemsSource = _filteredSheets;
                UI_List_Sheets.Items.Refresh();
            }
        }

        private string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "antiGGGravity");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "print_settings.txt");
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new List<string>
                {
                    UI_Txt_Path.Text,
                    UI_Txt_Naming.Text,
                    UI_Check_Combine.IsChecked.ToString(),
                    UI_Check_Color.IsChecked.ToString(),
                    UI_Combo_CadSetups.SelectedIndex.ToString(),
                    "", // Reserved for CAD
                    UI_Combo_Quality.SelectedIndex.ToString()
                };
                File.WriteAllLines(GetSettingsPath(), settings);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length >= 1) UI_Txt_Path.Text = lines[0];
                    if (lines.Length >= 2) UI_Txt_Naming.Text = lines[1];
                    if (lines.Length >= 3) UI_Check_Combine.IsChecked = lines[2].ToLower() == "true";
                    if (lines.Length >= 4) UI_Check_Color.IsChecked = lines[3].ToLower() == "true";
                    if (lines.Length >= 5 && int.TryParse(lines[4], out int cadIndex)) UI_Combo_CadSetups.SelectedIndex = cadIndex;
                    if (lines.Length >= 6 && int.TryParse(lines[5], out int quantIndex)) UI_Combo_Quality.SelectedIndex = quantIndex;
                }
                else
                {
                    UI_Txt_Path.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "antiGG Print Output");
                }
            }
            catch
            {
                UI_Txt_Path.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "antiGG Print Output");
            }
        }

        private void UI_Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_Search.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filter))
            {
                UI_List_Sheets.ItemsSource = _allSheets;
            }
            else
            {
                var filtered = _allSheets.Where(s => s.NumberName.ToLower().Contains(filter)).ToList();
                UI_List_Sheets.ItemsSource = filtered;
            }
        }


        private void UI_Btn_Browse_Click(object sender, RoutedEventArgs e)
        {
#if REVIT2025_OR_GREATER || REVIT2026_OR_GREATER || REVIT2027_OR_GREATER
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                UI_Txt_Path.Text = dialog.FolderName;
            }
#else
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    UI_Txt_Path.Text = dialog.SelectedPath;
                }
            }
#endif
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UI_Check_All_Checked(object sender, RoutedEventArgs e)
        {
            if (_allSheets == null) return;
            foreach (var vm in _allSheets) vm.IsSelected = true;
            UI_List_Sheets.Items.Refresh();
        }

        private void UI_Check_All_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_allSheets == null) return;
            foreach (var vm in _allSheets) vm.IsSelected = false;
            UI_List_Sheets.Items.Refresh();
        }

        private async void UI_Btn_Print_Click(object sender, RoutedEventArgs e)
        {
            var selectedSheets = _allSheets.Where(vm => vm.IsSelected).ToList();
            if (selectedSheets.Count == 0)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Print", "Please select at least one sheet.");
                return;
            }

            string folder = UI_Txt_Path.Text;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string template = UI_Txt_Naming.Text;
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");

            UI_Btn_Print.IsEnabled = false;
            UI_Progress.Visibility = System.Windows.Visibility.Visible;
            UI_Progress.Maximum = selectedSheets.Count;
            UI_Progress.Value = 0;

            try
            {
                if (UI_Radio_PDF.IsChecked == true)
                {
                    Autodesk.Revit.DB.PDFExportOptions opts = new Autodesk.Revit.DB.PDFExportOptions
                    {
                        HideCropBoundaries = UI_Check_HideCrop.IsChecked == true,
                        HideReferencePlane = UI_Check_HideRef.IsChecked == true,
                        Combine = UI_Check_Combine.IsChecked == true,
                        // ExportQuality = Autodesk.Revit.DB.PDFExportQuality.DPI300,
                        StopOnError = false
                    };

                    // Apply Print Setup properties if selected
                    if (UI_Combo_Setups.SelectedItem is PrintSetting setup)
                    {
                        try 
                        {
                            var printParameters = setup.PrintParameters;
                            opts.ColorDepth = printParameters.ColorDepth;
                        }
                        catch { }

                        // 1. PDF Quality Mapping (Resolution)
                        switch (UI_Combo_Quality.SelectedIndex)
                        {
                            case 0: opts.RasterQuality = RasterQualityType.Low; break;
                            case 1: opts.RasterQuality = RasterQualityType.Medium; break;
                            case 2: opts.RasterQuality = RasterQualityType.High; break;
                            case 3: opts.RasterQuality = RasterQualityType.Presentation; break;
                            default: opts.RasterQuality = RasterQualityType.Medium; break;
                        }
                    }

                    // User Override for Color
                    opts.ColorDepth = (UI_Check_Color.IsChecked == true) ? ColorDepthType.Color : ColorDepthType.GrayScale;

                    int index = 1;

                    // If combined, we handle all at once
                    if (opts.Combine)
                    {
                        string combinedName = PrintLogic.ResolveName(template, selectedSheets[0].Sheet, 1, dateStr);
                        opts.FileName = combinedName;
                        UI_Txt_Status.Text = $"Exporting Combined PDF: {combinedName}.pdf...";
                        
                        var ids = selectedSheets.Select(s => s.Sheet.Id).ToList();
                        _doc.Export(folder, ids, opts);
                    }
                    else
                    {
                        foreach (var vm in selectedSheets)
                        {
                            string fileName = PrintLogic.ResolveName(template, vm.Sheet, index++, dateStr) + ".pdf";
                            UI_Txt_Status.Text = $"Exporting {fileName}...";
                            PrintLogic.ExportToPdf(vm.Sheet, folder, fileName, opts);
                            UI_Progress.Value++;
                            await System.Threading.Tasks.Task.Delay(10); // UI breathing room
                        }
                    }
                }
                else
                {
                    // DWG Implementation
                    DWGExportOptions dwgOpts = null;
                    if (UI_Combo_CadSetups.SelectedItem is Autodesk.Revit.DB.ExportDWGSettings cadSetup)
                    {
                        dwgOpts = cadSetup.GetDWGExportOptions();
                    }
                    else
                    {
                        dwgOpts = new DWGExportOptions();
                    }

                    // Force True Color and Merged (No Xref) as per earlier user request
                    dwgOpts.Colors = ExportColorMode.TrueColor;
                    try { dwgOpts.MergedViews = true; } catch { } 
                    
                    int index = 1;

                    // Safety Check: if name is static and multiple sheets selected, append index
                    bool appendIndex = selectedSheets.Count > 1 && !template.Contains("{") && !template.Contains("}");

                    foreach (var vm in selectedSheets)
                    {
                        string currentTemplate = appendIndex ? template + "_{index}" : template;
                        string fileName = PrintLogic.ResolveName(currentTemplate, vm.Sheet, index++, dateStr);
                        
                        UI_Txt_Status.Text = $"Exporting {fileName}.dwg...";
                        var ids = new List<ElementId> { vm.Sheet.Id };
                        _doc.Export(folder, fileName, ids, dwgOpts);
                        UI_Progress.Value++;
                        await System.Threading.Tasks.Task.Delay(10);
                    }
                }

                Autodesk.Revit.UI.TaskDialog.Show("antiGG Print", "Export completed successfully.");
                
                // Open the output folder
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                
                // Save Path Settings for next time
                SaveSettings();

                this.Close();
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Print Error", ex.Message);
            }
            finally
            {
                UI_Btn_Print.IsEnabled = true;
                UI_Progress.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
    }

    public enum SourceType { Manual, Set, Schedule }
    
    public class PrintSourceViewModel
    {
        public SourceType Type { get; set; }
        public object Object { get; set; }
        public string Name { get; set; }
    }

    public class PrintSetViewModel
    {
        public ViewSheetSet Set { get; }
        public string NameOverride { get; set; }
        public string Name => NameOverride ?? Set?.Name ?? "Unnamed Set";
        public List<ElementId> SheetIds { get; }

        public PrintSetViewModel(ViewSheetSet set)
        {
            Set = set;
            SheetIds = new List<ElementId>();
            if (Set != null)
            {
                foreach (View sheet in Set.Views)
                {
                    if (sheet is ViewSheet) SheetIds.Add(sheet.Id);
                }
            }
        }
    }

    public class PrintSheetViewModel : INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public string NumberName => $"{Sheet.SheetNumber} - {Sheet.Name}";
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        
        public string SheetSize 
        {
            get
            {
                var tblock = new FilteredElementCollector(Sheet.Document, Sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .Cast<FamilyInstance>()
                    .FirstOrDefault();

                if (tblock != null)
                {
                    var pWidth = tblock.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                    var pHeight = tblock.get_Parameter(BuiltInParameter.SHEET_HEIGHT);
                    if (pWidth != null && pHeight != null)
                    {
                        return $"{Math.Round(pWidth.AsDouble() * 304.8)} x {Math.Round(pHeight.AsDouble() * 304.8)} mm";
                    }
                }
                return "Unknown Size";
            }
        }

        public PrintSheetViewModel(ViewSheet sheet)
        {
            Sheet = sheet;
        }
    }
}
