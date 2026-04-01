using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using antiGGGravity.Commands.ProjectAudit;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class OverlapResolverView : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly OverlapResolverHandler _handler;
        private readonly Document _doc;

        public OverlapResolverView(ExternalEvent externalEvent, OverlapResolverHandler handler, Document doc)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            _doc = doc;

            _handler.StatusCallback = (msg) => Dispatcher.Invoke(() => UI_Status.Text = msg);
            _handler.OperationCompleted = () => Dispatcher.Invoke(() => UI_Status.Text = "Done.");

            // Populate padding options
            ComboPadding.Items.Add("1.0 mm");
            ComboPadding.Items.Add("1.5 mm");
            ComboPadding.Items.Add("2.0 mm");
            ComboPadding.Items.Add("3.0 mm");
            ComboPadding.Items.Add("5.0 mm");
            ComboPadding.SelectedIndex = 2; // Default: 2.0 mm
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 1. Set Scope
            if (RadEntireProject.IsChecked == true)
                _handler.Scope = ApplicationScope.EntireProject;
            else if (RadActiveView.IsChecked == true)
                _handler.Scope = ApplicationScope.ActiveView;
            else
                _handler.Scope = ApplicationScope.Selection;

            // 2. Set Element Types
            _handler.IncludeTextNotes = ChkTextNotes.IsChecked == true;
            _handler.IncludeTags = ChkTags.IsChecked == true;

            if (!_handler.IncludeTextNotes && !_handler.IncludeTags)
            {
                UI_Status.Text = "Select at least one element type.";
                return;
            }

            // 3. Set Padding
            string paddingStr = ComboPadding.SelectedItem as string ?? "2.0 mm";
            if (double.TryParse(paddingStr.Replace(" mm", ""), out double padding))
                _handler.PaddingMm = padding;
            else
                _handler.PaddingMm = 2.0;

            // 4. Fire
            _externalEvent.Raise();
            UI_Status.Text = "Resolving overlaps...";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
