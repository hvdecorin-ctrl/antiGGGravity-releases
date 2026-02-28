using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Rebar;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// ExternalEventHandler that re-scans the document and refreshes the window.
    /// Required because Revit API calls must run on the main thread.
    /// </summary>
    public class RebarQuantityRefreshHandler : IExternalEventHandler
    {
        private RebarQuantityWindow _window;
        
        public void SetWindow(RebarQuantityWindow window)
        {
            _window = window;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_window == null) return;

                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null || !doc.IsValidObject) return;

                var result = RebarQuantityService.Scan(doc);
                if (result != null)
                {
                    _window.LoadResult(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RebarQuantityRefreshHandler Error: " + ex.Message);
            }
        }

        public string GetName() => "RebarQuantityRefreshHandler";
    }

    /// <summary>
    /// Ribbon command that opens the Quick Rebar Q'ty modeless window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarQuantityCommand : BaseCommand
    {
        private static RebarQuantityWindow _window;
        private static ExternalEvent _refreshEvent;
        private static RebarQuantityRefreshHandler _refreshHandler;

        protected override bool RequiresLicense => false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (uiApp.ActiveUIDocument == null)
            {
                TaskDialog.Show("Quick Rebar Q'ty", "Please open a project first.");
                return Result.Cancelled;
            }

            Document doc = uiApp.ActiveUIDocument.Document;

            // If window is already open, bring it to front and refresh
            if (_window != null)
            {
                try
                {
                    _window.Activate();
                    _window.Focus();
                    // Also refresh the data
                    var result = RebarQuantityService.Scan(doc);
                    _window.LoadResult(result);
                    return Result.Succeeded;
                }
                catch
                {
                    _window = null;
                }
            }

            // Scan rebar data
            var initialResult = RebarQuantityService.Scan(doc);

            // Create the refresh handler + external event
            _refreshHandler = new RebarQuantityRefreshHandler();
            _refreshEvent = ExternalEvent.Create(_refreshHandler);

            // Create modeless window
            _window = new RebarQuantityWindow(initialResult, _refreshEvent, _refreshHandler);

            // Set Revit as owner so the window stays on top
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var wrapper = new System.Windows.Interop.WindowInteropHelper(_window);
                wrapper.Owner = process.MainWindowHandle;
            }
            catch { }

            // Wire up cleanup on close
            _window.Closed += (s, e) =>
            {
                try { _refreshEvent?.Dispose(); } catch { }
                _refreshEvent = null;
                _refreshHandler = null;
                _window = null;
            };

            _window.Show();
            return Result.Succeeded;
        }
    }
}
