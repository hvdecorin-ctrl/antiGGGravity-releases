using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Rebar;

namespace antiGGGravity.Commands.Rebar
{
    public class RebarHostMarkRefreshHandler : IExternalEventHandler
    {
        private RebarHostMarkWindow _window;
        
        public void SetWindow(RebarHostMarkWindow window)
        {
            _window = window;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_window == null) return;
                var result = RebarHostMarkService.Scan(app);
                if (result != null)
                {
                    _window.LoadResult(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RebarHostMarkRefreshHandler Error: " + ex.Message);
            }
        }

        public string GetName() => "RebarHostMarkRefreshHandler";
    }

    [Transaction(TransactionMode.Manual)]
    public class RebarQtyHostMarkCommand : BaseCommand
    {
        private static RebarHostMarkWindow _window;
        private static ExternalEvent _refreshEvent;
        private static RebarHostMarkRefreshHandler _refreshHandler;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (uiApp.ActiveUIDocument == null)
            {
                TaskDialog.Show("Rebar Host Mark", "Please open a project first.");
                return Result.Cancelled;
            }

            // If window is already open, bring it to front and refresh
            if (_window != null)
            {
                try
                {
                    _window.Activate();
                    _window.Focus();
                    var result = RebarHostMarkService.Scan(uiApp);
                    _window.LoadResult(result);
                    return Result.Succeeded;
                }
                catch
                {
                    _window = null;
                }
            }

            // Scan rebar data
            var initialResult = RebarHostMarkService.Scan(uiApp);

            // Create the refresh handler + external event
            _refreshHandler = new RebarHostMarkRefreshHandler();
            _refreshEvent = ExternalEvent.Create(_refreshHandler);

            // Create modeless window
            _window = new RebarHostMarkWindow(initialResult, _refreshEvent, _refreshHandler);

            // Set Revit as owner so the window stays on top
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var wrapper = new System.Windows.Interop.WindowInteropHelper(_window);
                wrapper.Owner = process.MainWindowHandle;
            }
            catch { }

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
