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
        
        private static ElementId _lastViewId = ElementId.InvalidElementId;
        private static ElementId _lastTemplateId = ElementId.InvalidElementId;
        private static bool _lastWasTempMode = false;
        private static bool _isSubscribed = false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            if (!_isSubscribed)
            {
                uiApp.Idling += OnIdling;
                _isSubscribed = true;
            }

            if (_window == null || !_window.IsLoaded)
            {
                _handler = new QuickVgEventHandler();
                _externalEvent = ExternalEvent.Create(_handler);
                _window = new QuickVgView(uiApp, _externalEvent, _handler);

                // Set initial state to prevent immediate double-refresh
                var doc = uiApp.ActiveUIDocument?.Document;
                var view = uiApp.ActiveUIDocument?.ActiveView;
                if (view != null)
                {
                    _lastViewId = view.Id;
                    _lastTemplateId = view.ViewTemplateId;
                    _lastWasTempMode = view.IsTemporaryViewPropertiesModeEnabled();
                }

                // Set Revit as the parent window
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

        private static void OnIdling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            if (_window == null || !_window.IsLoaded) return;

            var uiApp = sender as UIApplication;
            var doc = uiApp?.ActiveUIDocument?.Document;
            var view = uiApp?.ActiveUIDocument?.ActiveView;

            if (view == null || !view.IsValidObject) return;

            bool needsRefresh = false;

            // 1. Check if View switched
            if (view.Id != _lastViewId)
            {
                _lastViewId = view.Id;
                needsRefresh = true;
            }

            // 2. Check if Template changed
            if (view.ViewTemplateId != _lastTemplateId)
            {
                _lastTemplateId = view.ViewTemplateId;
                needsRefresh = true;
            }

            // 3. Check if Temporary View Properties toggled
            bool isTemp = view.IsTemporaryViewPropertiesModeEnabled();
            if (isTemp != _lastWasTempMode)
            {
                _lastWasTempMode = isTemp;
                needsRefresh = true;
            }

            if (needsRefresh)
            {
                _window.RefreshView(view);
            }
        }
    }
}
