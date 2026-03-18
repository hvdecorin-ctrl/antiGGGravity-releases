using System;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using System.Diagnostics;

namespace antiGGGravity.Commands.Transfer.UI
{
    public partial class ViewTransferWindow : Window
    {
        private ViewTransferViewModel _viewModel;

        public ViewTransferWindow(UIApplication uiApp, TransferRequestHandler handler, ExternalEvent exEvent, FamilyManagerRequestHandler fmHandler, ExternalEvent fmExEvent, ReadFamilyTypesHandler typesHandler, ExternalEvent typesExEvent)
        {
            InitializeComponent();
            _viewModel = new ViewTransferViewModel(uiApp, handler, exEvent, fmHandler, fmExEvent, typesHandler, typesExEvent);
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
