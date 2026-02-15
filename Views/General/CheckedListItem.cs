using System.ComponentModel;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public class CheckedListItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked 
        { 
            get => _isChecked; 
            set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); } 
        }
        public string Name { get; set; }
        public Category Category { get; set; }
        public Element Element { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
