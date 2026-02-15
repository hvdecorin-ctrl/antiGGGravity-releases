using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class FamilyLoadingView : Window
    {
        private const string VIEW_NAME = "FamilyLoading";
        private const string SAVED_PATHS_KEY = "SavedPaths";
        private const int MAX_SAVED_PATHS = 10;

        // External events
        private readonly ExternalEvent _loadEvent;
        private readonly ExternalEvent _symbolsEvent;
        private readonly Commands.ProjectAudit.LoadFamilyTypesHandler _loadHandler;
        private readonly Commands.ProjectAudit.GetSymbolsHandler _symbolsHandler;

        // Data
        private string _currentDirectory;
        private Dictionary<string, string> _familiesDict = new(); // relPath -> absPath
        private List<string> _allFamilyNames = new();
        private string _currentFamilyPath;
        private List<string> _availableTypes = new();
        private HashSet<string> _loadedTypes = new();
        private bool _isLoading;
        private bool _isGettingSymbols;
        private bool _isChangingFolder;

        // Observable collection for types with checkboxes
        public ObservableCollection<TypeItem> TypeItems { get; set; } = new();

        public FamilyLoadingView(
            ExternalEvent loadEvent,
            Commands.ProjectAudit.LoadFamilyTypesHandler loadHandler,
            ExternalEvent symbolsEvent,
            Commands.ProjectAudit.GetSymbolsHandler symbolsHandler)
        {
            InitializeComponent();

            _loadEvent = loadEvent;
            _loadHandler = loadHandler;
            _symbolsEvent = symbolsEvent;
            _symbolsHandler = symbolsHandler;

            UI_List_Types.ItemsSource = TypeItems;
        }

        // ======== PUBLIC METHODS ========

        /// <summary>
        /// Set directory and load families. Called from command after folder selection.
        /// </summary>
        public void SetDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            _currentDirectory = directory;
            LoadFamiliesFromDirectory(directory);
            SavePath(directory);
            PopulateFolderDropdown();
        }

        // ======== FOLDER MANAGEMENT ========

        public void PopulateFolderDropdown()
        {
            _isChangingFolder = true;
            UI_Combo_Folder.Items.Clear();

            // Special options
            UI_Combo_Folder.Items.Add("[Browse for new folder...]");
            UI_Combo_Folder.Items.Add("[Manage saved paths...]");

            // Saved paths
            var savedPaths = GetSavedPaths();
            foreach (string path in savedPaths)
            {
                string folderName = Path.GetFileName(path);
                string parentName = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
                string display = !string.IsNullOrEmpty(parentName)
                    ? $"{parentName}\\{folderName}"
                    : folderName;
                UI_Combo_Folder.Items.Add(display);
            }

            // Select current folder
            if (!string.IsNullOrEmpty(_currentDirectory))
            {
                string folderName = Path.GetFileName(_currentDirectory);
                string parentName = Path.GetFileName(Path.GetDirectoryName(_currentDirectory) ?? "");
                string display = !string.IsNullOrEmpty(parentName)
                    ? $"{parentName}\\{folderName}"
                    : folderName;

                bool found = false;
                for (int i = 0; i < UI_Combo_Folder.Items.Count; i++)
                {
                    if (UI_Combo_Folder.Items[i].ToString() == display)
                    {
                        UI_Combo_Folder.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    UI_Combo_Folder.Items.Add(display);
                    UI_Combo_Folder.SelectedIndex = UI_Combo_Folder.Items.Count - 1;
                }
            }

            _isChangingFolder = false;
        }

        private void UI_Combo_Folder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_Combo_Folder == null || _isChangingFolder || UI_Combo_Folder.SelectedIndex < 0) return;

            string selected = UI_Combo_Folder.SelectedItem?.ToString();
            if (selected == null) return;

            if (selected == "[Browse for new folder...]")
            {
                Topmost = false;
                // Use OpenFileDialog as folder picker (WPF-compatible)
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    ValidateNames = false,
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Select Folder"
                };

                if (dlg.ShowDialog() == true)
                {
                    string path = dlg.FileName;
                    if (File.Exists(path)) path = Path.GetDirectoryName(path);
                    else if (!Directory.Exists(path)) path = Path.GetDirectoryName(path);

                    Topmost = true;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        ChangeFolder(path);
                        SavePath(path);
                        PopulateFolderDropdown();
                    }
                    else
                    {
                        PopulateFolderDropdown();
                    }
                }
                else
                {
                    Topmost = true;
                    PopulateFolderDropdown(); // Restore previous selection
                }
            }
            else if (selected == "[Manage saved paths...]")
            {
                Topmost = false;
                ManagePathsDialog();
                Topmost = true;
                PopulateFolderDropdown();
            }
            else
            {
                // Find matching saved path
                var savedPaths = GetSavedPaths();
                foreach (string path in savedPaths)
                {
                    string folderName = Path.GetFileName(path);
                    string parentName = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
                    string display = !string.IsNullOrEmpty(parentName)
                        ? $"{parentName}\\{folderName}"
                        : folderName;

                    if (display == selected && path != _currentDirectory)
                    {
                        ChangeFolder(path);
                        break;
                    }
                }
            }
        }

        private void ChangeFolder(string newFolder)
        {
            UI_Status.Text = "Loading families from folder...";

            // Find .rfa files recursively, exclude backups
            var allFiles = Directory.GetFiles(newFolder, "*.rfa", SearchOption.AllDirectories)
                .Where(f => !Regex.IsMatch(Path.GetFileName(f), @"\.\d{4}\.rfa$"))
                .ToList();

            if (!allFiles.Any())
            {
                UI_Status.Text = "No family files found in selected folder";
                return;
            }

            _currentDirectory = newFolder;
            _familiesDict = allFiles.ToDictionary(
                f => GetRelativePath(f, newFolder),
                f => f);
            _allFamilyNames = _familiesDict.Keys.OrderBy(n => n).ToList();

            // Clear type selection
            _currentFamilyPath = null;
            _availableTypes.Clear();
            _loadedTypes.Clear();
            TypeItems.Clear();

            // Reset family filter
            UI_Text_FamilyFilter.Text = "Type to filter families...";

            // Repopulate families
            PopulateFamilies();

            UI_Status.Text = $"Found {_familiesDict.Count} families — Select one to see types";
        }

        private void ManagePathsDialog()
        {
            var savedPaths = GetSavedPaths();
            if (!savedPaths.Any())
            {
                MessageBox.Show("No saved paths to manage.", "Manage Saved Paths",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Simple selection window for managing paths
            var manageWindow = new Window
            {
                Title = "Select Paths to Remove",
                Width = 600, Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            var listBox = new ListBox { SelectionMode = SelectionMode.Multiple, Height = 280 };
            foreach (var p in savedPaths) listBox.Items.Add(p);

            var btnRemove = new Button
            {
                Content = "Remove Selected", Width = 140, Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            btnRemove.Click += (s, ev) =>
            {
                var toRemove = listBox.SelectedItems.Cast<string>().ToList();
                foreach (var p in toRemove) RemovePath(p);
                MessageBox.Show($"Removed {toRemove.Count} path(s).", "Paths Removed");
                manageWindow.Close();
            };

            panel.Children.Add(new TextBlock { Text = "Select paths to remove:", Margin = new Thickness(0, 0, 0, 10) });
            panel.Children.Add(listBox);
            panel.Children.Add(btnRemove);
            manageWindow.Content = panel;
            manageWindow.ShowDialog();
        }

        // ======== PATH PERSISTENCE (via SettingsManager) ========

        private List<string> GetSavedPaths()
        {
            string raw = SettingsManager.Get(VIEW_NAME, SAVED_PATHS_KEY, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();

            return raw.Split(';')
                .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                .ToList();
        }

        private void SavePath(string path)
        {
            var paths = GetSavedPaths();
            paths.Remove(path); // Remove if already present
            paths.Insert(0, path); // Add to front
            if (paths.Count > MAX_SAVED_PATHS)
                paths = paths.Take(MAX_SAVED_PATHS).ToList();

            SettingsManager.Set(VIEW_NAME, SAVED_PATHS_KEY, string.Join(";", paths));
            SettingsManager.SaveAll();
        }

        private void RemovePath(string path)
        {
            var paths = GetSavedPaths();
            paths.Remove(path);
            SettingsManager.Set(VIEW_NAME, SAVED_PATHS_KEY, string.Join(";", paths));
            SettingsManager.SaveAll();
        }

        // ======== FAMILY LIST ========

        private void LoadFamiliesFromDirectory(string directory)
        {
            var allFiles = Directory.GetFiles(directory, "*.rfa", SearchOption.AllDirectories)
                .Where(f => !Regex.IsMatch(Path.GetFileName(f), @"\.\d{4}\.rfa$"))
                .ToList();

            _familiesDict = allFiles.ToDictionary(
                f => GetRelativePath(f, directory),
                f => f);
            _allFamilyNames = _familiesDict.Keys.OrderBy(n => n).ToList();

            PopulateFamilies();
            UI_Status.Text = $"Found {_familiesDict.Count} families";
        }

        private void PopulateFamilies()
        {
            UI_List_Families.Items.Clear();
            foreach (string name in _allFamilyNames)
                UI_List_Families.Items.Add(name);
        }

        // ======== FAMILY FILTER ========

        private void UI_Text_FamilyFilter_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UI_Text_FamilyFilter.Text == "Type to filter families...")
                UI_Text_FamilyFilter.Text = "";
        }

        private void UI_Text_FamilyFilter_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UI_Text_FamilyFilter.Text))
                UI_Text_FamilyFilter.Text = "Type to filter families...";
        }

        private void UI_Text_FamilyFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UI_List_Families == null || UI_Text_FamilyFilter == null) return;

            string filter = UI_Text_FamilyFilter.Text.ToLower();
            if (filter == "type to filter families...") filter = "";

            UI_List_Families.Items.Clear();
            foreach (string name in _allFamilyNames)
            {
                if (string.IsNullOrEmpty(filter) || name.ToLower().Contains(filter))
                    UI_List_Families.Items.Add(name);
            }
        }

        // ======== FAMILY SELECTION -> TYPE PREVIEW ========

        private void UI_List_Families_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_List_Families.SelectedIndex < 0 || _isGettingSymbols) return;

            string selectedName = UI_List_Families.SelectedItem?.ToString();
            if (selectedName == null || !_familiesDict.TryGetValue(selectedName, out string absPath))
                return;

            _currentFamilyPath = absPath;
            UI_Status.Text = "Loading types... (please wait)";
            TypeItems.Clear();
            _isGettingSymbols = true;

            // Reset types filter
            UI_Text_TypesFilter.Text = "Type to filter types...";

            // Set up handler and raise external event
            _symbolsHandler.SetData(_currentFamilyPath, OnSymbolsReceived);
            _symbolsEvent.Raise();
        }

        private void OnSymbolsReceived(List<string> allSymbols, HashSet<string> loadedSymbols, string error)
        {
            // This callback might be called from Revit's thread, marshal to UI thread
            Dispatcher.Invoke(() =>
            {
                _isGettingSymbols = false;

                if (!string.IsNullOrEmpty(error))
                {
                    UI_Status.Text = $"Error: {error}";
                    return;
                }

                _availableTypes = allSymbols;
                _loadedTypes = loadedSymbols;

                // Filter to unloaded types only
                var unloadedTypes = allSymbols.Where(t => !loadedSymbols.Contains(t)).ToList();

                if (!unloadedTypes.Any())
                {
                    UI_Status.Text = "All types from this family are already loaded";
                }
                else
                {
                    TypeItems.Clear();
                    foreach (string typeName in unloadedTypes)
                        TypeItems.Add(new TypeItem { Name = typeName, IsChecked = false });

                    UI_Status.Text = $"{unloadedTypes.Count} types available to load ({loadedSymbols.Count} already loaded)";
                }
            });
        }

        // ======== TYPES FILTER ========

        private void UI_Text_TypesFilter_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UI_Text_TypesFilter.Text == "Type to filter types...")
                UI_Text_TypesFilter.Text = "";
        }

        private void UI_Text_TypesFilter_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UI_Text_TypesFilter.Text))
                UI_Text_TypesFilter.Text = "Type to filter types...";
        }

        private void UI_Text_TypesFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TypeItems == null || UI_Text_TypesFilter == null) return;

            string filter = UI_Text_TypesFilter.Text.ToLower();
            if (filter == "type to filter types...") filter = "";

            // Rebuild the visible items from _availableTypes, excluding loaded ones
            TypeItems.Clear();
            foreach (string typeName in _availableTypes)
            {
                if (_loadedTypes.Contains(typeName)) continue;
                if (string.IsNullOrEmpty(filter) || typeName.ToLower().Contains(filter))
                    TypeItems.Add(new TypeItem { Name = typeName, IsChecked = false });
            }
        }

        // ======== BUTTON HANDLERS ========

        private void UI_Btn_CheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TypeItems) item.IsChecked = true;
        }

        private void UI_Btn_UncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TypeItems) item.IsChecked = false;
        }

        private void UI_Btn_Load_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                UI_Status.Text = "Already loading...";
                return;
            }

            if (string.IsNullOrEmpty(_currentFamilyPath))
            {
                UI_Status.Text = "Please select a family first";
                return;
            }

            var checkedTypes = TypeItems.Where(t => t.IsChecked).Select(t => t.Name).ToList();
            if (!checkedTypes.Any())
            {
                UI_Status.Text = "Please select at least one type to load";
                return;
            }

            // Set up handler
            _loadHandler.SetData(_currentFamilyPath, checkedTypes, OnLoadComplete);

            _isLoading = true;
            UI_Btn_Load.IsEnabled = false;
            UI_Status.Text = $"Loading {checkedTypes.Count} types... (you can work with Revit)";

            _loadEvent.Raise();
        }

        private void OnLoadComplete(bool success, string error)
        {
            Dispatcher.Invoke(() =>
            {
                _isLoading = false;
                UI_Btn_Load.IsEnabled = true;

                if (success)
                {
                    UI_Status.Text = "Successfully loaded! Select more types or another family.";
                    // Refresh types to show remaining unloaded
                    if (UI_List_Families.SelectedIndex >= 0)
                        UI_List_Families_SelectionChanged(null, null);
                }
                else
                {
                    UI_Status.Text = $"Error: {error}";
                }
            });
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ======== HELPERS ========

        private static string GetRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath)) return fullPath;

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>
    /// Data item for each family type with a checkbox.
    /// </summary>
    public class TypeItem : INotifyPropertyChanged
    {
        private string _name;
        private bool _isChecked;

        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
