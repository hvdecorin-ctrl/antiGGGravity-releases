using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.VisibilityGraphic;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class QuickVgCommand : BaseCommand
    {
        private static QuickVgView _window;
        private static ExternalEvent _externalEvent;
        private static QuickVgEventHandler _handler;

        protected override bool RequiresLicense => false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            if (_window == null || !_window.IsLoaded)
            {
                _handler = new QuickVgEventHandler();
                _externalEvent = ExternalEvent.Create(_handler);
                _window = new QuickVgView(uiApp, _externalEvent, _handler);

                // Set Revit as the parent window to ensure safe focus and closing behavior
                try
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    var wrapper = new System.Windows.Interop.WindowInteropHelper(_window);
                    wrapper.Owner = process.MainWindowHandle;
                }
                catch { }

                _window.Closed += (s, e) => 
                { 
                    try { _externalEvent?.Dispose(); } catch { }
                    _externalEvent = null;
                    _handler = null;
                    _window = null; 
                };
                _window.Show();
            }
            else
            {
                _window.Activate();
                _window.Focus();
            }

            return Result.Succeeded;
        }
    }
}
