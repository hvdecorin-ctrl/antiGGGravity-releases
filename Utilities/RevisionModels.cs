using System;
using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace antiGGGravity.Models
{
    public class RevisionViewModel : INotifyPropertyChanged
    {
        public Revision Revision { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        public string Label => $"{Revision.SequenceNumber} - {Revision.Description}";
        public string Date => Revision.RevisionDate;

        public RevisionViewModel(Revision rev) { Revision = rev; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SheetViewModel : INotifyPropertyChanged
    {
        public ViewSheet Sheet { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        public string DisplayName => $"{Sheet.SheetNumber} - {Sheet.Name}";

        public SheetViewModel(ViewSheet sheet) { Sheet = sheet; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum SelectionSourceType
    {
        Manual,
        Set,
        Schedule
    }

    public class SelectionSourceViewModel
    {
        public SelectionSourceType Type { get; set; }
        public string Name { get; set; }
        public object Object { get; set; }
    }
}
