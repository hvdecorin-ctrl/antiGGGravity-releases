using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using antiGGGravity.Commands.Transfer.DTO;
using antiGGGravity.Commands.Transfer.Modules;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
using Viewport = Autodesk.Revit.DB.Viewport;

namespace antiGGGravity.Commands.Transfer.UI
{
    public class ViewTransferViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private FileManagerModule _fileManager;
        private ViewCollectorModule _viewCollector;
        
        private string _sourceFilePath;
        private string _statusText = "Ready to transfer...";
        private bool _isSourceLoaded;

        private string _viewSearchText;
        private string _sheetSearchText;
        private string _familySearchText;
        private string _selectedCategory = "All";
        private string _currentTab = "General";
        private SheetTransferItem _selectedSheet;
        private FamilyTransferItem _selectedFamily;

        public ObservableCollection<ViewTransferItem> AvailableViews { get; set; } = new ObservableCollection<ViewTransferItem>();
        public ObservableCollection<SheetTransferItem> AvailableSheets { get; set; } = new ObservableCollection<SheetTransferItem>();
        public ObservableCollection<ViewTransferItem> ViewportsInSelectedSheet { get; set; } = new ObservableCollection<ViewTransferItem>();
        public ObservableCollection<FamilyTransferItem> AvailableFamilies { get; set; } = new ObservableCollection<FamilyTransferItem>();
        public ObservableCollection<FamilyTypeItem> SelectedFamilyTypes { get; set; } = new ObservableCollection<FamilyTypeItem>();
        
        public ICollectionView FilteredViews { get; private set; }
        public ICollectionView FilteredSheets { get; private set; }
        public ICollectionView FilteredFamilies { get; private set; }

        public TransferOptions Options { get; set; } = new TransferOptions();

        public TransferRequestHandler RequestHandler { get; private set; }
        public ExternalEvent ExEvent { get; private set; }

        public string ViewSearchText
        {
            get => _viewSearchText;
            set { _viewSearchText = value; FilteredViews.Refresh(); OnPropertyChanged(); }
        }

        public string SheetSearchText
        {
            get => _sheetSearchText;
            set { _sheetSearchText = value; FilteredSheets.Refresh(); OnPropertyChanged(); }
        }

        public string FamilySearchText
        {
            get => _familySearchText;
            set { _familySearchText = value; FilteredFamilies.Refresh(); OnPropertyChanged(); }
        }

        public string CurrentTab
        {
            get => _currentTab;
            set 
            { 
                _currentTab = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsGeneralTab));
                OnPropertyChanged(nameof(IsFamiliesTab));
            }
        }

        public bool IsGeneralTab => CurrentTab == "General";
        public bool IsFamiliesTab => CurrentTab == "Families";

        public ICommand SetTabCommand => new RelayCommand(p => CurrentTab = p?.ToString());

        public string SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                _selectedCategory = value; 
                FilteredViews.Refresh(); 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(IsDetailSelected));
                OnPropertyChanged(nameof(IsDraftingSelected));
                OnPropertyChanged(nameof(IsLegendSelected));
                OnPropertyChanged(nameof(Is2DSelected));
                OnPropertyChanged(nameof(Is3DSelected));
            }
        }

        public bool IsAllSelected => SelectedCategory == "All";
        public bool IsDetailSelected => SelectedCategory == "Detail";
        public bool IsDraftingSelected => SelectedCategory == "Drafting";
        public bool IsLegendSelected => SelectedCategory == "Legend";
        public bool Is2DSelected => SelectedCategory == "2D";
        public bool Is3DSelected => SelectedCategory == "3D";

        public ICommand SetCategoryCommand => new RelayCommand(p => SelectedCategory = p?.ToString());

        public SheetTransferItem SelectedSheet
        {
            get => _selectedSheet;
            set 
            { 
                _selectedSheet = value; 
                UpdateViewportsList();
                OnPropertyChanged(); 
            }
        }

        public FamilyTransferItem SelectedFamily
        {
            get => _selectedFamily;
            set
            {
                _selectedFamily = value;
                UpdateFamilyTypesList();
                OnPropertyChanged();
            }
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set { _sourceFilePath = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsSourceLoaded
        {
            get => _isSourceLoaded;
            set { _isSourceLoaded = value; OnPropertyChanged(); }
        }

        public ViewTransferViewModel(UIApplication uiApp, TransferRequestHandler handler, ExternalEvent exEvent)
        {
            _uiApp = uiApp;
            _fileManager = new FileManagerModule(uiApp);
            RequestHandler = handler;
            ExEvent = exEvent;
            
            Options.DuplicateHandlingPrefix = true;
            Options.PrefixString = "Copied_";

            FilteredViews = CollectionViewSource.GetDefaultView(AvailableViews);
            FilteredViews.Filter = (obj) => 
            {
                var item = obj as ViewTransferItem;
                if (item == null) return false;

                // Category Filter
                bool categoryMatch = true;
                if (SelectedCategory == "Detail") 
                    categoryMatch = item.ViewType == ViewType.Section || item.ViewType == ViewType.Detail;
                else if (SelectedCategory == "Drafting") 
                    categoryMatch = item.ViewType == ViewType.DraftingView;
                else if (SelectedCategory == "Legend") 
                    categoryMatch = item.ViewType == ViewType.Legend;
                else if (SelectedCategory == "2D")
                    categoryMatch = item.ViewType == ViewType.DraftingView || item.ViewType == ViewType.Legend || 
                                    item.ViewType == ViewType.Detail || item.ViewType == ViewType.Section ||
                                    item.ViewType == ViewType.Elevation || item.ViewType == ViewType.FloorPlan ||
                                    item.ViewType == ViewType.CeilingPlan;
                else if (SelectedCategory == "3D")
                    categoryMatch = item.ViewType == ViewType.ThreeD;

                if (!categoryMatch) return false;

                // Search Filter
                if (string.IsNullOrWhiteSpace(ViewSearchText)) return true;
                return item.ViewName.IndexOf(ViewSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            FilteredSheets = CollectionViewSource.GetDefaultView(AvailableSheets);
            FilteredSheets.Filter = (obj) => 
            {
                if (string.IsNullOrWhiteSpace(SheetSearchText)) return true;
                var item = obj as SheetTransferItem;
                return item.SheetName.IndexOf(SheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.SheetNumber.IndexOf(SheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            FilteredFamilies = CollectionViewSource.GetDefaultView(AvailableFamilies);
            FilteredFamilies.Filter = (obj) =>
            {
                if (string.IsNullOrWhiteSpace(FamilySearchText)) return true;
                var item = obj as FamilyTransferItem;
                return item.FamilyName.IndexOf(FamilySearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.CategoryName.IndexOf(FamilySearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
        }

        private void UpdateViewportsList()
        {
            ViewportsInSelectedSheet.Clear();
            if (SelectedSheet == null) return;
            if (_fileManager.DocLoader.SourceDocument == null) return;

            var sourceSheet = _fileManager.DocLoader.SourceDocument.GetElement(SelectedSheet.SourceSheetId) as ViewSheet;
            if (sourceSheet == null) return;

            var vpIds = sourceSheet.GetAllViewports();
            foreach (var vpId in vpIds)
            {
                var vp = _fileManager.DocLoader.SourceDocument.GetElement(vpId) as Viewport;
                if (vp == null) continue;

                var viewItem = AvailableViews.FirstOrDefault(v => v.SourceViewId == vp.ViewId);
                if (viewItem != null)
                {
                    ViewportsInSelectedSheet.Add(viewItem);
                }
            }
        }

        private void UpdateFamilyTypesList()
        {
            SelectedFamilyTypes.Clear();
            if (SelectedFamily == null) return;
            foreach (var type in SelectedFamily.Types)
            {
                SelectedFamilyTypes.Add(type);
            }
        }

        public void LoadSourceModel(string path)
        {
            StatusText = "Loading model in background...";
            SourceFilePath = path;

            if (_fileManager.SelectAndLoadSourceFile(path, out string error))
            {
                _viewCollector = new ViewCollectorModule(_fileManager.DocLoader.SourceDocument);
                var views = _viewCollector.GetTransferableViews();
                var sheets = _viewCollector.GetSheets();
                var families = _viewCollector.GetFamilies(_fileManager.DocLoader.SourceDocument);

                AvailableViews.Clear();
                foreach (var v in views)
                {
                    v.PropertyChanged += ViewItem_PropertyChanged;
                    AvailableViews.Add(v);
                }

                AvailableSheets.Clear();
                foreach (var s in sheets)
                {
                    s.PropertyChanged += SheetItem_PropertyChanged;
                    AvailableSheets.Add(s);
                }

                AvailableFamilies.Clear();
                foreach (var f in families)
                {
                    AvailableFamilies.Add(f);
                }

                IsSourceLoaded = true;
                StatusText = $"Loaded {views.Count} Views, {sheets.Count} Sheets, {families.Count} Families.";
            }
            else
            {
                IsSourceLoaded = false;
                StatusText = $"Error: {error}";
            }
        }

        private void SheetItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetTransferItem.IsSelected))
            {
                var sheet = sender as SheetTransferItem;
                if (sheet == null) return;

                // When a sheet is checked, auto-check all its views
                var sourceSheet = _fileManager.DocLoader.SourceDocument.GetElement(sheet.SourceSheetId) as ViewSheet;
                if (sourceSheet != null)
                {
                    var vpIds = sourceSheet.GetAllViewports();
                    foreach (var vpId in vpIds)
                    {
                        var vp = _fileManager.DocLoader.SourceDocument.GetElement(vpId) as Viewport;
                        if (vp != null)
                        {
                            var viewItem = AvailableViews.FirstOrDefault(v => v.SourceViewId == vp.ViewId);
                            if (viewItem != null)
                            {
                                viewItem.IsSelected = sheet.IsSelected;
                            }
                        }
                    }
                }
            }
        }

        private void ViewItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Optional: link back view selection to sheet if necessary
        }

        public void ExecuteTransfer()
        {
            if (!IsSourceLoaded) return;

            var selectedViews = AvailableViews.Where(v => v.IsSelected).ToList();
            var selectedSheets = AvailableSheets.Where(s => s.IsSelected).ToList();
            
            // Collect all selected types across all families
            var selectedFamilies = new List<FamilyTransferItem>();
            foreach (var family in AvailableFamilies)
            {
                var selectedTypes = family.Types.Where(t => t.IsSelected).ToList();
                if (selectedTypes.Count > 0)
                {
                    // For the engine, we can either pass specific symbols or 
                    // reconstructed FamilyTransferItems (flattened again for the engine logic)
                    foreach (var type in selectedTypes)
                    {
                        selectedFamilies.Add(new FamilyTransferItem
                        {
                            SourceFamilyId = family.SourceFamilyId,
                            SourceSymbolId = type.SourceSymbolId,
                            FamilyName = family.FamilyName,
                            TypeName = type.TypeName,
                            CategoryName = family.CategoryName
                        });
                    }
                }
            }

            if (selectedViews.Count == 0 && selectedSheets.Count == 0 && selectedFamilies.Count == 0)
            {
                StatusText = "Please select at least one item to transfer.";
                return;
            }

            // Push to request handler
            RequestHandler.SourceDoc = _fileManager.DocLoader.SourceDocument;
            RequestHandler.Options = Options;
            RequestHandler.SelectedViews = selectedViews;
            RequestHandler.SelectedSheets = selectedSheets;
            RequestHandler.SelectedFamilies = selectedFamilies;

            StatusText = "Transferring items...";
            
            // Raise the external event to fire outside UI thread
            ExEvent.Raise();
        }

        public void Cleanup()
        {
            _fileManager?.Cleanup();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
