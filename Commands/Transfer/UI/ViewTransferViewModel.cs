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
        private string _selectedCategory = "All";
        private SheetTransferItem _selectedSheet;

        public ObservableCollection<ViewTransferItem> AvailableViews { get; set; } = new ObservableCollection<ViewTransferItem>();
        public ObservableCollection<SheetTransferItem> AvailableSheets { get; set; } = new ObservableCollection<SheetTransferItem>();
        public ObservableCollection<ViewTransferItem> ViewportsInSelectedSheet { get; set; } = new ObservableCollection<ViewTransferItem>();
        
        public ICollectionView FilteredViews { get; private set; }
        public ICollectionView FilteredSheets { get; private set; }

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
            }
        }

        public bool IsAllSelected => SelectedCategory == "All";
        public bool IsDetailSelected => SelectedCategory == "Detail";
        public bool IsDraftingSelected => SelectedCategory == "Drafting";
        public bool IsLegendSelected => SelectedCategory == "Legend";

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
        }

        private void UpdateViewportsList()
        {
            ViewportsInSelectedSheet.Clear();
            if (SelectedSheet == null) return;

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

        public void LoadSourceModel(string path)
        {
            StatusText = "Loading model in background...";
            SourceFilePath = path;

            if (_fileManager.SelectAndLoadSourceFile(path, out string error))
            {
                _viewCollector = new ViewCollectorModule(_fileManager.DocLoader.SourceDocument);
                
                var views = _viewCollector.GetTransferableViews();
                var sheets = _viewCollector.GetSheets();

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

                IsSourceLoaded = true;
                StatusText = $"Loaded {views.Count} views, {sheets.Count} sheets.";
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

            if (selectedViews.Count == 0 && selectedSheets.Count == 0)
            {
                StatusText = "Please select at least one view or sheet.";
                return;
            }

            // Push to request handler
            RequestHandler.SourceDoc = _fileManager.DocLoader.SourceDocument;
            RequestHandler.Options = Options;
            RequestHandler.SelectedViews = selectedViews;
            RequestHandler.SelectedSheets = selectedSheets;

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
