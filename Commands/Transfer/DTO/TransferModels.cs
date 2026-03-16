using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.DTO
{
    public class BaseObservable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class TransferOptions : BaseObservable
    {
        private bool _transferViewports = true;
        private bool _transferViewTemplates = true;
        private bool _duplicateHandlingPrefix = true;
        private string _prefixString = "Copied_";

        public bool TransferViewports { get => _transferViewports; set { _transferViewports = value; OnPropertyChanged(); } }
        public bool TransferViewTemplates { get => _transferViewTemplates; set { _transferViewTemplates = value; OnPropertyChanged(); } }
        public bool DuplicateHandlingPrefix { get => _duplicateHandlingPrefix; set { _duplicateHandlingPrefix = value; OnPropertyChanged(); } }
        public string PrefixString { get => _prefixString; set { _prefixString = value; OnPropertyChanged(); } }
    }

    public class ViewTransferItem : BaseObservable
    {
        private ElementId _sourceViewId;
        private string _viewName;
        private ViewType _viewType;
        private bool _isSelected;

        public ElementId SourceViewId { get => _sourceViewId; set { _sourceViewId = value; OnPropertyChanged(); } }
        public string ViewName { get => _viewName; set { _viewName = value; OnPropertyChanged(); } }
        public ViewType ViewType { get => _viewType; set { _viewType = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    public class SheetTransferItem : BaseObservable
    {
        private ElementId _sourceSheetId;
        private string _sheetNumber;
        private string _sheetName;
        private bool _isSelected;

        public ElementId SourceSheetId { get => _sourceSheetId; set { _sourceSheetId = value; OnPropertyChanged(); } }
        public string SheetNumber { get => _sheetNumber; set { _sheetNumber = value; OnPropertyChanged(); } }
        public string SheetName { get => _sheetName; set { _sheetName = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public List<ViewTransferItem> PlacedViews { get; set; } = new List<ViewTransferItem>();
    }
}
