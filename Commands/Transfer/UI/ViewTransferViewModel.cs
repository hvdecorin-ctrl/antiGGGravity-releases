using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using antiGGGravity.Commands.Transfer.DTO;
using antiGGGravity.Commands.Transfer.Modules;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;
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
        private string _selectedFamilyCategory = "All";
        private bool _hideExistingFamilies = false;
        
        // Main Sidebar Tabs
        private string _currentMainTab = "Standard Details";
        
        // Standard Details Sub-Tabs
        private string _currentTab = "General";
        private SheetTransferItem _selectedSheet;
        private FamilyTransferItem _selectedFamily;
        private bool? _selectAllViews = false;
        private bool? _selectAllSheets = false;
        private bool? _selectAllFamilyTypes = false;
        private bool? _selectAllFamilies = false;
        private bool? _selectAllViewports = false;

        // System Families
        private string _systemTypeSearchText;
        private string _selectedSystemCategory = "All";
        private bool _hideExistingSystemTypes = false;
        private bool? _selectAllSystemTypes = false;

        // Standard file quick-load
        private TransferSettings _transferSettings;
        private string _standard1Path;
        private string _standard2Path;
        private string _folder1Path;
        private string _folder2Path;

        public ObservableCollection<ViewTransferItem> AvailableViews { get; set; } = new ObservableCollection<ViewTransferItem>();
        public ObservableCollection<SheetTransferItem> AvailableSheets { get; set; } = new ObservableCollection<SheetTransferItem>();
        public ObservableCollection<ViewTransferItem> ViewportsInSelectedSheet { get; set; } = new ObservableCollection<ViewTransferItem>();
        public ObservableCollection<FamilyTransferItem> AvailableFamilies { get; set; } = new ObservableCollection<FamilyTransferItem>();
        public ObservableCollection<FamilyTypeItem> SelectedFamilyTypes { get; set; } = new ObservableCollection<FamilyTypeItem>();
        public ObservableCollection<SystemFamilyTypeItem> AvailableSystemTypes { get; set; } = new ObservableCollection<SystemFamilyTypeItem>();
        
        public ICollectionView FilteredViews { get; private set; }
        public ICollectionView FilteredSheets { get; private set; }
        public ICollectionView FilteredFamilies { get; private set; }
        public ICollectionView FilteredSystemTypes { get; private set; }

        public TransferOptions Options { get; set; } = new TransferOptions();

        public TransferRequestHandler RequestHandler { get; private set; }
        public ExternalEvent ExEvent { get; private set; }

        // Family Manager
        private string _familyManagerFolderPath;
        private string _familyManagerSearchText;

        private bool? _selectAllManagerFamilies = false;

        public ObservableCollection<FamilyManagerItem> AvailableManagerFamilies { get; set; } = new ObservableCollection<FamilyManagerItem>();
        public ICollectionView FilteredManagerFamilies { get; private set; }
        
        public FamilyManagerRequestHandler FmRequestHandler { get; private set; }
        public ExternalEvent FmExEvent { get; private set; }

        private FamilyManagerItem _selectedManagerFamily;
        private string _managerFamilyTypeSearchText;
        private bool? _selectAllManagerFamilyTypes = false;

        public ReadFamilyTypesHandler TypesRequestHandler { get; private set; }
        public ExternalEvent TypesExEvent { get; private set; }

        public ICollectionView FilteredManagerFamilyTypes { get; private set; }

        // Duplicator
        public DuplicatorRequestHandler DupRequestHandler { get; private set; }
        public ExternalEvent DupExEvent { get; private set; }
        public ObservableCollection<DuplicatorRow> DuplicatorRows { get; set; } = new ObservableCollection<DuplicatorRow>();
        public ObservableCollection<FamilyManagerItem> LoadedProjectFamilies { get; set; } = new ObservableCollection<FamilyManagerItem>();

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

        public string CurrentMainTab
        {
            get => _currentMainTab;
            set
            {
                if (_currentMainTab != value)
                {
                    _currentMainTab = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStandardDetailsTab));
                    OnPropertyChanged(nameof(IsFamilyManagerTab));
                    OnPropertyChanged(nameof(IsDuplicatorTab));
                }
            }
        }

        public bool IsStandardDetailsTab => CurrentMainTab == "Standard Details";
        public bool IsFamilyManagerTab => CurrentMainTab == "Family Manager";
        public bool IsDuplicatorTab => CurrentMainTab == "Duplicator";

        public bool IsLibraryFolderLinked => !string.IsNullOrEmpty(Folder1Path) || AvailableManagerFamilies?.Any() == true;

        public ICommand SetMainTabCommand => new RelayCommand(p => CurrentMainTab = p?.ToString());

        public string FamilyManagerFolderPath 
        { 
            get => _familyManagerFolderPath; 
            set 
            { 
                _familyManagerFolderPath = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsFolder1Active)); 
                OnPropertyChanged(nameof(IsFolder2Active)); 
            } 
        }

        public string FamilyManagerSearchText
        {
            get => _familyManagerSearchText;
            set { _familyManagerSearchText = value; FilteredManagerFamilies?.Refresh(); OnPropertyChanged(); }
        }


        public FamilyManagerItem SelectedManagerFamily
        {
            get => _selectedManagerFamily;
            set 
            {
                _selectedManagerFamily = value;
                OnPropertyChanged();
                if (_selectedManagerFamily != null)
                {
                    _managerFamilyTypeSearchText = string.Empty;
                    OnPropertyChanged(nameof(ManagerFamilyTypeSearchText));
                    
                    if (_selectedManagerFamily.Types.Count == 0 && !string.IsNullOrEmpty(_selectedManagerFamily.FilePath))
                    {
                        StatusText = $"Reading types for {_selectedManagerFamily.FamilyName}...";
                        TypesRequestHandler.TargetFamily = _selectedManagerFamily;
                        TypesExEvent.Raise();
                    }
                    else
                    {
                        UpdateFilteredManagerFamilyTypes();
                        UpdateSelectAllManagerFamilyTypesState();
                    }
                }
            }
        }

        public string ManagerFamilyTypeSearchText
        {
            get => _managerFamilyTypeSearchText;
            set { _managerFamilyTypeSearchText = value; FilteredManagerFamilyTypes?.Refresh(); OnPropertyChanged(); }
        }

        private bool _isUpdatingManagerSelectAll = false;

        public bool? SelectAllManagerFamilyTypes
        {
            get => _selectAllManagerFamilyTypes;
            set
            {
                if (value == null || _isUpdatingManagerSelectAll) return;
                
                _isUpdatingManagerSelectAll = true;
                _selectAllManagerFamilyTypes = value;
                if (SelectedManagerFamily != null)
                {
                    foreach (var item in SelectedManagerFamily.Types)
                    {
                        if (PassesManagerTypeFilter(item))
                            item.IsSelected = value.Value;
                    }
                }
                _isUpdatingManagerSelectAll = false;
                
                OnPropertyChanged();
            }
        }

        public bool? SelectAllManagerFamilies
        {
            get => _selectAllManagerFamilies;
            set
            {
                if (value == null || _isUpdatingManagerSelectAll) return;
                
                _isUpdatingManagerSelectAll = true;
                _selectAllManagerFamilies = value;
                foreach (var item in AvailableManagerFamilies)
                {
                    if (PassesManagerFamilyFilter(item))
                        item.IsSelected = value.Value;
                }
                _isUpdatingManagerSelectAll = false;
                
                OnPropertyChanged();
            }
        }

        public string CurrentTab
        {
            get => _currentTab;
            set 
            { 
                if (_currentTab != value)
                {
                    _currentTab = value;
                    ResetFilters();
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(IsGeneralTab));
                    OnPropertyChanged(nameof(IsFamiliesTab));
                    OnPropertyChanged(nameof(IsSystemFamiliesTab));
                }
            }
        }

        private void ResetFilters()
        {
            _viewSearchText = string.Empty;
            _sheetSearchText = string.Empty;
            _familySearchText = string.Empty;
            _selectedCategory = "All";
            _selectedFamilyCategory = "All";
            
            OnPropertyChanged(nameof(ViewSearchText));
            OnPropertyChanged(nameof(SheetSearchText));
            OnPropertyChanged(nameof(FamilySearchText));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(SelectedFamilyCategory));
            FilteredViews?.Refresh();
            FilteredSheets?.Refresh();
            FilteredFamilies?.Refresh();
        }

        public bool IsGeneralTab => CurrentTab == "General";
        public bool IsFamiliesTab => CurrentTab == "Families";
        public bool IsSystemFamiliesTab => CurrentTab == "SystemFamilies";

        // System type filter properties
        public string SystemTypeSearchText
        {
            get => _systemTypeSearchText;
            set { _systemTypeSearchText = value; FilteredSystemTypes?.Refresh(); OnPropertyChanged(); }
        }

        public string SelectedSystemCategory
        {
            get => _selectedSystemCategory;
            set { _selectedSystemCategory = value; FilteredSystemTypes?.Refresh(); OnPropertyChanged(); OnPropertyChanged(nameof(IsSystemCatAll)); OnPropertyChanged(nameof(IsSystemCatStructural)); OnPropertyChanged(nameof(IsSystemCatArchitectural)); }
        }

        public bool IsSystemCatAll => SelectedSystemCategory == "All";
        public bool IsSystemCatStructural => SelectedSystemCategory == "Structural";
        public bool IsSystemCatArchitectural => SelectedSystemCategory == "Architectural";

        public ICommand SetSystemCategoryCommand => new RelayCommand(p => SelectedSystemCategory = p?.ToString());

        public bool HideExistingSystemTypes
        {
            get => _hideExistingSystemTypes;
            set { _hideExistingSystemTypes = value; FilteredSystemTypes?.Refresh(); OnPropertyChanged(); }
        }

        public bool? SelectAllSystemTypes
        {
            get => _selectAllSystemTypes;
            set
            {
                if (value == null) return;
                _selectAllSystemTypes = value;
                foreach (SystemFamilyTypeItem item in FilteredSystemTypes)
                    item.IsSelected = value.Value;
                OnPropertyChanged();
            }
        }

        public ICommand SetTabCommand => new RelayCommand(p => CurrentTab = p?.ToString());

        public string SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                _selectedCategory = value; 
                FilteredViews?.Refresh(); 
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

        public string SelectedFamilyCategory
        {
            get => _selectedFamilyCategory;
            set
            {
                _selectedFamilyCategory = value;
                FilteredFamilies.Refresh();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFamilyAllSelected));
                OnPropertyChanged(nameof(IsFamily2DSelected));
                OnPropertyChanged(nameof(IsFamily3DSelected));
            }
        }

        public bool HideExistingFamilies
        {
            get => _hideExistingFamilies;
            set
            {
                _hideExistingFamilies = value;
                FilteredFamilies.Refresh();
                UpdateFamilyTypesList(); // Re-filter the details list too
                OnPropertyChanged();
            }
        }

        public bool IsFamilyAllSelected => SelectedFamilyCategory == "All";
        public bool IsFamily2DSelected => SelectedFamilyCategory == "2D";
        public bool IsFamily3DSelected => SelectedFamilyCategory == "3D";

        public ICommand SetCategoryCommand => new RelayCommand(p => SelectedCategory = p?.ToString());
        public ICommand SetFamilyCategoryCommand => new RelayCommand(p => SelectedFamilyCategory = p?.ToString());

        public SheetTransferItem SelectedSheet
        {
            get => _selectedSheet;
            set 
            { 
                _selectedSheet = value; 
                UpdateViewportsList();
                _selectAllViewports = false; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(SelectAllViewports));
            }
        }

        public FamilyTransferItem SelectedFamily
        {
            get => _selectedFamily;
            set
            {
                _selectedFamily = value;
                UpdateFamilyTypesList();
                _selectAllFamilyTypes = false; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectAllFamilyTypes));
            }
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set
            {
                _sourceFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStandard1Loaded));
                OnPropertyChanged(nameof(IsStandard2Loaded));
            }
        }

        public bool? SelectAllViews
        {
            get => _selectAllViews;
            set
            {
                _selectAllViews = value;
                if (value.HasValue)
                {
                    foreach (ViewTransferItem item in FilteredViews)
                        item.IsSelected = value.Value;
                }
                OnPropertyChanged();
            }
        }

        public bool? SelectAllSheets
        {
            get => _selectAllSheets;
            set
            {
                _selectAllSheets = value;
                if (value.HasValue)
                {
                    foreach (SheetTransferItem item in FilteredSheets)
                        item.IsSelected = value.Value;
                }
                OnPropertyChanged();
            }
        }

        public bool? SelectAllFamilies
        {
            get => _selectAllFamilies;
            set
            {
                _selectAllFamilies = value;
                if (value.HasValue)
                {
                    foreach (FamilyTransferItem item in FilteredFamilies)
                        item.IsSelected = value.Value;
                }
                OnPropertyChanged();
            }
        }

        public bool? SelectAllFamilyTypes
        {
            get => _selectAllFamilyTypes;
            set
            {
                _selectAllFamilyTypes = value;
                if (value.HasValue)
                {
                    foreach (var item in SelectedFamilyTypes)
                        item.IsSelected = value.Value;
                }
                OnPropertyChanged();
            }
        }

        public bool? SelectAllViewports
        {
            get => _selectAllViewports;
            set
            {
                _selectAllViewports = value;
                if (value.HasValue)
                {
                    foreach (var item in ViewportsInSelectedSheet)
                        item.IsSelected = value.Value;
                }
                OnPropertyChanged();
            }
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

        public ViewTransferViewModel(UIApplication uiApp, TransferRequestHandler handler, ExternalEvent exEvent, FamilyManagerRequestHandler fmHandler, ExternalEvent fmExEvent, ReadFamilyTypesHandler typesHandler, ExternalEvent typesExEvent, DuplicatorRequestHandler dupHandler = null, ExternalEvent dupExEvent = null)
        {
            _uiApp = uiApp;
            _fileManager = new FileManagerModule(uiApp);
            RequestHandler = handler;
            ExEvent = exEvent;
            FmRequestHandler = fmHandler;
            FmExEvent = fmExEvent;
            TypesRequestHandler = typesHandler;
            TypesExEvent = typesExEvent;
            DupRequestHandler = dupHandler;
            DupExEvent = dupExEvent;
            
            RequestHandler.TransferCompleted += OnTransferCompleted;
            if (FmRequestHandler != null) FmRequestHandler.ProcessCompleted += OnManagerProcessCompleted;
            if (TypesRequestHandler != null) TypesRequestHandler.TypesReadCompleted += OnTypesReadCompleted;

            if (_uiApp?.ActiveUIDocument?.Document != null)
            {
                var doc = _uiApp.ActiveUIDocument.Document;

                // Restrict local project cache to purely Structural Categories (Phase 9)
                var allowedCategories = new long[]
                {
                    (long)BuiltInCategory.OST_StructuralFraming,
                    (long)BuiltInCategory.OST_StructuralColumns,
                    (long)BuiltInCategory.OST_StructuralFoundation,
                    (long)BuiltInCategory.OST_Walls,
                    (long)BuiltInCategory.OST_Floors
                };

                var symbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => s.Category != null && allowedCategories.Contains(s.Category.Id.GetIdValue()))
                    .GroupBy(s => s.FamilyName)
                    .ToList();

                foreach (var group in symbols)
                {
                    var firstSymbol = group.First();
                    var categoryName = firstSymbol.Category?.Name ?? "";

                    var familyItem = new FamilyManagerItem 
                    { 
                        FamilyName = group.Key, 
                        CategoryName = categoryName,
                        FilePath = "Current Project" 
                    };
                    familyItem.Types = new ObservableCollection<FamilyManagerTypeItem>(
                        group.Select(s => new FamilyManagerTypeItem { TypeName = s.Name })
                    );
                    LoadedProjectFamilies.Add(familyItem);
                }
            }

            // Wire up new DataGrid rows added directly via WPF UI interacting with ObservableCollection (Phase 7 Fix)
            DuplicatorRows.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (DuplicatorRow item in e.NewItems)
                    {
                        item.PropertyChanged -= DuplicatorRow_PropertyChanged; // Prevent double subscription
                        item.PropertyChanged += DuplicatorRow_PropertyChanged;
                    }
                }
            };

            Options.DuplicateHandlingPrefix = true;
            Options.PrefixString = "Copied_";

            // Load saved standard file and folder paths
            _transferSettings = TransferSettings.Load();
            _standard1Path = _transferSettings.Standard1Path;
            _standard2Path = _transferSettings.Standard2Path;
            _folder1Path = _transferSettings.Folder1Path;
            _folder2Path = _transferSettings.Folder2Path;

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
                var item = obj as FamilyTransferItem;
                if (item == null) return false;

                // 2D/3D Category Filter
                if (SelectedFamilyCategory == "2D" && !item.Is2D) return false;
                if (SelectedFamilyCategory == "3D" && item.Is2D) return false;

                // Hide Existing Filter
                if (HideExistingFamilies && item.Types.All(t => t.IsAlreadyInTarget)) return false;

                // Search Filter
                if (string.IsNullOrWhiteSpace(FamilySearchText)) return true;
                return item.FamilyName.IndexOf(FamilySearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.CategoryName.IndexOf(FamilySearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            FilteredSystemTypes = CollectionViewSource.GetDefaultView(AvailableSystemTypes);
            FilteredSystemTypes.Filter = (obj) =>
            {
                var item = obj as SystemFamilyTypeItem;
                if (item == null) return false;

                // Category group filter
                if (SelectedSystemCategory == "Structural")
                {
                    bool isStructural = item.CategoryName.Contains("Structural") || 
                                       item.CategoryName.Contains("Rebar") ||
                                       item.CategoryName.Contains("Foundation");
                    if (!isStructural) return false;
                }
                else if (SelectedSystemCategory == "Architectural")
                {
                    bool isArch = item.CategoryName.Contains("Wall") || 
                                  item.CategoryName.Contains("Floor") ||
                                  item.CategoryName.Contains("Roof") ||
                                  item.CategoryName.Contains("Ceiling");
                    if (!isArch) return false;
                }

                // Hide existing
                if (HideExistingSystemTypes && item.IsAlreadyInTarget) return false;

                // Search
                if (string.IsNullOrWhiteSpace(SystemTypeSearchText)) return true;
                return item.TypeName.IndexOf(SystemTypeSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.FamilyName.IndexOf(SystemTypeSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.CategoryName.IndexOf(SystemTypeSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            FilteredManagerFamilies = CollectionViewSource.GetDefaultView(AvailableManagerFamilies);
            FilteredManagerFamilies.Filter = (obj) =>
            {
                var item = obj as FamilyManagerItem;
                if (item == null) return false;

                if (string.IsNullOrWhiteSpace(FamilyManagerSearchText)) return true;
                return item.FamilyName.IndexOf(FamilyManagerSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.CategoryName.IndexOf(FamilyManagerSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       item.Status.IndexOf(FamilyManagerSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            // Load last used Family Manager folder
            _familyManagerFolderPath = _transferSettings.LastManagerFolderPath;
            if (!string.IsNullOrEmpty(_familyManagerFolderPath) && Directory.Exists(_familyManagerFolderPath))
            {
                // Run index load asynchronously or just load it fast
                ScanManagerFolder(_familyManagerFolderPath, useCache: true);
            }
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
                    // Ensure we listen for changes to keep SelectAllViewports in sync
                    viewItem.PropertyChanged -= ViewItemInSheet_PropertyChanged;
                    viewItem.PropertyChanged += ViewItemInSheet_PropertyChanged;
                    ViewportsInSelectedSheet.Add(viewItem);
                }
            }
        }

        private void ViewItemInSheet_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewTransferItem.IsSelected))
                UpdateSelectAllViewportsState();
        }

        private void UpdateSelectAllViewportsState()
        {
            if (ViewportsInSelectedSheet.Count == 0) { _selectAllViewports = false; }
            else
            {
                bool allChecked = ViewportsInSelectedSheet.All(v => v.IsSelected);
                bool noneChecked = ViewportsInSelectedSheet.All(v => !v.IsSelected);
                _selectAllViewports = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllViewports));
        }

        private void UpdateFamilyTypesList()
        {
            // Unsubscribe from old types
            foreach (var t in SelectedFamilyTypes) t.PropertyChanged -= FamilyTypeItem_PropertyChanged;
            
            SelectedFamilyTypes.Clear();
            if (SelectedFamily == null) return;
            foreach (var type in SelectedFamily.Types)
            {
                if (HideExistingFamilies && type.IsAlreadyInTarget) continue;
                
                type.PropertyChanged -= FamilyTypeItem_PropertyChanged;
                type.PropertyChanged += FamilyTypeItem_PropertyChanged;
                SelectedFamilyTypes.Add(type);
            }
            UpdateSelectAllFamilyTypesState();
        }

        private void FamilyTypeItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyTypeItem.IsSelected))
                UpdateSelectAllFamilyTypesState();
        }

        private void UpdateSelectAllFamilyTypesState()
        {
            if (SelectedFamilyTypes.Count == 0) { _selectAllFamilyTypes = false; }
            else
            {
                bool allChecked = SelectedFamilyTypes.All(t => t.IsSelected);
                bool noneChecked = SelectedFamilyTypes.All(t => !t.IsSelected);
                _selectAllFamilyTypes = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllFamilyTypes));
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
                    f.PropertyChanged += FamilyItem_PropertyChanged;
                    AvailableFamilies.Add(f);
                }

                // System Family Types
                var systemTypes = _viewCollector.GetSystemFamilyTypes(_fileManager.DocLoader.SourceDocument);
                AvailableSystemTypes.Clear();
                foreach (var st in systemTypes)
                {
                    st.PropertyChanged += SystemTypeItem_PropertyChanged;
                    AvailableSystemTypes.Add(st);
                }

                IsSourceLoaded = true;
                StatusText = $"Loaded {views.Count} Views, {sheets.Count} Sheets, {families.Count} Families, {systemTypes.Count} System Types.";
            }
            else
            {
                IsSourceLoaded = false;
                StatusText = $"Error: {error}";
            }
        }

        private void FamilyItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyTransferItem.IsSelected))
            {
                UpdateSelectAllFamiliesState();
                // If on types tab, refresh types displayed
                UpdateFamilyTypesList();
            }
        }

        private void UpdateSelectAllFamiliesState()
        {
            var list = FilteredFamilies.OfType<FamilyTransferItem>().ToList();
            if (list.Count == 0) { _selectAllFamilies = false; }
            else
            {
                bool allChecked = list.All(f => f.IsSelected);
                bool noneChecked = list.All(f => !f.IsSelected);
                _selectAllFamilies = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllFamilies));
        }

        private void SystemTypeItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemFamilyTypeItem.IsSelected))
                UpdateSelectAllSystemTypesState();
        }

        private void UpdateSelectAllSystemTypesState()
        {
            var list = FilteredSystemTypes.OfType<SystemFamilyTypeItem>().ToList();
            if (list.Count == 0) { _selectAllSystemTypes = false; }
            else
            {
                bool allChecked = list.All(s => s.IsSelected);
                bool noneChecked = list.All(s => !s.IsSelected);
                _selectAllSystemTypes = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllSystemTypes));
        }

        private void SheetItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SheetTransferItem.IsSelected))
            {
                UpdateSelectAllSheetsState();
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
            if (e.PropertyName == nameof(ViewTransferItem.IsSelected))
            {
                UpdateSelectAllViewsState();
            }
        }

        private void UpdateSelectAllViewsState()
        {
            var list = FilteredViews.OfType<ViewTransferItem>().ToList();
            if (list.Count == 0) { _selectAllViews = false; }
            else
            {
                bool allChecked = list.All(v => v.IsSelected);
                bool noneChecked = list.All(v => !v.IsSelected);
                _selectAllViews = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllViews));
        }

        private void UpdateSelectAllSheetsState()
        {
            var list = FilteredSheets.OfType<SheetTransferItem>().ToList();
            if (list.Count == 0) { _selectAllSheets = false; }
            else
            {
                bool allChecked = list.All(s => s.IsSelected);
                bool noneChecked = list.All(s => !s.IsSelected);
                _selectAllSheets = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllSheets));
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

            // Collect selected system family types
            var selectedSystemTypes = AvailableSystemTypes.Where(st => st.IsSelected).ToList();

            if (selectedViews.Count == 0 && selectedSheets.Count == 0 && selectedFamilies.Count == 0 && selectedSystemTypes.Count == 0)
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
            RequestHandler.SelectedSystemTypes = selectedSystemTypes;

            StatusText = "Transferring items...";
            
            // Raise the external event to fire outside UI thread
            ExEvent.Raise();
        }

        public void ScanManagerFolder(string folderPath, bool useCache = true)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;
            
            StatusText = useCache ? "Loading indexed families..." : "Scanning directory and building index...";
            
            // Save last used folder settings
            FamilyManagerFolderPath = folderPath;
            if (_transferSettings != null)
            {
                _transferSettings.LastManagerFolderPath = folderPath;
                _transferSettings.Save();
            }

            AvailableManagerFamilies.Clear();

            System.Collections.Generic.List<FamilyManagerItem> items;
            
            if (useCache)
            {
                var cachedItems = antiGGGravity.Commands.Transfer.Core.LibraryIndexer.LoadIndex(folderPath);
                if (cachedItems != null)
                {
                    items = cachedItems;
                }
                else
                {
                    StatusText = "Index not found. Scanning directory...";
                    items = antiGGGravity.Commands.Transfer.Core.LibraryIndexer.BuildIndex(_uiApp.Application, _uiApp.ActiveUIDocument.Document, folderPath);
                }
            }
            else
            {
                items = antiGGGravity.Commands.Transfer.Core.LibraryIndexer.BuildIndex(_uiApp.Application, _uiApp.ActiveUIDocument.Document, folderPath);
            }

            foreach (var item in items)
            {
                item.PropertyChanged -= ManagerFamilyItem_PropertyChanged;
                item.PropertyChanged += ManagerFamilyItem_PropertyChanged;
                AvailableManagerFamilies.Add(item);
            }
            
            RefreshFamilyStatuses(); // Set real-time Loaded/Missing status for current project
            StatusText = $"Loaded {items.Count} families.";
            OnPropertyChanged(nameof(IsLibraryFolderLinked));
        }

        private void ManagerFamilyItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyManagerItem.IsSelected) && !_isUpdatingManagerSelectAll)
                UpdateSelectAllManagerFamiliesState();
        }

        private void UpdateSelectAllManagerFamiliesState()
        {
            var list = FilteredManagerFamilies.OfType<FamilyManagerItem>().ToList();
            if (list.Count == 0) { _selectAllManagerFamilies = false; }
            else
            {
                bool allChecked = list.All(f => f.IsSelected);
                bool noneChecked = list.All(f => !f.IsSelected);
                _selectAllManagerFamilies = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllManagerFamilies));
        }

        private void UpdateFilteredManagerFamilyTypes()
        {
            if (SelectedManagerFamily == null) return;
            FilteredManagerFamilyTypes = CollectionViewSource.GetDefaultView(SelectedManagerFamily.Types);
            FilteredManagerFamilyTypes.Filter = (obj) =>
            {
                var item = obj as FamilyManagerTypeItem;
                if (item == null) return false;
                
                // Hide Loaded Types (as per user request)
                if (item.IsAlreadyInTarget) return false;

                if (string.IsNullOrWhiteSpace(ManagerFamilyTypeSearchText)) return true;
                return item.TypeName.IndexOf(ManagerFamilyTypeSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            OnPropertyChanged(nameof(FilteredManagerFamilyTypes));
        }

        private void OnTypesReadCompleted(object sender, TypesReadEventArgs e)
        {
            if (e.Family != null)
            {
                e.Family.Types.Clear();
                foreach (var stItem in e.ExtractedTypes)
                {
                    stItem.PropertyChanged += ManagerFamilyTypeItem_PropertyChanged;
                    e.Family.Types.Add(stItem);
                }
                
                if (SelectedManagerFamily == e.Family)
                {
                    UpdateFilteredManagerFamilyTypes();
                    UpdateSelectAllManagerFamilyTypesState();
                    StatusText = $"Read {e.ExtractedTypes.Count} types from {e.Family.FamilyName}.";
                }
            }
        }
        
        private void ManagerFamilyTypeItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyManagerTypeItem.IsSelected) && !_isUpdatingManagerSelectAll)
                UpdateSelectAllManagerFamilyTypesState();
        }

        public void UpdateSelectAllManagerFamilyTypesState()
        {
            if (SelectedManagerFamily == null) { _selectAllManagerFamilyTypes = false; OnPropertyChanged(nameof(SelectAllManagerFamilyTypes)); return; }
            
            var list = SelectedManagerFamily.Types.Where(PassesManagerTypeFilter).ToList();
            if (list.Count == 0) { _selectAllManagerFamilyTypes = false; }
            else
            {
                bool allChecked = list.All(f => f.IsSelected);
                bool noneChecked = list.All(f => !f.IsSelected);
                _selectAllManagerFamilyTypes = allChecked ? (bool?)true : (noneChecked ? (bool?)false : null);
            }
            OnPropertyChanged(nameof(SelectAllManagerFamilyTypes));
        }

        private bool PassesManagerTypeFilter(FamilyManagerTypeItem item)
        {
            if (item == null) return false;
            // Hide Loaded Types (as per user request)
            if (item.IsAlreadyInTarget) return false;

            if (string.IsNullOrWhiteSpace(ManagerFamilyTypeSearchText)) return true;
            return item.TypeName.IndexOf(ManagerFamilyTypeSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool PassesManagerFamilyFilter(FamilyManagerItem item)
        {
            if (item == null) return false;

            // Search Filter
            if (string.IsNullOrWhiteSpace(FamilyManagerSearchText)) return true;
            return item.FamilyName.IndexOf(FamilyManagerSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.CategoryName.IndexOf(FamilyManagerSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public ICommand BrowseManagerFolderCommand => new RelayCommand(_ =>
        {
            try {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Directory containing Revit Families"
                };
                if (dialog.ShowDialog() == true)
                {
                    ScanManagerFolder(dialog.FolderName, useCache: true);
                }
            } catch {
                StatusText = "Folder dialog not supported on this OS framework version.";
            }
        });

        public ICommand RebuildManagerIndexCommand => new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(FamilyManagerFolderPath) && Directory.Exists(FamilyManagerFolderPath))
            {
                ScanManagerFolder(FamilyManagerFolderPath, useCache: false);
            }
            else
            {
                StatusText = "Select a valid folder first before rebuilding index.";
            }
        });

        public ICommand ProcessSelectedManagerFamiliesCommand => new RelayCommand(_ =>
        {
            // Collect selected via checkboxes
            var selected = AvailableManagerFamilies.Where(f => f.IsSelected).ToList();
            
            // UX Improvement: If current highlighted family has checked types, include it even if parent is not checked
            if (SelectedManagerFamily != null && !SelectedManagerFamily.IsSelected)
            {
                if (SelectedManagerFamily.Types.Any(t => t.IsSelected))
                {
                    selected.Add(SelectedManagerFamily);
                }
            }

            if (selected.Count == 0)
            {
                StatusText = "Please select at least one family to process.";
                return;
            }

            FmRequestHandler.FamiliesToProcess = selected;
            StatusText = $"Batch processing {selected.Count} families...";
            FmExEvent.Raise();
        });

        private void OnManagerProcessCompleted(object sender, FamilyManagerProcessResultEventArgs e)
        {
            foreach (var f in AvailableManagerFamilies) 
            {
                f.IsSelected = false;
                // Update SelectAll state for sub-types
                foreach (var t in f.Types) t.IsSelected = false;
            }
            _selectAllManagerFamilies = false;
            _selectAllManagerFamilyTypes = false;
            OnPropertyChanged(nameof(SelectAllManagerFamilies));
            OnPropertyChanged(nameof(SelectAllManagerFamilyTypes));

            if (e.Errors != null && e.Errors.Count > 0)
                StatusText = $"Loaded {e.LoadedCount}, Updated {e.UpdatedCount}. Errors: {e.Errors.Count}";
            else
                StatusText = $"Successfully Loaded {e.LoadedCount} and Updated {e.UpdatedCount} families.";

            // Re-check statuses in the main document
            RefreshFamilyStatuses();

            // Trigger filter refresh for the current view
            UpdateFilteredManagerFamilyTypes();
            FilteredManagerFamilies?.Refresh();
        }

        public void RefreshFamilyStatuses()
        {
            if (_uiApp?.ActiveUIDocument?.Document == null) return;
            var doc = _uiApp.ActiveUIDocument.Document;

            // 1. Get all loaded families in the project
            var loadedFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => f.Name)
                .ToList();

            // 2. Get all loaded symbols for all families (to check types)
            var loadedSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .GroupBy(s => s.FamilyName)
                .ToDictionary(g => g.Key, g => g.Select(s => s.Name).ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var family in AvailableManagerFamilies)
            {
                bool isLoaded = loadedFamilies.Contains(family.FamilyName, StringComparer.OrdinalIgnoreCase);
                family.Status = isLoaded ? "Loaded" : "Missing";

                if (family.Types != null && family.Types.Count > 0)
                {
                    if (loadedSymbols.TryGetValue(family.FamilyName, out var symbols))
                    {
                        foreach (var type in family.Types)
                        {
                            bool exists = symbols.Contains(type.TypeName, StringComparer.OrdinalIgnoreCase);
                            type.IsAlreadyInTarget = exists;
                            type.Status = exists ? "Loaded" : "Missing";
                        }
                    }
                    else
                    {
                         foreach (var type in family.Types)
                         {
                             type.IsAlreadyInTarget = false;
                             type.Status = "Missing";
                         }
                    }
                }
            }
            
            // Refresh views
            FilteredManagerFamilies?.Refresh();
            FilteredManagerFamilyTypes?.Refresh();
        }

        public void Cleanup()
        {
            RequestHandler.TransferCompleted -= OnTransferCompleted;
            if (FmRequestHandler != null) FmRequestHandler.ProcessCompleted -= OnManagerProcessCompleted;
            if (TypesRequestHandler != null) TypesRequestHandler.TypesReadCompleted -= OnTypesReadCompleted;
            _fileManager?.Cleanup();
        }

        private void OnTransferCompleted(object sender, EventArgs e)
        {
            // Reset all selections
            foreach (var v in AvailableViews) v.IsSelected = false;
            foreach (var s in AvailableSheets) s.IsSelected = false;
            foreach (var f in AvailableFamilies)
            {
                f.IsSelected = false;
                foreach (var t in f.Types) t.IsSelected = false;
            }
            foreach (var st in AvailableSystemTypes) st.IsSelected = false;

            // Reset headers
            _selectAllViews = false;
            _selectAllSheets = false;
            _selectAllFamilies = false;
            _selectAllFamilyTypes = false;
            _selectAllViewports = false;
            _selectAllSystemTypes = false;

            OnPropertyChanged(nameof(SelectAllViews));
            OnPropertyChanged(nameof(SelectAllSheets));
            OnPropertyChanged(nameof(SelectAllFamilies));
            OnPropertyChanged(nameof(SelectAllFamilyTypes));
            OnPropertyChanged(nameof(SelectAllViewports));
            OnPropertyChanged(nameof(SelectAllSystemTypes));

            // Refresh data (to update "Already in Target" status)
            if (!string.IsNullOrEmpty(SourceFilePath))
            {
                LoadSourceModel(SourceFilePath);
            }
            
            RefreshFamilyStatuses();
            StatusText = "Transfer completed successfully.";
        }

        // ===== Standard 1 / Standard 2 Quick-Load =====

        public string Standard1Path
        {
            get => _standard1Path;
            set
            {
                _standard1Path = value;
                _transferSettings.Standard1Path = value;
                _transferSettings.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Standard1Label));
                OnPropertyChanged(nameof(IsStandard1Loaded));
            }
        }

        public string Standard2Path
        {
            get => _standard2Path;
            set
            {
                _standard2Path = value;
                _transferSettings.Standard2Path = value;
                _transferSettings.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Standard2Label));
                OnPropertyChanged(nameof(IsStandard2Loaded));
            }
        }

        public string Standard1Label => string.IsNullOrEmpty(_standard1Path)
            ? "⚙ Set Standard 1..."
            : "📁 " + Path.GetFileNameWithoutExtension(_standard1Path);

        public string Standard2Label => string.IsNullOrEmpty(_standard2Path)
            ? "⚙ Set Standard 2..."
            : "📁 " + Path.GetFileNameWithoutExtension(_standard2Path);

        public bool IsStandard1Loaded => !string.IsNullOrEmpty(_standard1Path) && string.Equals(SourceFilePath, _standard1Path, StringComparison.OrdinalIgnoreCase);
        public bool IsStandard2Loaded => !string.IsNullOrEmpty(_standard2Path) && string.Equals(SourceFilePath, _standard2Path, StringComparison.OrdinalIgnoreCase);

        public ICommand LoadStandard1Command => new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(_standard1Path) && File.Exists(_standard1Path))
                LoadSourceModel(_standard1Path);
            else
                StatusText = "Standard 1 not set. Right-click to set a file path.";
        });

        public ICommand LoadStandard2Command => new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(_standard2Path) && File.Exists(_standard2Path))
                LoadSourceModel(_standard2Path);
            else
                StatusText = "Standard 2 not set. Right-click to set a file path.";
        });

        // ===== Family Folder Favorites =====

        public string Folder1Path
        {
            get => _folder1Path;
            set
            {
                _folder1Path = value;
                _transferSettings.Folder1Path = value;
                _transferSettings.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Folder1Label));
                OnPropertyChanged(nameof(IsFolder1Active));
                OnPropertyChanged(nameof(IsLibraryFolderLinked));
            }
        }

        public string Folder2Path
        {
            get => _folder2Path;
            set
            {
                _folder2Path = value;
                _transferSettings.Folder2Path = value;
                _transferSettings.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Folder2Label));
                OnPropertyChanged(nameof(IsFolder2Active));
            }
        }

        public string Folder1Label => string.IsNullOrEmpty(_folder1Path)
            ? "⚙ Set Folder 1..."
            : "📁 " + new DirectoryInfo(_folder1Path).Name;

        public string Folder2Label => string.IsNullOrEmpty(_folder2Path)
            ? "⚙ Set Folder 2..."
            : "📁 " + new DirectoryInfo(_folder2Path).Name;

        public bool IsFolder1Active => !string.IsNullOrEmpty(_folder1Path) && string.Equals(FamilyManagerFolderPath, _folder1Path, StringComparison.OrdinalIgnoreCase);
        public bool IsFolder2Active => !string.IsNullOrEmpty(_folder2Path) && string.Equals(FamilyManagerFolderPath, _folder2Path, StringComparison.OrdinalIgnoreCase);

        public ICommand LoadFolder1Command => new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(_folder1Path) && Directory.Exists(_folder1Path))
            {
                ScanManagerFolder(_folder1Path, useCache: true);
                OnPropertyChanged(nameof(IsFolder1Active));
                OnPropertyChanged(nameof(IsFolder2Active));
            }
            else
                StatusText = "Folder 1 not set. Right-click to set a folder path.";
        });

        public ICommand LoadFolder2Command => new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(_folder2Path) && Directory.Exists(_folder2Path))
            {
                ScanManagerFolder(_folder2Path, useCache: true);
                OnPropertyChanged(nameof(IsFolder1Active));
                OnPropertyChanged(nameof(IsFolder2Active));
            }
            else
                StatusText = "Folder 2 not set. Right-click to set a folder path.";
        });

        // ============================================================
        // DUPLICATOR — Auto-match + commands
        // ============================================================

        /// <summary>
        /// Searches the indexed Family Manager families for a type name that matches the input.
        /// Uses exact match first, then case-insensitive, then partial/contains.
        /// </summary>
        public void AutoMatchDuplicatorRow(DuplicatorRow row)
        {
            if (string.IsNullOrWhiteSpace(row.TypeComment))
            {
                row.BaseFamily = null;
                row.BaseFamilyPath = null;
                row.BaseTypeName = null;
                row.Status = null;
                return;
            }

            string keyword = row.TypeComment.Trim();

            // Search through loaded project families FIRST, then indexed library families
            var allMatches = new List<AvailableMatchItem>();
            var collectionsToSearch = new[] { LoadedProjectFamilies, AvailableManagerFamilies };

            foreach (var collection in collectionsToSearch)
            {
                if (collection == null || !collection.Any()) continue;

                var allowedCategoryNames = new[] { "Structural Framing", "Structural Columns", "Structural Foundations", "Walls", "Floors" };
                var structuralFamilies = collection.Where(f => f.CategoryName != null && allowedCategoryNames.Any(c => f.CategoryName.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (structuralFamilies.Count == 0) continue;

                // Pass 1: Exact match on TypeName
                foreach (var family in structuralFamilies)
                {
                    foreach (var type in family.Types)
                    {
                        if (string.Equals(type.TypeName, keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            allMatches.Add(new AvailableMatchItem { FamilyName = family.FamilyName, FilePath = family.FilePath, TypeName = type.TypeName });
                        }
                    }
                }

                // Pass 2: Partial match — keyword contained in TypeName
                foreach (var family in structuralFamilies)
                {
                    foreach (var type in family.Types)
                    {
                        if (type.TypeName != null && type.TypeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            allMatches.Add(new AvailableMatchItem { FamilyName = family.FamilyName, FilePath = family.FilePath, TypeName = type.TypeName });
                        }
                    }
                }

                // Pass 3: Inverse Partial match — TypeName contained in keyword (Phase 5)
                foreach (var family in structuralFamilies)
                {
                    foreach (var type in family.Types)
                    {
                        if (!string.IsNullOrEmpty(type.TypeName) && keyword.IndexOf(type.TypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            allMatches.Add(new AvailableMatchItem { FamilyName = family.FamilyName, FilePath = family.FilePath, TypeName = type.TypeName });
                        }
                    }
                }

                // Pass 4: Keyword contained in FamilyName (e.g. user types "UB")
                foreach (var family in structuralFamilies)
                {
                    if (family.FamilyName != null && family.FamilyName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        allMatches.Add(new AvailableMatchItem { FamilyName = family.FamilyName, FilePath = family.FilePath, TypeName = null });
                    }
                }
            }

            // Deduplicate matches so we don't show the exact same Family+Type combination multiple times
            var uniqueMatches = allMatches.GroupBy(m => m.DisplayString).Select(g => g.First()).ToList();

            // Needs to avoid firing property changed loop issues if we assign same match
            row.AvailableMatches = new ObservableCollection<AvailableMatchItem>(uniqueMatches);
            
            if (uniqueMatches.Any())
            {
                row.SelectedMatch = uniqueMatches.First();
            }
            else
            {
                row.SelectedMatch = null;
            }
        }

        public ICommand AddDuplicatorRowCommand => new RelayCommand(_ =>
        {
            var row = new DuplicatorRow();
            row.PropertyChanged += DuplicatorRow_PropertyChanged;
            DuplicatorRows.Add(row);
        });

        public ICommand ClearDuplicatorRowsCommand => new RelayCommand(_ =>
        {
            DuplicatorRows.Clear();
        });

        public ICommand GenerateDuplicatorCommand => new RelayCommand(_ =>
        {
            var validRows = DuplicatorRows.Where(r =>
                !string.IsNullOrEmpty(r.PreviewName) &&
                !string.IsNullOrEmpty(r.BaseFamilyPath)).ToList();

            if (!validRows.Any())
            {
                StatusText = "No valid rows to generate. Ensure Type Mark and Type Comment are filled.";
                return;
            }

            DupRequestHandler.RowsToProcess = validRows;
            DupRequestHandler.StatusCallback = msg =>
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => StatusText = msg);
            DupExEvent.Raise();
            StatusText = "Generating types...";
        });

        private void DuplicatorRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DuplicatorRow.TypeComment))
            {
                var row = sender as DuplicatorRow;
                if (row != null) AutoMatchDuplicatorRow(row);
            }
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
