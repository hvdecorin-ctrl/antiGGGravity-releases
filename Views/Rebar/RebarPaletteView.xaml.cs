using System.Windows;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.Rebar;

namespace antiGGGravity.Views.Rebar
{
    public partial class RebarPaletteView : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly RebarPaletteEventHandler _handler;

        public RebarPaletteView(ExternalEvent externalEvent, RebarPaletteEventHandler handler)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
        }

        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string commandName)
            {
                // Send command back to Revit main thread
                _handler.CommandToPost = commandName;
                _externalEvent.Raise();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
