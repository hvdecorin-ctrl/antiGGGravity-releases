using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace antiGGGravity.Commands.Transfer.DTO
{
    public class AvailableMatchItem
    {
        public string FamilyName { get; set; }
        public string FilePath { get; set; }
        public string TypeName { get; set; }
        public string DisplayString => $"{FamilyName} - {TypeName}";
    }

    public class DuplicatorRow : INotifyPropertyChanged
    {
        private string _typeMark;
        private string _typeComment;
        private string _description;
        private string _baseFamily;
        private string _baseFamilyPath;
        private string _baseTypeName;
        private string _previewName;
        private string _status;
        private ObservableCollection<AvailableMatchItem> _availableMatches = new ObservableCollection<AvailableMatchItem>();
        private AvailableMatchItem _selectedMatch;

        public string TypeMark
        {
            get => _typeMark;
            set { _typeMark = value; OnPropertyChanged(); UpdatePreviewName(); }
        }

        public string TypeComment
        {
            get => _typeComment;
            set
            {
                var oldComment = _typeComment;
                _typeComment = value;
                OnPropertyChanged();
                
                // Auto-fill Description if it's empty or was previously auto-filled with the old type comment
                if (string.IsNullOrEmpty(_description) || (!string.IsNullOrEmpty(oldComment) && _description == oldComment))
                    Description = value;
                    
                UpdatePreviewName();
            }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string BaseFamily
        {
            get => _baseFamily;
            set { _baseFamily = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Full file path to the .rfa family file (from the index).
        /// </summary>
        public string BaseFamilyPath
        {
            get => _baseFamilyPath;
            set { _baseFamilyPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// The matched type name inside the family (e.g. "200UB30").
        /// </summary>
        public string BaseTypeName
        {
            get => _baseTypeName;
            set { _baseTypeName = value; OnPropertyChanged(); }
        }

        public string PreviewName
        {
            get => _previewName;
            set { _previewName = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public ObservableCollection<AvailableMatchItem> AvailableMatches
        {
            get => _availableMatches;
            set { _availableMatches = value; OnPropertyChanged(); }
        }

        public AvailableMatchItem SelectedMatch
        {
            get => _selectedMatch;
            set
            {
                _selectedMatch = value;
                OnPropertyChanged();
                if (value != null)
                {
                    BaseFamily = value.FamilyName;
                    BaseFamilyPath = value.FilePath;
                    BaseTypeName = value.TypeName;
                    Status = "✅ Match";
                }
                else
                {
                    BaseFamily = null;
                    BaseFamilyPath = null;
                    BaseTypeName = null;
                    if (AvailableMatches == null || AvailableMatches.Count == 0)
                        Status = "⚠ No match";
                }
            }
        }

        private void UpdatePreviewName()
        {
            if (!string.IsNullOrEmpty(_typeMark) && !string.IsNullOrEmpty(_typeComment))
                PreviewName = $"{_typeMark}-{_typeComment}";
            else if (!string.IsNullOrEmpty(_typeMark))
                PreviewName = _typeMark;
            else
                PreviewName = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
