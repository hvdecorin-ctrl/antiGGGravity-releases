using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands;
using antiGGGravity.StructuralRebar.Core;
using antiGGGravity.StructuralRebar.UI;

namespace antiGGGravity.StructuralRebar
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnRebarCommand : BaseCommand
    {
        private static ColumnRebarWindow _window;
        private static ExternalEvent _externalEvent;
        private static RebarGenerateHandler _handler;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Run(commandData.Application);
        }

        public Result Run(UIApplication uiApp)
        {
            if (uiApp.ActiveUIDocument == null) return Result.Cancelled;

            if (_window != null)
            {
                try { _window.Activate(); _window.Focus(); return Result.Succeeded; }
                catch { _window = null; }
            }

            _handler = new RebarGenerateHandler(null);
            _externalEvent = ExternalEvent.Create(_handler);
            _window = new ColumnRebarWindow(uiApp.ActiveUIDocument, _externalEvent);
            _handler.SetWindow(_window);

            _window.Closed += (s, e) => { _window = null; _externalEvent?.Dispose(); _externalEvent = null; _handler = null; };
            _window.Show();

            return Result.Succeeded;
        }
    }
}
