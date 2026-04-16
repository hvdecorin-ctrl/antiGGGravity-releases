using System;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using System.Diagnostics;

namespace antiGGGravity.Commands.Transfer.UI
{
    public partial class FamilyManagerWindow : Window
    {
        private FamilyManagerViewModel _viewModel;

        public FamilyManagerWindow(UIApplication uiApp, TransferRequestHandler handler, ExternalEvent externalEvent, FamilyManagerRequestHandler fmHandler, ExternalEvent fmExternalEvent, ReadFamilyTypesHandler typesHandler, ExternalEvent typesExEvent, DuplicatorRequestHandler dupHandler, ExternalEvent dupExEvent)
        {
            InitializeComponent();
            _viewModel = new FamilyManagerViewModel(uiApp, handler, externalEvent, fmHandler, fmExternalEvent, typesHandler, typesExEvent, dupHandler, dupExEvent);
            this.DataContext = _viewModel;

            // Set Revit as owner to prevent crashes with Style=None and Transparency=True
            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = Process.GetCurrentProcess().MainWindowHandle;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.Cleanup();
            this.Close();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Revit Files (*.rvt)|*.rvt",
                Title = "Select Source Revit Document to Transfer From"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.LoadSourceModel(openFileDialog.FileName);
            }
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExecuteTransfer();
        }

        private void SetStandard1_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var path = BrowseForRvtFile("Set Standard 1 Source File");
            if (path != null)
            {
                _viewModel.Standard1Path = path;
                _viewModel.LoadSourceModel(path);
            }
            e.Handled = true;
        }

        private void SetStandard2_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var path = BrowseForRvtFile("Set Standard 2 Source File");
            if (path != null)
            {
                _viewModel.Standard2Path = path;
                _viewModel.LoadSourceModel(path);
            }
            e.Handled = true;
        }

        private void SetFolder1_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var path = BrowseForFolderPath("Set Favorite Folder 1");
            if (path != null)
            {
                _viewModel.Folder1Path = path;
                _viewModel.ScanManagerFolder(path);
            }
            e.Handled = true;
        }

        private void SetFolder2_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var path = BrowseForFolderPath("Set Favorite Folder 2");
            if (path != null)
            {
                _viewModel.Folder2Path = path;
                _viewModel.ScanManagerFolder(path);
            }
            e.Handled = true;
        }

        private void SetDuplicatorFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseForFolderPath("Set Base Library Folder");
            if (path != null)
            {
                _viewModel.Folder1Path = path;
                _viewModel.ScanManagerFolder(path);
            }
        }

        private string BrowseForFolderPath(string title)
        {
#if REVIT2025_OR_GREATER
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = title
            };
            return dialog.ShowDialog() == true ? dialog.FolderName : null;
#else
            // Fallback for .NET 4.8 (R22-R24) - Using OpenFileDialog hack since we can't use WinForms
            var dialog = new OpenFileDialog
            {
                Title = title + " (Select any file in the folder)",
                CheckFileExists = false,
                FileName = "Select Folder",
                Filter = "Folders|*.none"
            };
            if (dialog.ShowDialog() == true)
            {
                return System.IO.Path.GetDirectoryName(dialog.FileName);
            }
            return null;
#endif
        }

        private string BrowseForRvtFile(string title)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Revit Files (*.rvt)|*.rvt",
                Title = title
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Cleanup();
            base.OnClosed(e);
        }
    }
}
