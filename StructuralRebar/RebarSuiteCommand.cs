using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands;
using antiGGGravity.StructuralRebar.Core;
using antiGGGravity.StructuralRebar.UI;

namespace antiGGGravity.StructuralRebar
{
    /// <summary>
    /// Single ribbon command that opens the unified Rebar Suite window.
    /// The window is modeless — stays open so the user can generate rebar repeatedly.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarSuiteCommand : BaseCommand
    {
        // Singleton references — kept alive while the window is open
        private static RebarSuiteWindow _window;
        private static ExternalEvent _externalEvent;
        private static RebarGenerateHandler _handler;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (uiApp.ActiveUIDocument == null)
            {
                TaskDialog.Show("Rebar Suite", "Please open a project first.");
                return Result.Cancelled;
            }

            Document doc = uiApp.ActiveUIDocument.Document;

            // If window is already open, bring it to front
            if (_window != null)
            {
                try
                {
                    _window.Activate();
                    _window.Focus();
                    return Result.Succeeded;
                }
                catch
                {
                    // Window was disposed or corrupted — recreate
                    _window = null;
                }
            }

            // Create the handler + external event
            _handler = new RebarGenerateHandler(null); // Window ref set below
            _externalEvent = ExternalEvent.Create(_handler);

            // Create modeless window
            _window = new RebarSuiteWindow(doc, _externalEvent);
            _handler.SetWindow(_window);

            // Wire up the Closed event to clean up references
            _window.Closed += (s, e) =>
            {
                _window = null;
                try
                {
                    _externalEvent?.Dispose();
                }
                finally
                {
                    _externalEvent = null;
                    _handler = null;
                }
            };

            _window.Show();
            return Result.Succeeded;
        }
    }
}
