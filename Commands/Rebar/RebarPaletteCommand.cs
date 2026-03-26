using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Rebar;

namespace antiGGGravity.Commands.Rebar
{
    [Transaction(TransactionMode.Manual)]
    public class RebarPaletteCommand : BaseCommand
    {
        private static RebarPaletteView _window;
        private static ExternalEvent _externalEvent;
        private static RebarPaletteEventHandler _handler;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            if (_window == null || !_window.IsLoaded)
            {
                _handler = new RebarPaletteEventHandler();
                _externalEvent = ExternalEvent.Create(_handler);
                _window = new RebarPaletteView(_externalEvent, _handler);

                // Set Revit as owner so the palette stays on top of Revit
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
