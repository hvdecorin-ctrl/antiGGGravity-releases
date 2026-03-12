using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class LoadMoreTypeView : Window
    {
        private readonly ExternalEvent _loadEvent;
        private readonly Commands.ProjectAudit.LoadFamilyTypesHandler _loadHandler;
        private readonly string _familyPath;
        private bool _isLoading;

        public ObservableCollection<MoreTypeItem> TypeItems { get; set; } = new();

        public LoadMoreTypeView(
            string familyName,
            string familyPath,
            List<string> options,
            ExternalEvent loadEvent,
            Commands.ProjectAudit.LoadFamilyTypesHandler loadHandler)
        {
            InitializeComponent();
            
            _familyPath = familyPath;
            _loadEvent = loadEvent;
            _loadHandler = loadHandler;
            
            UI_Subtitle.Text = $"Found {options.Count} more types in {familyName}";
            
            foreach (var typeName in options)
            {
                TypeItems.Add(new MoreTypeItem { Name = typeName, IsChecked = false }); // Default to all unchecked
            }
            
            UI_List_Types.ItemsSource = TypeItems;
        }

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
            if (_isLoading) return;

            var checkedTypes = TypeItems.Where(t => t.IsChecked).Select(t => t.Name).ToList();
            if (!checkedTypes.Any())
            {
                MessageBox.Show("Please select at least one type to load.", "Selection Empty");
                return;
            }

            _loadHandler.SetData(_familyPath, checkedTypes, (success, error) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _isLoading = false;
                    UI_Btn_Load.IsEnabled = true;
                    if (success)
                    {
                        TaskDialog.Show("Success", $"Successfully loaded {checkedTypes.Count} types.");
                        Close();
                    }
                    else
                    {
                        TaskDialog.Show("Error", "Failed to load types: " + error);
                    }
                });
            });

            _isLoading = true;
            UI_Btn_Load.IsEnabled = false;
            _loadEvent.Raise();
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Class for the list items
    public class MoreTypeItem : INotifyPropertyChanged
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
