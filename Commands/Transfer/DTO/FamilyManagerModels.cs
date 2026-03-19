using System;
using System.Collections.ObjectModel;

namespace antiGGGravity.Commands.Transfer.DTO
{
    public class FamilyManagerItem : BaseObservable
    {
        private string _filePath;
        private string _familyName;
        private string _categoryName;
        private string _status;
        private bool _isSelected;

        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); } }
        public string FamilyName { get => _familyName; set { _familyName = value; OnPropertyChanged(); } }
        public string CategoryName { get => _categoryName; set { _categoryName = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public ObservableCollection<FamilyManagerTypeItem> Types { get; set; } = new ObservableCollection<FamilyManagerTypeItem>();
    }

    public class FamilyManagerTypeItem : BaseObservable
    {
        private string _typeName;
        private bool _isSelected;
        private bool _isAlreadyInTarget;
        private string _status;

        public string TypeName { get => _typeName; set { _typeName = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public bool IsAlreadyInTarget { get => _isAlreadyInTarget; set { _isAlreadyInTarget = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    }
}
