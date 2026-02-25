using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class FamilyDuplicatorView : Window
    {
        private readonly Document _doc;
        private readonly ExternalEvent _dupEvent;
        private readonly FamilyDuplicationHandler _handler;
        // Family Loading events injected from Command
        private readonly ExternalEvent _loadEvent;
        private readonly Commands.ProjectAudit.LoadFamilyTypesHandler _loadHandler;
        private readonly ExternalEvent _symbolsEvent;
        private readonly Commands.ProjectAudit.GetSymbolsHandler _symbolsHandler;

        // Modeless reference for FamilyLoadingView
        private static FamilyLoadingView _loadingView;

        public ObservableCollection<DuplicationRow> Rows { get; set; } = new ObservableCollection<DuplicationRow>();

        // ============================================================
        // TYPE CACHING â€” matches Python's category_types_cache
        // ============================================================
        private static readonly Dictionary<string, BuiltInCategory> SupportedCategories = new Dictionary<string, BuiltInCategory>
        {
            { "Foundations", BuiltInCategory.OST_StructuralFoundation },
            { "Walls", BuiltInCategory.OST_Walls },
            { "Floors", BuiltInCategory.OST_Floors },
            { "Columns", BuiltInCategory.OST_StructuralColumns },
            { "Framings", BuiltInCategory.OST_StructuralFraming }
        };

        private Dictionary<string, List<string>> _categoryTypesCache = new Dictionary<string, List<string>>();
        private List<string> _allBaseTypes = new List<string>();

        public FamilyDuplicatorView(Document doc, ExternalEvent dupEvent, FamilyDuplicationHandler handler,
            ExternalEvent loadEvent, Commands.ProjectAudit.LoadFamilyTypesHandler loadHandler,
            ExternalEvent symbolsEvent, Commands.ProjectAudit.GetSymbolsHandler symbolsHandler)
        {
            InitializeComponent();
            _doc = doc;
            _dupEvent = dupEvent;
            _handler = handler;

            _loadEvent = loadEvent;
            _loadHandler = loadHandler;
            _symbolsEvent = symbolsEvent;
            _symbolsHandler = symbolsHandler;

            PreloadCategories();
            UI_Grid.ItemsSource = Rows;

            // Keyboard shortcuts
            UI_Grid.PreviewKeyDown += UI_Grid_PreviewKeyDown;
        }

        // ============================================================
        // TYPE LOADING & CACHING
        // ============================================================
        private void PreloadCategories()
        {
            _categoryTypesCache.Clear();
            foreach (var kvp in SupportedCategories)
            {
                _categoryTypesCache[kvp.Key] = GetTypeNamesForCategory(kvp.Value);
            }
            RefreshAllBaseTypes();
        }

        private List<string> GetTypeNamesForCategory(BuiltInCategory cat)
        {
            var types = new List<string>();
            try
            {
                var collector = new FilteredElementCollector(_doc)
                    .OfCategory(cat)
                    .WhereElementIsElementType();

                foreach (Element el in collector)
                {
                    string name = el.Name;
                    string familyName = "";
                    if (el is FamilySymbol fs)
                        familyName = fs.Family?.Name ?? "";

                    string fullName = string.IsNullOrEmpty(familyName) ? name : $"{familyName}:{name}";
                    types.Add(fullName);
                }
            }
            catch { }
            return types.Distinct().OrderBy(t => t).ToList();
        }

        private void RefreshAllBaseTypes()
        {
            _allBaseTypes = _categoryTypesCache.Values
                .SelectMany(t => t)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Refresh all existing rows' filters
            foreach (var row in Rows) row.UpdateFilteredTypes(_allBaseTypes);
        }

        /// <summary>
        /// Infer which category a BaseType belongs to (matches Python's category inference).
        /// </summary>
        private string InferCategory(string baseType)
        {
            foreach (var kvp in _categoryTypesCache)
            {
                if (kvp.Value.Contains(baseType))
                    return kvp.Key;
            }
            return null;
        }

        // ============================================================
        // KEYBOARD SHORTCUTS
        // ============================================================
        private void UI_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If the user is actively typing in a cell (TextBox or ComboBox), 
            // allow standard text entry and standard copy/paste to work.
            if (e.OriginalSource is System.Windows.Controls.TextBox || e.OriginalSource is System.Windows.Controls.ComboBox)
            {
                // But still intercept Enter to commit and move down
                if (e.Key == Key.Enter)
                {
                    UI_Grid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                return;
            }

            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                PasteFromClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                CopySelectedCells();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && UI_Grid.SelectedItems.Count > 0 && !UI_Grid.IsEditing())
            {
                DeleteSelectedRows();
                e.Handled = true;
            }
        }

        // ============================================================
        // CLIPBOARD â€” PASTE with fuzzy BaseType matching (matches Python)
        // ============================================================
        private void PasteFromClipboard()
        {
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text)) return;

                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                // Get the starting index. If nothing selected, start at the end.
                int startIndex = UI_Grid.SelectedIndex;
                if (startIndex < 0) startIndex = Rows.Count;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    string[] parts = line.Split('\t');
                    int targetIndex = startIndex + i;
                    
                    DuplicationRow row;
                    if (targetIndex < Rows.Count)
                    {
                        // Overwrite existing row
                        row = Rows[targetIndex];
                    }
                    else
                    {
                        // Add new row at the bottom
                        row = new DuplicationRow();
                        row.UpdateFilteredTypes(_allBaseTypes);
                        Rows.Add(row);
                    }

                    if (parts.Length > 0) row.TypeMark = parts[0].Trim();
                    if (parts.Length > 1) row.TypeComment = parts[1].Trim();
                    if (parts.Length > 2) row.Description = parts[2].Trim();
                    if (parts.Length > 3)
                    {
                        string val = parts[3].Trim();
                        row.BaseType = FuzzyMatchBaseType(val);
                    }
                }
                UI_Status.Text = $"Pasted {lines.Length} rows.";
            }
            catch (Exception ex)
            {
                UI_Status.Text = $"Paste error: {ex.Message}";
            }
        }

        /// <summary>
        /// Fuzzy matching for BaseType paste â€” exact, case-insensitive, then partial.
        /// Matches Python's paste logic.
        /// </summary>
        private string FuzzyMatchBaseType(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            // Exact match
            if (_allBaseTypes.Contains(value)) return value;

            // Case-insensitive match
            var match = _allBaseTypes.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // Partial match (value contained in type name)
            match = _allBaseTypes.FirstOrDefault(t => t.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
            return match; // may be null
        }

        private void CopySelectedCells()
        {
            var selectedItems = UI_Grid.SelectedItems.Cast<DuplicationRow>().ToList();
            if (!selectedItems.Any()) return;

            var lines = selectedItems.Select(r => $"{r.TypeMark}\t{r.TypeComment}\t{r.Description}\t{r.BaseType}");
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }

        // ============================================================
        // CELL EDIT EVENTS â€” auto-copy TypeCommentâ†’Description, filter combo
        // ============================================================
        private void UI_Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;

            // Column index 1 = TypeComment
            if (e.Column == UI_Grid.Columns[1])
            {
                var row = e.Row.Item as DuplicationRow;
                if (row != null)
                {
                    var textBox = e.EditingElement as System.Windows.Controls.TextBox;
                    string newValue = textBox?.Text?.Trim() ?? "";

                    // Auto-copy to Description if Description is empty (matches Python)
                    if (string.IsNullOrEmpty(row.Description))
                    {
                        row.Description = newValue;
                    }

                    // Pre-filter the Base Types list for this row
                    row.UpdateFilteredTypes(_allBaseTypes);
                }
            }
        }

        private void UI_BaseTypeCombo_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as System.Windows.Controls.ComboBox;
            if (combo == null) return;
            
            // Auto-focus and open dropdown for better UX
            combo.Focus();
            combo.IsDropDownOpen = true;
        }

        /// <summary>
        /// Logic moved to DuplicationRow.UpdateFilteredTypes for better performance.
        /// </summary>
        private void UI_Grid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            // Redundant with new TemplateColumn binding approach
        }

        // ============================================================
        // BUTTON HANDLERS
        // ============================================================
        private void UI_Btn_AddRow_Click(object sender, RoutedEventArgs e)
        {
            var row = new DuplicationRow();
            row.UpdateFilteredTypes(_allBaseTypes);
            Rows.Add(row);
        }

        private void UI_Btn_DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedRows();
        }

        private void DeleteSelectedRows()
        {
            var selected = UI_Grid.SelectedItems.Cast<DuplicationRow>().ToList();
            if (selected.Any())
            {
                foreach (var item in selected) Rows.Remove(item);
            }
            else if (UI_Grid.CurrentItem is DuplicationRow current)
            {
                Rows.Remove(current);
            }
        }

        private void UI_Btn_Clear_Click(object sender, RoutedEventArgs e)
        {
            Rows.Clear();
        }

        /// <summary>
        /// Opens the FamilyLoadingView so the user can selectively load specific types from .rfa files.
        /// </summary>
        private void UI_Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Modeless implementation: bring to front if already open
                if (_loadingView != null && _loadingView.IsVisible)
                {
                    _loadingView.Focus();
                    return;
                }

                // Create and show modeless form using injected events
                _loadingView = new FamilyLoadingView(_loadEvent, _loadHandler, _symbolsEvent, _symbolsHandler);
                _loadingView.Owner = this;

                // Populate folder dropdown with saved paths
                _loadingView.PopulateFolderDropdown();

                _loadingView.Show();
                
                UI_Status.Text = "Family Loading tools opened. 'Refresh' when finished loading.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Family Loading tool: {ex.Message}", "Error");
            }
        }

        private void UI_Btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            PreloadCategories();
            UI_Status.Text = "Base type list refreshed.";
        }

        private void UI_Btn_Create_Click(object sender, RoutedEventArgs e)
        {
            var validRows = Rows.Where(r => !string.IsNullOrEmpty(r.TypeMark) && !string.IsNullOrEmpty(r.BaseType)).ToList();
            if (!validRows.Any())
            {
                UI_Status.Text = "No valid rows (Type Mark and Base Type are required).";
                return;
            }

            _handler.RowsToProcess = validRows;
            _handler.CategoryTypesCache = _categoryTypesCache;
            _dupEvent.Raise();
            UI_Status.Text = "Duplication started...";
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ============================================================
        // CONTEXT MENU HANDLERS
        // ============================================================
        private void UI_Menu_Copy_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedCells();
        }

        private void UI_Menu_Paste_Click(object sender, RoutedEventArgs e)
        {
            PasteFromClipboard();
        }

        private void UI_Menu_DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedRows();
        }
    }

    // ================================================================
    // DATA MODEL
    // ================================================================
    public class DuplicationRow : System.ComponentModel.INotifyPropertyChanged
    {
        private string _typeMark;
        private string _typeComment;
        private string _description;
        private string _baseType;
        private List<string> _filteredBaseTypes = new List<string>();

        public string TypeMark
        {
            get => _typeMark;
            set { _typeMark = value; OnPropertyChanged(nameof(TypeMark)); }
        }
        public string TypeComment
        {
            get => _typeComment;
            set { _typeComment = value; OnPropertyChanged(nameof(TypeComment)); }
        }
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }
        public string BaseType
        {
            get => _baseType;
            set { _baseType = value; OnPropertyChanged(nameof(BaseType)); }
        }

        public List<string> FilteredBaseTypes
        {
            get => _filteredBaseTypes;
            set { _filteredBaseTypes = value; OnPropertyChanged(nameof(FilteredBaseTypes)); }
        }

        public void UpdateFilteredTypes(IEnumerable<string> allTypes)
        {
            string filter = (TypeComment ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                FilteredBaseTypes = allTypes.ToList();
                return;
            }

            // Primary filter: contains the filter text
            var filtered = allTypes.Where(t => t.ToLower().Contains(filter)).ToList();

            // Fallback: any word (3+ chars) from the filter matches
            if (!filtered.Any())
            {
                var words = filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                filtered = allTypes.Where(t => words.Any(w => w.Length >= 3 && t.ToLower().Contains(w))).ToList();
            }

            // Union with current selection to ensure it's always in the list
            if (!string.IsNullOrEmpty(BaseType) && !filtered.Contains(BaseType))
            {
                filtered.Add(BaseType);
            }

            FilteredBaseTypes = filtered.OrderBy(t => t).ToList();

            // Auto-select if empty and we have a strong candidate
            if (string.IsNullOrEmpty(BaseType) && filtered.Any())
            {
                // If there's an exact match in the name, pick it
                var exact = filtered.FirstOrDefault(t => string.Equals(t, filter, StringComparison.OrdinalIgnoreCase));
                if (exact != null) BaseType = exact;
                else if (filtered.Count == 1) BaseType = filtered.First();
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    // ================================================================
    // DUPLICATION HANDLER â€” with duplicate check (matches Python)
    // ================================================================
    public class FamilyDuplicationHandler : IExternalEventHandler
    {
        public List<DuplicationRow> RowsToProcess { get; set; } = new List<DuplicationRow>();
        public Dictionary<string, List<string>> CategoryTypesCache { get; set; }

        private static readonly Dictionary<string, BuiltInCategory> Categories = new Dictionary<string, BuiltInCategory>
        {
            { "Foundations", BuiltInCategory.OST_StructuralFoundation },
            { "Walls", BuiltInCategory.OST_Walls },
            { "Floors", BuiltInCategory.OST_Floors },
            { "Columns", BuiltInCategory.OST_StructuralColumns },
            { "Framings", BuiltInCategory.OST_StructuralFraming }
        };

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            int success = 0;
            int failed = 0;
            int skipped = 0;

            // Build a lookup map of existing types per category (matches Python's get_types_map)
            var existingTypeMap = new Dictionary<string, ElementType>();
            foreach (var kvp in Categories)
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(kvp.Value)
                    .WhereElementIsElementType();

                foreach (Element el in collector)
                {
                    string name = el.Name;
                    string familyName = "";
                    if (el is FamilySymbol fs) familyName = fs.Family?.Name ?? "";
                    string fullName = string.IsNullOrEmpty(familyName) ? name : $"{familyName}:{name}";

                    existingTypeMap[fullName] = el as ElementType;
                    if (!existingTypeMap.ContainsKey(name))
                        existingTypeMap[name] = el as ElementType;
                }
            }

            using (Transaction t = new Transaction(doc, "Duplicate Families"))
            {
                t.Start();
                foreach (var row in RowsToProcess)
                {
                    try
                    {
                        // Find base type
                        if (!existingTypeMap.TryGetValue(row.BaseType, out ElementType baseType) || baseType == null)
                        { failed++; continue; }

                        // Generate new name: TypeMark-TypeComment (matches Python naming rule)
                        string newName;
                        if (!string.IsNullOrEmpty(row.TypeComment))
                            newName = !string.IsNullOrEmpty(row.TypeMark) ? $"{row.TypeMark}-{row.TypeComment}" : row.TypeComment;
                        else
                            newName = row.TypeMark;

                        // Check if type already exists (matches Python duplicate check)
                        if (existingTypeMap.ContainsKey(newName))
                        {
                            skipped++;
                            continue;
                        }

                        // Duplicate
                        ElementType newType = baseType.Duplicate(newName) as ElementType;
                        if (newType == null) { failed++; continue; }

                        // Set parameters
                        var tmParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        if (tmParam != null && !tmParam.IsReadOnly) tmParam.Set(row.TypeMark ?? "");

                        var descParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                        if (descParam != null && !descParam.IsReadOnly)
                            descParam.Set(row.Description ?? "");
                        else
                        {
                            descParam = newType.LookupParameter("Description");
                            if (descParam != null && !descParam.IsReadOnly)
                                descParam.Set(row.Description ?? "");
                        }

                        var tcParam = newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                        if (tcParam != null && !tcParam.IsReadOnly) tcParam.Set(row.TypeComment ?? "");

                        // Add to map so next rows see it
                        existingTypeMap[newName] = newType;
                        success++;
                    }
                    catch { failed++; }
                }
                t.Commit();
            }

            string msg = $"Created {success} types.";
            if (skipped > 0) msg += $" Skipped {skipped} (already exist).";
            if (failed > 0) msg += $" Failed: {failed}.";
            TaskDialog.Show("Duplication Complete", msg);
        }

        public string GetName() => "Family Duplication Event Handler";
    }

    // ================================================================
    // HELPER EXTENSION
    // ================================================================
    internal static class DataGridExtensions
    {
        /// <summary>
        /// Check if a DataGrid is currently in editing mode.
        /// </summary>
        public static bool IsEditing(this DataGrid grid)
        {
            // Non-destructive check for editing mode
            return grid.CurrentCell != null && grid.CurrentCell.Column != null && 
                   grid.CurrentCell.Item != null && 
                   System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox;
        }
    }
}
