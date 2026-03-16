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

        public ViewTransferWindow(UIApplication uiApp, TransferRequestHandler handler, ExternalEvent exEvent)
        {
            InitializeComponent();
            _viewModel = new ViewTransferViewModel(uiApp, handler, exEvent);
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

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Cleanup();
            base.OnClosed(e);
        }
    }
}
